using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

class SimpleServer
{
    // 模型远程地址（通过 gh-proxy 镜像加速，直连 GitHub 在国内极慢）
    const string MODEL_REMOTE_URL = "https://gh-proxy.com/https://github.com/panhanmo/dongming-school-3d/releases/download/V1.0.0/point_cloud_6999_.+.+.+.splat";

    static void Main(string[] args)
    {
        int port = 8080;
        string root = Directory.GetCurrentDirectory();
        if (args.Length > 0) port = int.Parse(args[0]);

        // 启用 TLS 1.2（GitHub 要求）
        ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
        ServicePointManager.DefaultConnectionLimit = 10;

        var listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:" + port + "/");
        listener.Start();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("================================");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("  Server started: http://localhost:" + port);
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("  Press Ctrl+C to stop");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("================================");
        Console.ResetColor();
        Console.WriteLine();

        // 多线程：每个请求在独立线程处理，模型流式传输时不阻塞其他请求
        while (listener.IsListening)
        {
            try
            {
                var ctx = listener.GetContext();
                ThreadPool.QueueUserWorkItem(_ => HandleRequest(ctx, root));
            }
            catch (Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("ERROR: " + e.Message);
                Console.ResetColor();
            }
        }
    }

    static void HandleRequest(HttpListenerContext ctx, string root)
    {
        try
        {
            var req = ctx.Request;
            var res = ctx.Response;
            string path = req.Url.AbsolutePath;

            // -------- 拦截 @vite/client --------
            if (path == "/@vite/client" || path == "/vite/client")
            {
                res.ContentType = "application/javascript; charset=utf-8";
                res.Headers.Add("Access-Control-Allow-Origin", "*");
                byte[] dummy = Encoding.UTF8.GetBytes("// vite client disabled");
                res.ContentLength64 = dummy.Length;
                res.OutputStream.Write(dummy, 0, dummy.Length);
                res.Close();
                return;
            }

            // -------- /api/model 代理端点：流式转发 GitHub 模型，不缓存本地 --------
            if (path == "/api/model")
            {
                HandleModelProxy(req, res);
                res.Close();
                return;
            }

            // -------- /api/backgrounds 背景图列表端点 --------
            if (path == "/api/backgrounds")
            {
                HandleBackgrounds(res, root);
                res.Close();
                return;
            }

            if (path == "/") path = "/index.html";
            string relPath = Uri.UnescapeDataString(path.TrimStart('/'));
            string filePath = Path.Combine(root, relPath);

            string mime = "application/octet-stream";
            string ext = Path.GetExtension(filePath).ToLower();
            switch (ext)
            {
                case ".html": mime = "text/html; charset=utf-8"; break;
                case ".js": mime = "application/javascript; charset=utf-8"; break;
                case ".json": mime = "application/json; charset=utf-8"; break;
                case ".css": mime = "text/css; charset=utf-8"; break;
                case ".png": mime = "image/png"; break;
                case ".jpg": mime = "image/jpeg"; break;
                case ".ico": mime = "image/x-icon"; break;
                case ".svg": mime = "image/svg+xml"; break;
            }

            // 禁止缓存 HTML 和 JSON（确保代码更新后浏览器获取最新版本）
            if (ext == ".html" || ext == ".json")
            {
                res.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");
            }

            if (File.Exists(filePath))
            {
                long fileLen = new FileInfo(filePath).Length;
                res.ContentType = mime;
                res.ContentLength64 = fileLen;
                res.Headers.Add("Access-Control-Allow-Origin", "*");
                res.Headers.Add("Accept-Ranges", "bytes");

                using (var fs = File.OpenRead(filePath))
                {
                    byte[] buffer = new byte[131072];
                    int read;
                    while ((read = fs.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        res.OutputStream.Write(buffer, 0, read);
                    }
                }
                res.OutputStream.Flush();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("200 " + relPath + " " + fileLen + " bytes");
            }
            else
            {
                byte[] body = Encoding.UTF8.GetBytes("404 Not Found: " + relPath);
                res.ContentType = "text/plain";
                res.ContentLength64 = body.Length;
                res.OutputStream.Write(body, 0, body.Length);
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("404 " + relPath);
            }
            Console.ResetColor();
            res.Close();
        }
        catch (Exception e)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("ERROR: " + e.Message);
            Console.ResetColor();
            try { ctx.Response.Close(); } catch { }
        }
    }

