using FastMCP.Attributes;

public class HealthChecksDemoTools
{
    [McpTool("ping", Description = "Returns a simple pong response")]
    public string Ping() => "pong from HealthChecksDemo";
}
