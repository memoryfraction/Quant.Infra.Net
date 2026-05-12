# 以管理员权限启动 Schwab Web 应用
# 需要管理员权限来监听 https://127.0.0.1/oauth/callback (port 443)

param([switch]$Register)

# 检查是否管理员
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Host ""
    Write-Host "需要管理员权限来监听 HTTPS 443 端口" -ForegroundColor Yellow
    Write-Host "正在以管理员权限重新启动..." -ForegroundColor Cyan
    Write-Host ""
    Start-Process powershell -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`"" -Verb RunAs
    exit
}

Write-Host ""
Write-Host ("=" * 60) -ForegroundColor Cyan
Write-Host " Schwab Trading Web App" -ForegroundColor White
Write-Host ("=" * 60) -ForegroundColor Cyan
Write-Host ""

# 注册 URL（只需运行一次）
Write-Host "注册 HTTPS 回调地址..." -ForegroundColor Yellow
netsh http add urlacl url=https://127.0.0.1/oauth/callback/ user=Everyone 2>$null
Write-Host "OK" -ForegroundColor Green
Write-Host ""

# 启动应用
Write-Host "启动 Web 应用..." -ForegroundColor Yellow
Write-Host "浏览器访问: " -NoNewline
Write-Host "http://localhost:5237" -ForegroundColor Cyan
Write-Host ""

Set-Location "$PSScriptRoot\src\Quant.Infra.Net.Web"
dotnet run
