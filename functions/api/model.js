// Cloudflare Pages Function: /api/model
// 部署后自动处理 /api/model 请求，流式转发 GitHub Release 模型并添加 CORS 头
//
// 部署方式：将整个仓库连接到 Cloudflare Pages，此文件会自动生效
// 访问地址：https://[项目名].pages.dev/api/model

const MODEL_URL = "https://gh-proxy.com/https://github.com/panhanmo/dongming-school-3d/releases/download/V1.0.0/point_cloud_6999_.+.+.+.splat";

const CORS_HEADERS = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Methods": "GET, HEAD, OPTIONS",
  "Access-Control-Allow-Headers": "*",
  "Access-Control-Max-Age": "86400",
};

// 处理 OPTIONS 预检请求
export async function onRequestOptions() {
  return new Response(null, { headers: CORS_HEADERS });
}

// 处理 HEAD 请求（只返回文件大小）
export async function onRequestHead() {
  try {
    const res = await fetch(MODEL_URL, { method: "HEAD" });
    return new Response(null, {
      status: 200,
      headers: {
        ...CORS_HEADERS,
        "Content-Length": res.headers.get("Content-Length") || "0",
        "Accept-Ranges": "bytes",
        "Content-Type": "application/octet-stream",
      },
    });
  } catch (e) {
    return new Response("HEAD failed: " + e.message, { status: 502, headers: CORS_HEADERS });
  }
}

// 处理 GET 请求：流式转发模型文件
export async function onRequestGet() {
  try {
    const res = await fetch(MODEL_URL);
    if (!res.ok) {
      return new Response("Upstream error: " + res.status, {
        status: res.status,
        headers: CORS_HEADERS,
      });
    }

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
      headers: CORS_HEADERS,
    });
  }
}
