// File: src/FastMCP/Health/McpHealthCheckOptions.cs
namespace FastMCP.Health;

/// <summary>
/// Configuration options for the built-in MCP health check endpoint.
/// Follows the same Options-pattern as <see cref="FastMCP.Telemetry.McpTelemetryOptions"/>.
/// </summary>
public class McpHealthCheckOptions
{
    /// <summary>
    /// HTTP path at which the health endpoint is exposed.
    /// Default: "/mcp/health"
    /// Consumers almost never need to change this — it matches the Kubernetes probe convention.
    /// </summary>
    public string HealthEndpointPath { get; set; } = "/mcp/health";

    /// <summary>
    /// Maximum time in milliseconds that a single health check is allowed to run.
    /// A check that exceeds this limit is reported as Degraded rather than hanging forever.
    /// Default: 5000 ms (5 seconds).
    /// </summary>
    public int MaxResponseTimeMs { get; set; } = 5_000;

    /// <summary>
    /// When true (default), the response includes automatic server diagnostics:
    /// tool count, uptime, and framework version.
    /// Set to false in highly security-sensitive environments where metadata must not leak.
    /// </summary>
    public bool IncludeServerDiagnostics { get; set; } = true;

    /// <summary>
    /// The list of named health checks registered by the consumer.
    /// Populated via <see cref="AddCheck"/> and <see cref="AddAsyncCheck"/> fluent methods.
    /// </summary>
    internal List<NamedHealthCheck> Checks { get; } = new();

    /// <summary>
    /// Registers a synchronous health check.
    /// The lambda returns true for healthy, false for unhealthy.
    /// </summary>
    /// <param name="name">Unique name shown in the JSON response (e.g. "database", "cache").</param>
    /// <param name="check">Function that returns true = healthy, false = unhealthy.</param>
    public McpHealthCheckOptions AddCheck(string name, Func<bool> check)
    {
        Checks.Add(new NamedHealthCheck(name, _ => Task.FromResult(check())));
        return this;
    }

    /// <summary>
    /// Registers an async health check.
    /// Use this for I/O-bound checks (database pings, HTTP calls, etc.).
    /// </summary>
    /// <param name="name">Unique name shown in the JSON response.</param>
    /// <param name="check">Async function returning true = healthy, false = unhealthy.</param>
    public McpHealthCheckOptions AddAsyncCheck(string name, Func<CancellationToken, Task<bool>> check)
    {
        Checks.Add(new NamedHealthCheck(name, check));
        return this;
    }
}

/// <summary>
/// Internal holder for a named async health check.
/// Kept internal so consumers only interact with the fluent API on McpHealthCheckOptions.
/// </summary>
internal record NamedHealthCheck(string Name, Func<CancellationToken, Task<bool>> Check);
