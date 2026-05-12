using Microsoft.AspNetCore.Authentication.Cookies;
using Quant.Infra.Net.Broker.Interfaces;
using Quant.Infra.Net.Broker.Model;
using Quant.Infra.Net.Broker.Service;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(5237);
    options.Listen(System.Net.IPAddress.Loopback, 443, listen =>
    {
        listen.UseHttps("schwab-dev.pfx", "schwab123");
    });
});

builder.Services.AddRazorPages();
builder.Services.AddHttpClient();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options => { options.LoginPath = "/"; });
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<ISchwabBrokerService>(sp =>
{
    var ctx = sp.GetRequiredService<IHttpContextAccessor>().HttpContext;
    var credentials = new BrokerCredentials
    {
        ApiKey  = ctx?.Session.GetString("AppKey")    ?? "",
        Secret  = ctx?.Session.GetString("AppSecret") ?? "",
        BaseUrl = "https://api.schwabapi.com/trader/v1"
    };
    var svc = new SchwabBrokerService(credentials, ctx?.Session.GetString("AccountNumber") ?? "");
    var token = ctx?.Session.GetString("AccessToken");
    if (!string.IsNullOrEmpty(token)) svc.SetAccessToken(token);
    return svc;
});

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

app.Run();
