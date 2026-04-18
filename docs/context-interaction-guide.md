# Context & Interaction Guide

DotnetFastMCP provides a powerful **Context System** that allows your tools to interact with the client purely through the MCP protocol, without worrying about the underlying transport (Stdio, HTTP, SSE).

## Key Features
*   **Logging**: Send logs (Info, Warning, Error, Debug) to the client.
*   **Progress**: Report progress for long-running operations.
*   **Request Cancellation**: Handle client cancellation requests gracefully.
*   **Access Request ID**: Retrieve the unique JSON-RPC request ID.

## How It Works
Simply add a parameter of type `McpContext` to your tool method. The framework will automatically inject the context for the current request.

### Example Tool
```csharp
[McpTool("process_data")]
public async Task<string> ProcessData(string input, McpContext context)
{
    // 1. Logging
    await context.LogInfoAsync($"Starting processing for: {input}");

    // 2. Progress Reporting
    await context.ReportProgressAsync(0, 100);
    
    for (int i = 0; i <= 10; i++)
    {
        // Check for cancellation
        context.CancellationToken.ThrowIfCancellationRequested();

        await Task.Delay(100); // Simulate work
        await context.ReportProgressAsync(i * 10, 100);
    }

    await context.LogInfoAsync("Processing complete!");
    return $"Processed: {input}";
}
```

## API Reference

### `McpContext` Class

| Method / Property | Description |
| :--- | :--- |
| `LogInfoAsync(message)` | Sends an INFO log to the client. |
| `LogWarningAsync(message)` | Sends a WARNING log to the client. |
| `LogErrorAsync(message)` | Sends an ERROR log to the client. |
| `LogDebugAsync(message)` | Sends a DEBUG log to the client. |
| `ReportProgressAsync(current, total)` | Reports progress (e.g., 50/100). |
| `CancellationToken` | Use this to observe client cancellation requests. |
| `RequestId` | The JSON-RPC ID of the current request. |

## Transport Support

*   **Stdio**: Fully supported. Notifications are sent to `stdout`.
*   **HTTP**: Supported via Server Logs. Since HTTP is request/response, notifications are logged to the server's console/logger to ensure visibility, as they cannot be pushed to the client during the request.
*   **SSE**: (Future) Will support real-time push notifications.
