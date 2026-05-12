# Charles Schwab 登录界面演示脚本
# 运行方式: powershell -File ShowSchwabLoginDemo.ps1

Clear-Host

Write-Host "╔" -NoNewline
Write-Host ("═" * 78) -NoNewline
Write-Host "╗"
Write-Host "║ Charles Schwab 登录界面预览" -NoNewline
Write-Host (" " * 50) -NoNewline
Write-Host "║"
Write-Host "╚" -NoNewline
Write-Host ("═" * 78) -NoNewline
Write-Host "╝"
Write-Host ""

Write-Host "当您启动 OAuth 认证流程时，浏览器会打开以下页面：" -ForegroundColor Cyan
Write-Host ""

Write-Host "┌" -NoNewline
Write-Host ("─" * 78) -NoNewline
Write-Host "┐"
Write-Host "│ " -NoNewline
Write-Host "🔐 Charles Schwab - 登录" -ForegroundColor Green -NoNewline
Write-Host (" " * 53) -NoNewline
Write-Host "│"
Write-Host "├" -NoNewline
Write-Host ("─" * 78) -NoNewline
Write-Host "┤"
Write-Host "│" -NoNewline
Write-Host (" " * 78) -NoNewline
Write-Host "│"
Write-Host "│   [Schwab Logo]" -NoNewline
Write-Host (" " * 62) -NoNewline
Write-Host "│"
Write-Host "│" -NoNewline
Write-Host (" " * 78) -NoNewline
Write-Host "│"
Write-Host "│   登录您的账户" -NoNewline
Write-Host (" " * 64) -NoNewline
Write-Host "│"
Write-Host "│" -NoNewline
Write-Host (" " * 78) -NoNewline
Write-Host "│"
Write-Host "│   ┌────────────────────────────────────────────────────────┐" -NoNewline
Write-Host (" " * 15) -NoNewline
Write-Host "│"
Write-Host "│   │ " -NoNewline
Write-Host "用户名或账户号码" -ForegroundColor Yellow -NoNewline
Write-Host (" " * 40) -NoNewline
Write-Host "│" -NoNewline
Write-Host (" " * 15) -NoNewline
Write-Host "│"
Write-Host "│   └────────────────────────────────────────────────────────┘" -NoNewline
Write-Host (" " * 15) -NoNewline
Write-Host "│"
Write-Host "│" -NoNewline
Write-Host (" " * 78) -NoNewline
Write-Host "│"
Write-Host "│   ┌────────────────────────────────────────────────────────┐" -NoNewline
Write-Host (" " * 15) -NoNewline
Write-Host "│"
Write-Host "│   │ " -NoNewline
Write-Host "密码" -ForegroundColor Yellow -NoNewline
Write-Host (" " * 52) -NoNewline
Write-Host "│" -NoNewline
Write-Host (" " * 15) -NoNewline
Write-Host "│"
Write-Host "│   └────────────────────────────────────────────────────────┘" -NoNewline
Write-Host (" " * 15) -NoNewline
Write-Host "│"
Write-Host "│" -NoNewline
Write-Host (" " * 78) -NoNewline
Write-Host "│"
Write-Host "│   [ ] 记住我" -NoNewline
Write-Host (" " * 66) -NoNewline
Write-Host "│"
Write-Host "│" -NoNewline
Write-Host (" " * 78) -NoNewline
Write-Host "│"
Write-Host "│   ┌────────────────────────────────────────────────────────┐" -NoNewline
Write-Host (" " * 15) -NoNewline
Write-Host "│"
Write-Host "│   │              " -NoNewline
Write-Host "[  登  录  ]" -ForegroundColor Green -NoNewline
Write-Host (" " * 31) -NoNewline
Write-Host "│" -NoNewline
Write-Host (" " * 15) -NoNewline
Write-Host "│"
Write-Host "│   └────────────────────────────────────────────────────────┘" -NoNewline
Write-Host (" " * 15) -NoNewline
Write-Host "│"
Write-Host "│" -NoNewline
Write-Host (" " * 78) -NoNewline
Write-Host "│"
Write-Host "│   " -NoNewline
Write-Host "忘记密码？" -ForegroundColor Cyan -NoNewline
Write-Host "  |  " -NoNewline
Write-Host "需要帮助？" -ForegroundColor Cyan -NoNewline
Write-Host (" " * 52) -NoNewline
Write-Host "│"
Write-Host "│" -NoNewline
Write-Host (" " * 78) -NoNewline
Write-Host "│"
Write-Host "└" -NoNewline
Write-Host ("─" * 78) -NoNewline
Write-Host "┘"
Write-Host ""

