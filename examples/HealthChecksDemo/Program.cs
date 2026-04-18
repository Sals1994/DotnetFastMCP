using FastMCP.Health;
using FastMCP.Hosting;
using FastMCP.Server;
using System.Reflection;

var server = new FastMCPServer("HealthChecksDemo");
var builder = McpServerBuilder.Create(server, args);
builder.WithComponentsFrom(Assembly.GetExecutingAssembly());

// Simulate: one always-healthy check, one that degrades if memory is high,
// one custom async check (simulates a DB ping).
builder.WithHealthChecks(checks =>
{
    checks.AddCheck("memory", () =>
        GC.GetTotalMemory(false) < 1_000_000_000L); // < 1 GB

    checks.AddAsyncCheck("simulated_database", async ct =>
    {
        await Task.Delay(10, ct);  // simulate I/O
        return true;               // always healthy in the demo
    });

    checks.AddAsyncCheck("simulated_llm_provider", async ct =>
    {
        await Task.Delay(20, ct);
        return true;
    });
});

var app = builder.Build();

Console.Error.WriteLine("[HealthChecksDemo] Server starting...");
Console.Error.WriteLine("[HealthChecksDemo] Health endpoint → GET http://localhost:5000/mcp/health");

await app.RunMcpAsync(args);
