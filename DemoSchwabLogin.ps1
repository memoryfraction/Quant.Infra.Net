# Schwab Login Demo - Auto run with demo App Key
# This script will automatically show the login preview

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

Write-Host ""
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host " Starting Schwab OAuth Demo..." -ForegroundColor White
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host ""

# Create a temporary demo app key for testing
$demoAppKey = "DEMO_APP_KEY_12345"

Write-Host "This demo will show you:" -ForegroundColor Yellow
Write-Host "  1. Login interface preview (text-based)" -ForegroundColor Gray
Write-Host "  2. How the OAuth flow works" -ForegroundColor Gray
Write-Host "  3. What happens after authorization" -ForegroundColor Gray
Write-Host ""
Write-Host "Press any key to start the demo..." -ForegroundColor Yellow
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

# Run the PowerShell demo
& ".\ShowSchwabLogin.ps1"

Write-Host ""
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host ""
Write-Host "To see the REAL browser login:" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. Get your Schwab App Key from: " -NoNewline -ForegroundColor White
Write-Host "https://developer.schwab.com/" -ForegroundColor Cyan
Write-Host ""
Write-Host "2. Set it in user secrets:" -ForegroundColor White
Write-Host "   dotnet user-secrets set `"Schwab:AppKey`" `"your-real-app-key`"" -ForegroundColor Gray
Write-Host ""
Write-Host "3. Run the Console program:" -ForegroundColor White
Write-Host "   cd src/Quant.Infra.Net.Console" -ForegroundColor Gray
Write-Host "   dotnet run" -ForegroundColor Gray
Write-Host "   Then select option 1 -> option 2" -ForegroundColor Gray
Write-Host ""
Write-Host "4. Your browser will automatically open the Schwab login page!" -ForegroundColor Green
Write-Host ""
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host ""
