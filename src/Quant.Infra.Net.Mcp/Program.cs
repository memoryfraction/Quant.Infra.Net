namespace Quant.Infra.Net.Mcp;

/// <summary>
/// 可执行宿主入口（转发到 <see cref="QuantInfraNetMcpServer.MainAsync"/>）。
/// Executable host entry point (forwards to <see cref="QuantInfraNetMcpServer.MainAsync"/>).
/// </summary>
public static class Program
{
    /// <summary>进程入口 / Process entry point.</summary>
    public static Task<int> Main() => QuantInfraNetMcpServer.MainAsync();
}
