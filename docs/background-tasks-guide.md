# Background Tasks Guide

## Overview

**Background Tasks** (v1.9.0+) allow MCP tools to start long-running operations—like processing files, generating large reports, or extended computations—without blocking the client.

This "Fire-and-Forget" pattern enables tools to return an immediate acknowledgement ("Job Started") while the actual work continues safely in the background.

## Key Features

*   **Non-Blocking Support**: Return responses immediately while work continues.
*   **Thread-Safe Queue**: Uses high-performance `System.Threading.Channels`.
*   **Automatic Lifecycle Management**: Background services are automatically managed by the host.
*   **Easy Integration**: Use `McpContext.RunInBackground(...)` directly in your tools.

## Usage

### 1. Basic Example

Inject `McpContext` into your tool. Use `RunInBackground` to queue work.

```csharp
[McpTool]
public static async Task<string> ProcessFile(string fileName, McpContext context)
{
    // Queue the work
    await context.RunInBackground(async (ct) => 
    {
        // This runs in the background!
        await LongRunningFileProcessing(fileName, ct);
        Console.WriteLine($"Finished processing {fileName}");
    });

    // Return immediately to the client
    return $"Processing started for {fileName}";
}
```

### 2. Handling Long-Running Operations

If your task takes a while, ensure you respect the `CancellationToken` provided in the delegate.

```csharp
await context.RunInBackground(async (ct) => 
{
    for (int i = 0; i < 100; i++)
    {
        if (ct.IsCancellationRequested) break;
        await Task.Delay(1000, ct); // Do work
    }
});
```

### 3. Error Handling

Background tasks catch exceptions to prevent crashing the server. Errors are logged to the console/logger automatically.

```csharp
await context.RunInBackground(async (ct) => 
{
    throw new Exception("This will be logged but won't crash the server.");
});
```

## Internal Architecture

The feature uses a **Hosted Service** pattern:
1.  **`IBackgroundTaskQueue`**: A Singleton queue service.
2.  **`McpBackgroundService`**: A hosted service that runs loop to process queued items sequentially.

## Validation & Testing

To test background capabilities manually:

1.  **Run your server** with `--stdio` or SSE.
2.  **Call a tool** that uses `RunInBackground` (e.g., one that sleeps for 5 seconds).
3.  **Verify Immediate Response**: The client should receive a result instantly.
4.  **Verify Logs**: Check the server logs (stderr) to see the background task completing later.
