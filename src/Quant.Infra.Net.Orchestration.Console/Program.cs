using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Quant.Infra.Net.Analysis.Service;
using Quant.Infra.Net.Orchestration;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.Orchestration.Pipeline;
using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.Shared.Service;
using Quant.Infra.Net.SourceData.Service;
using Quant.Infra.Net.Orchestration.Console;

var builder = Host.CreateApplicationBuilder(args);

// 内容根锁定到可执行件目录，保证 appsettings.json 总能被读取（不受启动目录影响）
// Pin the content root to the executable directory so appsettings.json is always found (CWD-independent).
builder.Environment.ContentRootPath = AppContext.BaseDirectory;

// 无论宿主默认是否加载 appsettings.json，都显式加入可执行件目录中的配置文件
// Always load appsettings.json from the executable directory, regardless of host defaults.
builder.Configuration.AddJsonFile(System.IO.Path.Combine(AppContext.BaseDirectory, "appsettings.json"));

builder.Services.Configure<OrchestrationOptions>(builder.Configuration.GetSection("Orchestration"));
builder.Services.AddSingleton<IAnalysisService, AnalysisService>();
builder.Services.AddSingleton<ITraditionalFinanceSourceDataService, DemoTraditionalFinanceSourceDataService>();
builder.Services.AddQuantInfraNetOrchestration();
builder.Services.AddSingleton<IntervalTrigger>(_ => new IntervalTrigger(StartMode.NextMinute, TimeSpan.Zero));

var host = builder.Build();
var runner = host.Services.GetRequiredService<PipelineRunner>();
runner.RunCompleted += ctx =>
{
    System.Console.WriteLine($"--- Orchestration cycle runId={ctx.RunId} strategy={ctx.GetParameter("Strategy") ?? "-"} ---");
    foreach (var evt in ctx.Events)
    {
        System.Console.WriteLine($"{evt.TimestampUtc:HH:mm:ss} INF [{evt.Stage}] {evt.Message}");
    }
    System.Console.WriteLine($"--- cycle complete: events={ctx.Events.Count} errors={ctx.Errors.Count} ---");
};
var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
runner.RunCompleted += _ => lifetime.StopApplication(); // 完成一整轮后退出宿主（演示）/ exit the host after one full cycle (demo)

await host.RunAsync();
