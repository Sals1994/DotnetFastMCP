using FastMCP.Attributes;

public static class HealthChecksDemoTools
{
    [McpTool("ping", Description = "Returns a simple pong response")]
    public static string Ping() => "pong from HealthChecksDemo";
}
