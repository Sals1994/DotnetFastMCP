# Middleware Interception User Guide

Middleware in **DotnetFastMCP** provides a powerful way to intercept and modify the request/response pipeline of your MCP server. This allows you to implement cross-cutting concerns such as logging, validation, error handling, and performance monitoring without cluttering your core business logic.

## 🎯 What is Middleware?

Middleware is a component that sits between the incoming JSON-RPC request and the final tool handler. It forms a pipeline where each middleware can:

1.  Inspect the **Incoming Request**.
2.  Pass control to the **Next** middleware in the chain.
3.  Inspect the **Outgoing Response** (or Error).
4.  Short-circuit the pipeline (e.g., return a cached response or an error).

## 🚀 Creating Middleware

To create middleware, implement the `IMcpMiddleware` interface.

```csharp
using FastMCP.Hosting;
using FastMCP.Protocol;

public class MyCustomMiddleware : IMcpMiddleware
{
    public async Task<JsonRpcResponse> InvokeAsync(McpMiddlewareContext context, McpMiddlewareDelegate next, CancellationToken ct)
    {
        // 1. Logic BEFORE the handler
        Console.WriteLine($"[Middleware] Processing: {context.Request.Method}");

        // 2. Call the next delegate in the pipeline
        var response = await next(context, ct);

        // 3. Logic AFTER the handler
        if (response.Error != null)
        {
            Console.WriteLine($"[Middleware] Request Failed: {response.Error.Message}");
        }

        // 4. Return result
        return response;
    }
}
```

### The `McpMiddlewareContext`
The context provides access to everything you need:
*   `Request`: The raw `JsonRpcRequest` (Method, Params, Id).
*   `Server`: The `FastMCPServer` instance (access registered tools/resources).
*   `User`: The `ClaimsPrincipal` (if authenticated).
*   `Session`: The current `IMcpSession` (transport agnostic).

## 🔧 Registration

Register your middleware in `Program.cs` using the `McpServerBuilder`. Middleware is executed in the order it is registered.

```csharp
var builder = McpServerBuilder.Create(server, args);

// First to run
builder.AddMcpMiddleware<GlobalExceptionMiddleware>();

// Second to run
builder.AddMcpMiddleware<LoggingMiddleware>();

// Third to run
builder.AddMcpMiddleware<ValidationMiddleware>();
```

## 💡 Common Use Cases

### 1. Global Exception Handling
Catch exceptions from any tool and convert them to a standardized JSON-RPC error.

```csharp
public class ExceptionMiddleware : IMcpMiddleware
{
    public async Task<JsonRpcResponse> InvokeAsync(McpMiddlewareContext context, McpMiddlewareDelegate next, CancellationToken ct)
    {
        try
        {
            return await next(context, ct);
        }
        catch (Exception ex)
        {
            return JsonRpcResponse.FromError(
                JsonRpcError.ErrorCodes.InternalError, 
                $"Server Error: {ex.Message}", 
                context.Request.Id
            );
        }
    }
}
```

### 2. Request Validation
Reject requests based on custom logic before they reach the tool.

```csharp
public class ValidationMiddleware : IMcpMiddleware
{
    public async Task<JsonRpcResponse> InvokeAsync(McpMiddlewareContext context, McpMiddlewareDelegate next, CancellationToken ct)
    {
        if (context.Request.Method == "tools/call" && context.Request.Params == null)
        {
             return JsonRpcResponse.FromError(
                 JsonRpcError.ErrorCodes.InvalidParams, 
                 "Parameters are required", 
                 context.Request.Id
             );
        }

        return await next(context, ct);
    }
}
```

### 3. Performance Monitoring
Measure how long each tool execution takes.

```csharp
public class TimingMiddleware : IMcpMiddleware
{
    public async Task<JsonRpcResponse> InvokeAsync(McpMiddlewareContext context, McpMiddlewareDelegate next, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var response = await next(context, ct);
        sw.Stop();

        Console.Error.WriteLine($"[Perf] {context.Request.Method} took {sw.ElapsedMilliseconds}ms");
        return response;
    }
}
```
