using System;
using System.IO;
using System.Net;
using System.Text;

class SimpleServer
{
    static void Main(string[] args)
    {
        int port = 8080;
        string root = Directory.GetCurrentDirectory();
        if (args.Length > 0) port = int.Parse(args[0]);

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

        while (listener.IsListening)
        {
            try
            {
                var ctx = listener.GetContext();
                var req = ctx.Request;
                var res = ctx.Response;

                string path = req.Url.AbsolutePath;
                if (path == "/") path = "/index.html";
                string relPath = path.TrimStart('/');
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
            }
        }
    }
}
