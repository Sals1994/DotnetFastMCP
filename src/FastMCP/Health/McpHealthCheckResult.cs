// File: src/FastMCP/Health/McpHealthCheckResult.cs
using System.Text.Json.Serialization;

namespace FastMCP.Health;

/// <summary>
/// The JSON payload returned by GET /mcp/health.
/// Follows the ASP.NET Core HealthChecks response format so it is
/// compatible with standard health-check UIs and monitoring tools.
/// </summary>
public class McpHealthCheckResult
{
    /// <summary>
    /// Overall status: "Healthy", "Degraded", or "Unhealthy".
    /// Degraded = server is running but at least one check is failing.
    /// Unhealthy = server health is fully compromised.
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "Healthy";

    /// <summary>
    /// UTC timestamp of when this health report was generated.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Results for each named check registered by the consumer (plus the built-in server check).
    /// </summary>
    [JsonPropertyName("checks")]
    public List<McpHealthCheckEntry> Checks { get; set; } = new();

    /// <summary>
    /// Automatic server diagnostics — only present when IncludeServerDiagnostics = true.
    /// </summary>
    [JsonPropertyName("diagnostics")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public McpServerDiagnostics? Diagnostics { get; set; }
}

/// <summary>Individual check result inside the response.</summary>
public class McpHealthCheckEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>"Healthy", "Degraded", or "Unhealthy"</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "Healthy";

    /// <summary>Human-readable description of the check result (e.g. error message on failure).</summary>
    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    /// <summary>How long the check took. Useful for detecting slow dependencies.</summary>
    [JsonPropertyName("durationMs")]
    public double DurationMs { get; set; }
}

/// <summary>
/// Framework-level diagnostic data included automatically when IncludeServerDiagnostics = true.
/// This allows Kubernetes operators to verify which version of the MCP server is running
/// without needing a separate info endpoint.
/// </summary>
public class McpServerDiagnostics
{
    [JsonPropertyName("serverName")]
    public string ServerName { get; set; } = string.Empty;

    [JsonPropertyName("frameworkVersion")]
    public string FrameworkVersion { get; set; } = string.Empty;

    [JsonPropertyName("toolCount")]
    public int ToolCount { get; set; }

    [JsonPropertyName("resourceCount")]
    public int ResourceCount { get; set; }

    [JsonPropertyName("promptCount")]
    public int PromptCount { get; set; }

    /// <summary>How long the process has been running.</summary>
    [JsonPropertyName("uptimeSeconds")]
    public double UptimeSeconds { get; set; }
}
