// File: src/FastMCP/Health/McpHealthCheckRegistry.cs
using System.Diagnostics;
using FastMCP.Server;
using Microsoft.Extensions.Logging;

namespace FastMCP.Health;

/// <summary>
/// The runtime engine for health checks.
/// Registered as a Singleton in DI; runs all checks on each request to the health endpoint.
/// </summary>
public class McpHealthCheckRegistry
{
    private readonly McpHealthCheckOptions _options;
    private readonly FastMCPServer _server;
    private readonly ILogger<McpHealthCheckRegistry> _logger;
    private static readonly DateTimeOffset _startTime = DateTimeOffset.UtcNow;

    // Framework version resolved once at startup — cheap reflection, not per-request.
    private static readonly string _frameworkVersion =
        typeof(McpHealthCheckRegistry).Assembly.GetName().Version?.ToString() ?? "unknown";

    public McpHealthCheckRegistry(
        McpHealthCheckOptions options,
        FastMCPServer server,
        ILogger<McpHealthCheckRegistry> logger)
    {
        _options = options;
        _server = server;
        _logger = logger;
    }

    /// <summary>
    /// Runs all registered checks concurrently (with timeout) and returns the aggregated result.
    /// </summary>
    public async Task<McpHealthCheckResult> RunAllChecksAsync(CancellationToken cancellationToken = default)
    {
        var result = new McpHealthCheckResult
        {
            Timestamp = DateTimeOffset.UtcNow
        };

        // Always add the built-in "mcp_server" self-check as the first entry.
        var entries = new List<McpHealthCheckEntry>
        {
            new McpHealthCheckEntry
            {
                Name = "mcp_server",
                Status = "Healthy",
                Description = "MCP server process is running",
                DurationMs = 0
            }
        };

        // Run all consumer-registered checks concurrently.
        if (_options.Checks.Count > 0)
        {
            var checkTasks = _options.Checks.Select(check => RunSingleCheckAsync(check, cancellationToken));
            var checkResults = await Task.WhenAll(checkTasks);
            entries.AddRange(checkResults);
        }

        // Compute overall status: worst entry wins.
        // Priority: Unhealthy > Degraded > Healthy
        if (entries.Any(e => e.Status == "Unhealthy"))
            result.Status = "Unhealthy";
        else if (entries.Any(e => e.Status == "Degraded"))
            result.Status = "Degraded";
        else
            result.Status = "Healthy";

        result.Checks = entries;

        // Attach framework diagnostics if configured.
        if (_options.IncludeServerDiagnostics)
        {
            result.Diagnostics = new McpServerDiagnostics
            {
                ServerName       = _server.Name,
                FrameworkVersion = _frameworkVersion,
                ToolCount        = _server.Tools.Count,
                ResourceCount    = _server.Resources.Count,
                PromptCount      = _server.Prompts.Count,
                UptimeSeconds    = (DateTimeOffset.UtcNow - _startTime).TotalSeconds
            };
        }

        return result;
    }

    private async Task<McpHealthCheckEntry> RunSingleCheckAsync(
        NamedHealthCheck namedCheck,
        CancellationToken parentToken)
    {
        var sw = Stopwatch.StartNew();

        // Wrap the parent token with the per-check timeout.
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(parentToken);
        cts.CancelAfter(TimeSpan.FromMilliseconds(_options.MaxResponseTimeMs));

        try
        {
            var healthy = await namedCheck.Check(cts.Token);
            sw.Stop();

            return new McpHealthCheckEntry
            {
                Name      = namedCheck.Name,
                Status    = healthy ? "Healthy" : "Unhealthy",
                DurationMs = sw.Elapsed.TotalMilliseconds
            };
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            sw.Stop();
            _logger.LogWarning("Health check '{CheckName}' timed out after {TimeoutMs}ms",
                namedCheck.Name, _options.MaxResponseTimeMs);

            return new McpHealthCheckEntry
            {
                Name        = namedCheck.Name,
                Status      = "Degraded",
                Description = $"Check timed out after {_options.MaxResponseTimeMs}ms",
                DurationMs  = sw.Elapsed.TotalMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Health check '{CheckName}' threw an exception", namedCheck.Name);

            return new McpHealthCheckEntry
            {
                Name        = namedCheck.Name,
                Status      = "Unhealthy",
                Description = ex.Message,
                DurationMs  = sw.Elapsed.TotalMilliseconds
            };
        }
    }
}