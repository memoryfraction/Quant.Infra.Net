# Start Schwab Web Application
# 启动 Schwab Web 应用

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

Write-Host ""
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host " Schwab Trading Web Application" -ForegroundColor White
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host ""

Write-Host "正在启动 Web 应用..." -ForegroundColor Yellow
Write-Host ""

# Check if user secrets are configured
Write-Host "提示: 如果你还没有配置 Schwab 凭据，请运行:" -ForegroundColor Yellow
Write-Host "  cd src/Quant.Infra.Net.Web" -ForegroundColor Gray
Write-Host "  dotnet user-secrets set `"Schwab:AppKey`" `"your-app-key`"" -ForegroundColor Gray
Write-Host "  dotnet user-secrets set `"Schwab:Secret`" `"your-app-secret`"" -ForegroundColor Gray
Write-Host ""

Write-Host "启动中..." -ForegroundColor Green
Write-Host ""

# Start the web application
Set-Location src/Quant.Infra.Net.Web
dotnet run

Write-Host ""
Write-Host "Web 应用已停止" -ForegroundColor Yellow
