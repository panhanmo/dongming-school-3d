/**
 * Cloudflare Worker: CORS 代理 for GitHub Release 模型文件
 *
 * 部署步骤：
 * 1. 登录 https://dash.cloudflare.com → Workers & Pages → Create
 * 2. 将此文件内容粘贴到 Worker 编辑器中
 * 3. 部署后获得 URL，如 https://dongming-model.your-name.workers.dev
 * 4. 在 config.json 的 model.urls 数组最前面添加该 URL
 *
 * 免费额度：100,000 请求/天，足够使用
 */

// 模型远程地址（通过 gh-proxy 镜像加速）
const MODEL_URL = "https://gh-proxy.com/https://github.com/panhanmo/dongming-school-3d/releases/download/V1.0.0/point_cloud_6999_.+.+.+.splat";

export default {
  async fetch(request) {
    // 处理 CORS 预检请求
    if (request.method === "OPTIONS") {
      return new Response(null, {
        headers: {
          "Access-Control-Allow-Origin": "*",
          "Access-Control-Allow-Methods": "GET, HEAD",
          "Access-Control-Allow-Headers": "*",
          "Access-Control-Max-Age": "86400",
        },
      });
    }

    // HEAD 请求：只返回文件大小
    if (request.method === "HEAD") {
      try {
        const headRes = await fetch(MODEL_URL, { method: "HEAD" });
        return new Response(null, {
          status: 200,
          headers: {
            "Access-Control-Allow-Origin": "*",
            "Content-Length": headRes.headers.get("Content-Length") || "0",
            "Accept-Ranges": "bytes",
            "Content-Type": "application/octet-stream",
          },
        });
      } catch (e) {
        return new Response("HEAD failed: " + e.message, { status: 502 });
      }
    }

    // GET 请求：流式转发模型文件
    try {
      const res = await fetch(MODEL_URL);
      if (!res.ok) {
        return new Response("Upstream error: " + res.status, {
          status: res.status,
          headers: { "Access-Control-Allow-Origin": "*" },
        });
      }

      // 流式转发，添加 CORS 头
      const headers = new Headers(res.headers);
      headers.set("Access-Control-Allow-Origin", "*");
      headers.set("Accept-Ranges", "bytes");
      headers.set("Content-Type", "application/octet-stream");

      return new Response(res.body, {
        status: 200,
        headers,
      });
    } catch (e) {
      return new Response("Proxy error: " + e.message, {
        status: 502,
        headers: { "Access-Control-Allow-Origin": "*" },
      });
    }
  },
};