    // ====================== 模型代理（流式转发，不缓存本地） ======================
    // 直接从 GitHub（通过 gh-proxy）流式转发给浏览器，不在本地保存任何文件。
    static void HandleModelProxy(HttpListenerRequest req, HttpListenerResponse res)
    {
        res.Headers.Add("Access-Control-Allow-Origin", "*");
        res.Headers.Add("Accept-Ranges", "bytes");
        res.ContentType = "application/octet-stream";

        try
        {
            var request = (HttpWebRequest)WebRequest.Create(MODEL_REMOTE_URL);
            request.Method = "GET";
            request.AllowAutoRedirect = true;
            request.Timeout = 300000;
            request.ReadWriteTimeout = 300000;
            request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)";

            // HEAD 请求：只获取文件大小，不下载 body
            if (req.HttpMethod == "HEAD")
            {
                request.Method = "HEAD";
                using (var response = request.GetResponse())
                {
                    res.ContentLength64 = response.ContentLength;
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("[model-proxy] HEAD 200 " + response.ContentLength + " bytes (from GitHub)");
                    Console.ResetColor();
                    return;
                }
            }

            // GET 请求：流式转发
            using (var response = request.GetResponse())
            using (var responseStream = response.GetResponseStream())
            {
                if (response.ContentLength > 0)
                    res.ContentLength64 = response.ContentLength;

                byte[] buffer = new byte[131072];
                int read;
                long total = 0;
                while ((read = responseStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    res.OutputStream.Write(buffer, 0, read);
                    total += read;
                    if (total % (10 * 1024 * 1024) < buffer.Length)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        Console.Write("\r[model-proxy] 已转发 " + (total / 1024 / 1024) + " MB...   ");
                        Console.ResetColor();
                    }
                }
                Console.WriteLine();
                res.OutputStream.Flush();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[model-proxy] 200 " + total + " bytes (streamed from GitHub)");
                Console.ResetColor();
            }
        }
        catch (Exception e)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[model-proxy] ERROR: " + e.Message);
            Console.ResetColor();
            SendError(res, 502, "模型加载失败: " + e.Message);
        }
    }

    // ====================== 背景图列表 ======================
    static void HandleBackgrounds(HttpListenerResponse res, string root)
    {
        res.Headers.Add("Access-Control-Allow-Origin", "*");
        res.ContentType = "application/json; charset=utf-8";

        string bgDir = Path.Combine(root, "blackground");
        var files = new System.Collections.Generic.List<string>();

        if (Directory.Exists(bgDir))
        {
            string[] jpgs = Directory.GetFiles(bgDir, "*.jpg");
            string[] pngs = Directory.GetFiles(bgDir, "*.png");
            foreach (var f in jpgs) files.Add("blackground/" + Path.GetFileName(f));
            foreach (var f in pngs) files.Add("blackground/" + Path.GetFileName(f));
        }

        var sb = new StringBuilder();
        sb.Append("{\"backgrounds\":[");
        for (int i = 0; i < files.Count; i++)
        {
            if (i > 0) sb.Append(",");
            sb.Append("\"").Append(files[i]).Append("\"");
        }
        sb.Append("]}");

        byte[] body = Encoding.UTF8.GetBytes(sb.ToString());
        res.ContentLength64 = body.Length;
        res.OutputStream.Write(body, 0, body.Length);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("[backgrounds] " + files.Count + " images");
        Console.ResetColor();
    }

    static void SendError(HttpListenerResponse res, int code, string msg)
    {
        try
        {
            res.StatusCode = code;
            byte[] body = Encoding.UTF8.GetBytes(msg);
            res.ContentType = "text/plain; charset=utf-8";
            res.ContentLength64 = body.Length;
            res.OutputStream.Write(body, 0, body.Length);
        }
        catch { }
    }
}