Write-Host "登录后，您会看到授权页面：" -ForegroundColor Cyan
Write-Host ""

Write-Host "┌" -NoNewline
Write-Host ("─" * 78) -NoNewline
Write-Host "┐"
Write-Host "│ " -NoNewline
Write-Host "🔓 授权应用访问" -ForegroundColor Green -NoNewline
Write-Host (" " * 63) -NoNewline
Write-Host "│"
Write-Host "├" -NoNewline
Write-Host ("─" * 78) -NoNewline
Write-Host "┤"
Write-Host "│" -NoNewline
Write-Host (" " * 78) -NoNewline
Write-Host "│"
Write-Host "│   Quant Trading System 请求访问您的账户" -NoNewline
Write-Host (" " * 39) -NoNewline
Write-Host "│"
Write-Host "│" -NoNewline
Write-Host (" " * 78) -NoNewline
Write-Host "│"
Write-Host "│   该应用将能够：" -NoNewline
Write-Host (" " * 62) -NoNewline
Write-Host "│"
Write-Host "│   " -NoNewline
Write-Host "✓" -ForegroundColor Green -NoNewline
Write-Host " 查看账户信息和余额" -NoNewline
Write-Host (" " * 57) -NoNewline
Write-Host "│"
Write-Host "│   " -NoNewline
Write-Host "✓" -ForegroundColor Green -NoNewline
Write-Host " 查看持仓和交易历史" -NoNewline
Write-Host (" " * 57) -NoNewline
Write-Host "│"
Write-Host "│   " -NoNewline
Write-Host "✓" -ForegroundColor Green -NoNewline
Write-Host " 获取市场数据和报价" -NoNewline
Write-Host (" " * 57) -NoNewline
Write-Host "│"
Write-Host "│   " -NoNewline
Write-Host "✓" -ForegroundColor Green -NoNewline
Write-Host " 代表您执行交易" -NoNewline
Write-Host (" " * 61) -NoNewline
Write-Host "│"
Write-Host "│" -NoNewline
Write-Host (" " * 78) -NoNewline
Write-Host "│"
Write-Host "│   ┌──────────────────┐  ┌──────────────────┐" -NoNewline
Write-Host (" " * 31) -NoNewline
Write-Host "│"
Write-Host "│   │   " -NoNewline
Write-Host "[  拒绝  ]" -ForegroundColor Red -NoNewline
Write-Host "     │  │   " -NoNewline
Write-Host "[  授权  ]" -ForegroundColor Green -NoNewline
Write-Host "     │" -NoNewline
Write-Host (" " * 31) -NoNewline
Write-Host "│"
Write-Host "│   └──────────────────┘  └──────────────────┘" -NoNewline
Write-Host (" " * 31) -NoNewline
Write-Host "│"
Write-Host "│" -NoNewline
Write-Host (" " * 78) -NoNewline
Write-Host "│"
Write-Host "└" -NoNewline
Write-Host ("─" * 78) -NoNewline
Write-Host "┘"
Write-Host ""

Write-Host "授权成功后，浏览器会自动跳转回应用程序。" -ForegroundColor Green
Write-Host ""

Write-Host "═" * 80 -ForegroundColor Cyan
Write-Host ""
Write-Host "📋 如何启动真实的 OAuth 流程：" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. 获取 Schwab 开发者凭据" -ForegroundColor White
Write-Host "   访问: " -NoNewline -ForegroundColor Gray
Write-Host "https://developer.schwab.com/" -ForegroundColor Cyan
Write-Host ""
Write-Host "2. 配置 App Key" -ForegroundColor White
Write-Host "   dotnet user-secrets set `"Schwab:AppKey`" `"your-app-key`"" -ForegroundColor Gray
Write-Host ""
Write-Host "3. 运行演示程序" -ForegroundColor White
Write-Host "   dotnet run --project src/Quant.Infra.Net.Console" -ForegroundColor Gray
Write-Host ""
Write-Host "4. 选择菜单选项 2 启动 OAuth 认证" -ForegroundColor White
Write-Host ""
Write-Host "═" * 80 -ForegroundColor Cyan
Write-Host ""
Write-Host "📚 更多信息：" -ForegroundColor Yellow
Write-Host "   - SCHWAB_DEVELOPER_REGISTRATION_GUIDE.md - 开发者注册指南" -ForegroundColor Gray
Write-Host "   - SCHWAB_QUICKSTART.md - 快速开始指南" -ForegroundColor Gray
Write-Host "   - SCHWAB_INTEGRATION_GUIDE.md - 完整集成文档" -ForegroundColor Gray
Write-Host ""

Write-Host "按任意键退出..." -ForegroundColor Yellow
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
