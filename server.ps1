# 简易 HTTP 服务器（HttpListener + 流式传输大文件）
$port = 8080
$root = $PSScriptRoot
$listener = New-Object System.Net.HttpListener
$listener.Prefixes.Add("http://localhost:$port/")
try {
    $listener.Start()
} catch {
    Write-Host "Failed to start: $_" -ForegroundColor Red
    exit 1
}

Write-Host "================================" -ForegroundColor Cyan
Write-Host "  服务器已启动" -ForegroundColor Green
Write-Host "  地址: http://localhost:$port" -ForegroundColor Green
Write-Host "  按 Ctrl+C 停止" -ForegroundColor Yellow
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

$mimes = @{
    '.html' = 'text/html; charset=utf-8'
    '.js'   = 'application/javascript; charset=utf-8'
    '.json' = 'application/json; charset=utf-8'
    '.css'  = 'text/css; charset=utf-8'
    '.png'  = 'image/png'
    '.jpg'  = 'image/jpeg'
    '.ico'  = 'image/x-icon'
    '.splat'= 'application/octet-stream'
    '.ply'  = 'application/octet-stream'
    '.svg'  = 'image/svg+xml'
}

while ($listener.IsListening) {
    $ctx = $listener.GetContext()
    $req = $ctx.Request
    $res = $ctx.Response

    $reqPath = $req.Url.AbsolutePath
    if ($reqPath -eq '/') { $reqPath = '/index.html' }
    $relPath = $reqPath.TrimStart('/')
    $filePath = Join-Path $root $relPath

    if (Test-Path -LiteralPath $filePath -PathType Leaf) {
        $ext = [System.IO.Path]::GetExtension($filePath)
        $mime = if ($mimes[$ext]) { $mimes[$ext] } else { 'application/octet-stream' }
        $fileLen = (Get-Item -LiteralPath $filePath).Length

        $res.ContentType = $mime
        $res.ContentLength64 = $fileLen
        $res.Headers.Add("Access-Control-Allow-Origin", "*")

        # 流式写入
        $fs = [System.IO.File]::OpenRead($filePath)
        $buffer = New-Object byte[] 131072
        while ($true) {
            $read = $fs.Read($buffer, 0, $buffer.Length)
            if ($read -le 0) { break }
            $res.OutputStream.Write($buffer, 0, $read)
        }
        $fs.Close()
        $res.OutputStream.Flush()
        Write-Host "200 $relPath $fileLen bytes" -ForegroundColor Green
    } else {
        $body = "404 Not Found: $relPath"
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($body)
        $res.ContentType = "text/plain"
        $res.ContentLength64 = $bytes.Length
        $res.OutputStream.Write($bytes, 0, $bytes.Length)
        Write-Host "404 $relPath" -ForegroundColor Red
    }
    $res.Close()
}
