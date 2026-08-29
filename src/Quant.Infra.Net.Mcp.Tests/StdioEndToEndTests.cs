using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Quant.Infra.Net.Mcp.Tests;

/// <summary>
/// P6 端到端：用真实子进程 + stdin/stdout 走一遍 MCP JSON-RPC（initialize → initialized → tools/list），
/// 证明一个真实的 MCP 客户端（如 Claude Desktop）能连上这个 stdio server 并拿到工具目录。
/// P6 end-to-end: drive a real child process over stdin/stdout with MCP JSON-RPC
/// (initialize → notifications/initialized → tools/list) to prove a real MCP client can connect
/// and list tools.
/// </summary>
[TestClass]
public sealed class StdioEndToEndTests
{
    private static string LocateServerDll()
    {
        // 测试程序集引用了 MCP 程序集，所以 MCP dll 就在测试输出目录里。
        var testDir = AppContext.BaseDirectory;
        var direct = Path.Combine(testDir, "Quant.Infra.Net.Mcp.dll");
        if (File.Exists(direct)) return direct;

        // Fallback: walk up to find the MCP project bin dir.
        var root = new DirectoryInfo(testDir);
        for (int i = 0; i < 6 && root is not null; i++)
        {
            var candidate = Path.Combine(root.FullName, "Quant.Infra.Net.Mcp", "bin", "Release", "net8.0", "Quant.Infra.Net.Mcp.dll");
            if (File.Exists(candidate)) return candidate;
            root = root.Parent;
        }
        throw new FileNotFoundException("Quant.Infra.Net.Mcp.dll not found.");
    }

    [TestMethod]
    [Timeout(30000)]
    public async Task StdioHandshake_RealProcess_ReturnsAllFourTools()
    {
        var dll = LocateServerDll();
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{dll}\"",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(dll)!,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("failed to start server");

        // 1) initialize
        await WriteJsonAsync(process.StandardInput,
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"codex-e2e","version":"1.0"}}}""");
        var initResp = await ReadJsonLineAsync(process.StandardOutput, "\"id\":1", 15000);
        Assert.IsNotNull(initResp, "no initialize response");
        var initDoc = JsonDocument.Parse(initResp);
        Assert.IsTrue(initDoc.RootElement.TryGetProperty("result", out var ir), "initialize should return a result");
        Assert.AreEqual("2025-03-26", ir.GetProperty("protocolVersion").GetString());
        Assert.AreEqual("Quant.Infra.Net.Mcp", ir.GetProperty("serverInfo").GetProperty("name").GetString());

        // 2) notifications/initialized (no response expected)
        await WriteJsonAsync(process.StandardInput, """{"jsonrpc":"2.0","method":"notifications/initialized"}""");

        // 3) tools/list
        await WriteJsonAsync(process.StandardInput, """{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}""");
        var toolsResp = await ReadJsonLineAsync(process.StandardOutput, "\"id\":2", 15000);
        Assert.IsNotNull(toolsResp, "no tools/list response");
        var toolsDoc = JsonDocument.Parse(toolsResp);
        var tools = toolsDoc.RootElement.GetProperty("result").GetProperty("tools");
        var names = new HashSet<string>();
        foreach (var t in tools.EnumerateArray())
        {
            names.Add(t.GetProperty("name").GetString()!);
        }

        Assert.IsTrue(names.Contains("list_strategies"), "list_strategies must be listed");
        Assert.IsTrue(names.Contains("run_backtest"), "run_backtest must be listed");
        Assert.IsTrue(names.Contains("run_paper_cycle"), "run_paper_cycle must be listed");
        Assert.IsTrue(names.Contains("fetch_ohlcv"), "fetch_ohlcv must be listed");
    }

    private static async Task WriteJsonAsync(StreamWriter w, string json)
    {
        await w.WriteLineAsync(json);
        await w.FlushAsync();
    }

    private static async Task<string?> ReadJsonLineAsync(StreamReader r, string containsToken, int timeoutMs = 15000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (r.EndOfStream) return null;
            var line = await r.ReadLineAsync();
            if (line is null) return null;
            if (line.Contains(containsToken)) return line;
        }
        return null;
    }
}
