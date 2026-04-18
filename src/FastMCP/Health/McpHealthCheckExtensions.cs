// File: src/FastMCP/Health/McpHealthCheckExtensions.cs
using FastMCP.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace FastMCP.Health;

/// <summary>
/// Extension methods to add health checks to a <see cref="McpServerBuilder"/>.
/// </summary>
public static class McpHealthCheckExtensions
{
    /// <summary>
    /// Adds a built-in health check endpoint at <c>GET /mcp/health</c> (configurable).
    /// <para>
    /// Minimal setup — no configuration needed, works out of the box:
    /// <code>
    /// builder.WithHealthChecks();
    /// </code>
    /// </para>
    /// <para>
    /// With custom checks:
    /// <code>
    /// builder.WithHealthChecks(checks =>
    /// {
    ///     checks.AddAsyncCheck("database", ct => dbContext.Database.CanConnectAsync(ct));
    ///     checks.AddAsyncCheck("llm",      ct => llmProvider.IsHealthyAsync(ct));
    /// });
    /// </code>
    /// </para>
    /// </summary>
    /// <param name="builder">The <see cref="McpServerBuilder"/> to configure.</param>
    /// <param name="configure">Optional lambda to add custom checks or change the endpoint path.</param>
    public static McpServerBuilder WithHealthChecks(
        this McpServerBuilder builder,
        Action<McpHealthCheckOptions>? configure = null)
    {
        var options = new McpHealthCheckOptions();
        configure?.Invoke(options);

        // Register options as a singleton — the registry reads it at construction time.
        builder.GetWebAppBuilder().Services.AddSingleton(options);

        // Register the runtime engine — singleton because check state is stateless;
        // creating per-request would be wasteful.
        builder.GetWebAppBuilder().Services.AddSingleton<McpHealthCheckRegistry>();

        return builder;
    }
}