# Charles Schwab Login Interface Demo
# Usage: powershell -File ShowSchwabLogin.ps1

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

Clear-Host

Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host " Charles Schwab OAuth Login Flow Demo" -ForegroundColor White
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host ""

Write-Host "When you start the OAuth authentication, your browser will open this page:" -ForegroundColor Yellow
Write-Host ""

# Login Page
Write-Host ("+".PadRight(79, '-') + "+") -ForegroundColor Gray
Write-Host ("| " + "Charles Schwab - Login".PadRight(77) + " |") -ForegroundColor White
Write-Host ("+".PadRight(79, '=') + "+") -ForegroundColor Gray
Write-Host "|                                                                               |"
Write-Host "|   [Schwab Logo]                                                               |" -ForegroundColor Cyan
Write-Host "|                                                                               |"
Write-Host "|   Login to Your Account                                                       |" -ForegroundColor White
Write-Host "|                                                                               |"
Write-Host "|   +-------------------------------------------------------------------+       |"
Write-Host "|   | Username or Account Number                                        |       |" -ForegroundColor Yellow
Write-Host "|   +-------------------------------------------------------------------+       |"
Write-Host "|                                                                               |"
Write-Host "|   +-------------------------------------------------------------------+       |"
Write-Host "|   | Password                                                          |       |" -ForegroundColor Yellow
Write-Host "|   +-------------------------------------------------------------------+       |"
Write-Host "|                                                                               |"
Write-Host "|   [ ] Remember Me                                                             |"
Write-Host "|                                                                               |"
Write-Host "|   +-------------------------------------------------------------------+       |"
Write-Host "|   |                         [  L O G I N  ]                           |       |" -ForegroundColor Green
Write-Host "|   +-------------------------------------------------------------------+       |"
Write-Host "|                                                                               |"
Write-Host "|   Forgot Password?  |  Need Help?                                            |" -ForegroundColor Cyan
Write-Host "|                                                                               |"
Write-Host ("+".PadRight(79, '-') + "+") -ForegroundColor Gray
Write-Host ""

Write-Host "After successful login, you will see the authorization page:" -ForegroundColor Yellow
Write-Host ""

# Authorization Page
Write-Host ("+".PadRight(79, '-') + "+") -ForegroundColor Gray
Write-Host ("| " + "Authorize Application".PadRight(77) + " |") -ForegroundColor White
Write-Host ("+".PadRight(79, '=') + "+") -ForegroundColor Gray
Write-Host "|                                                                               |"
Write-Host "|   Quant Trading System requests access to your account                        |" -ForegroundColor White
Write-Host "|                                                                               |"
Write-Host "|   This application will be able to:                                           |"
Write-Host "|   " -NoNewline
Write-Host "+" -ForegroundColor Green -NoNewline
Write-Host " View account information and balances                                      |"
Write-Host "|   " -NoNewline
Write-Host "+" -ForegroundColor Green -NoNewline
Write-Host " View positions and trading history                                         |"
Write-Host "|   " -NoNewline
Write-Host "+" -ForegroundColor Green -NoNewline
Write-Host " Get market data and quotes                                                 |"
Write-Host "|   " -NoNewline
Write-Host "+" -ForegroundColor Green -NoNewline
Write-Host " Execute trades on your behalf                                              |"
Write-Host "|                                                                               |"
Write-Host "|   +------------------+  +------------------+                                  |"
Write-Host "|   |   [  DENY  ]     |  |  [  AUTHORIZE  ] |                                  |" -NoNewline
Write-Host ""
Write-Host "|   " -NoNewline
Write-Host "+------------------+" -ForegroundColor Red -NoNewline
Write-Host "  " -NoNewline
Write-Host "+------------------+" -ForegroundColor Green -NoNewline
Write-Host "                                  |"
Write-Host "|                                                                               |"
Write-Host ("+".PadRight(79, '-') + "+") -ForegroundColor Gray
Write-Host ""

Write-Host "After authorization, the browser will redirect back to the application." -ForegroundColor Green
Write-Host ""

Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host ""
Write-Host "How to Start the Real OAuth Flow:" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. Get Schwab Developer Credentials" -ForegroundColor White
Write-Host "   Visit: " -NoNewline -ForegroundColor Gray
Write-Host "https://developer.schwab.com/" -ForegroundColor Cyan
Write-Host ""
Write-Host "2. Configure App Key" -ForegroundColor White
Write-Host "   dotnet user-secrets set `"Schwab:AppKey`" `"your-app-key`"" -ForegroundColor Gray
Write-Host ""
Write-Host "3. Run the Demo Program" -ForegroundColor White
Write-Host "   cd src/Quant.Infra.Net.Console" -ForegroundColor Gray
Write-Host "   dotnet run" -ForegroundColor Gray
Write-Host ""
Write-Host "4. Select Option 2 to start OAuth authentication" -ForegroundColor White
Write-Host ""
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host ""
Write-Host "Documentation:" -ForegroundColor Yellow
Write-Host "   - SCHWAB_DEVELOPER_REGISTRATION_GUIDE.md - Developer registration guide" -ForegroundColor Gray
Write-Host "   - SCHWAB_QUICKSTART.md - Quick start guide" -ForegroundColor Gray
Write-Host "   - SCHWAB_INTEGRATION_GUIDE.md - Complete integration documentation" -ForegroundColor Gray
Write-Host ""

Write-Host "Press any key to exit..." -ForegroundColor Yellow
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
