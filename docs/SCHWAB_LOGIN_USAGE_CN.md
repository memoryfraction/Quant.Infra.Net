# Schwab 登录和授权使用说明

这份说明用于本项目里的 Schwab Web 登录页面。真实登录入口是本地 Web 服务：

```text
https://127.0.0.1/
```

## 1. 准备 Schwab Developer 凭据

先登录 Schwab Developer Portal，确认你的 App 已经创建并可以使用 Trader API。

需要准备三项信息：

```text
Client ID / App Key
Client Secret
Schwab 账户号码
```

`Schwab 账户号码` 是你的嘉信证券交易账户号，不是登录用户名。可以填写完整账号，也可以填写末尾几位；程序会通过 Schwab 返回的 account hash 自动匹配。

## 2. 配置 Callback URL

在 Schwab Developer Portal 里，把 App 的 Callback URL 配成：

```text
https://127.0.0.1
```

注意必须完全一致，包括：

```text
https
127.0.0.1
没有末尾路径
```

如果 Schwab 后台配置的是 `https://127.0.0.1/`，一般也可以，但建议和项目里的 `RedirectUri` 保持一致：`https://127.0.0.1`。

## 3. 启动本地 Web 服务

在项目根目录运行：

```powershell
dotnet run --project src/Quant.Infra.Net.Tests/Quant.Infra.Net.Web/Quant.Infra.Net.Web.csproj
```

启动后浏览器打开：

```text
https://127.0.0.1/
```

如果浏览器提示证书不安全，选择继续访问本地站点即可。

## 4. 登录授权流程

在登录页填写：

```text
Client ID / App Key
Client Secret
Schwab 账户号码
```

然后点击：

```text
打开 Schwab 授权页面
```

浏览器会跳转到 Schwab 官方登录页面。完成登录并点击 Allow 后，Schwab 会跳回：

```text
https://127.0.0.1/?code=...
```

项目会自动用这个 `code` 换取 access token，然后进入 Dashboard。

## 5. Dashboard 功能

登录成功后可以查看：

```text
Account：账户资产、现金、购买力
Positions：持仓列表
Quotes：实时行情
Options：期权链
Orders：最近订单历史
```

`Quotes` 和 `Options` 查询后会停留在当前页签，不会自动切回 Account。

## 6. 常见问题

### 点击按钮没有跳到 Schwab

请确认你打开的是：

```text
https://127.0.0.1/
```

不要直接打开静态 HTML 文件。真实 OAuth 登录只通过本地 Web 服务完成。

### 授权码换 Token 失败

重点检查：

```text
Client ID 是否正确
Client Secret 是否正确
Callback URL 是否是 https://127.0.0.1
授权码是否已经使用过
是否从同一个浏览器会话完成授权
```

Schwab 的授权码通常只能使用一次。失败后建议重新从首页点击授权按钮，不要重复使用旧的 `code`。

### 看不到账户或持仓

重点检查：

```text
填写的是 Schwab 交易账户号，不是登录用户名
当前 Schwab App 是否有 Trader API 权限
授权时是否允许访问账户
access token 是否已过期
```

如果只有一个授权账户，程序会自动使用该账户的 hash；如果有多个账户，建议填写完整账户号。

### 实时报价或期权链失败

行情和期权链使用 Schwab Market Data API。请确认：

```text
App 有 market data 权限
股票代码正确，例如 AAPL、MSFT、SPY
期权链标的有可交易期权
```

## 7. 重新登录

如果页面状态不对，点击 Dashboard 右上角 Logout，然后重新从：

```text
https://127.0.0.1/
```

开始授权。
