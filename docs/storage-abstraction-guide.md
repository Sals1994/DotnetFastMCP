# Storage Abstraction

**DotnetFastMCP** provides a built-in storage abstraction to help your tools persist state across executions. This logic allows your MCP server to remember user preferences, cache expensive results, or maintain conversation context.

## 🔑 Key Features

-   **Simple Key-Value API**: Easy `Get`, `Set`, and `Delete` operations.
-   **Dependency Injection**: `IMcpStorage` is injected into the container, allowing you to swap implementations (Memory, File, Redis, SQL) without changing your tools.
-   **Context Integration**: Access storage directly from `McpContext`.
-   **Type Safety**: Generic `GetAsync<T>` and `SetAsync<T>` methods handle JSON serialization for you.

## 🚀 Getting Started

### 1. Using Storage in Tools

The easiest way to use storage is by accepting `McpContext` as a parameter in your tool.

```csharp
[McpTool("remember_preference")]
public async Task<string> RememberPreference(string key, string value, McpContext context)
{
    await context.Storage.SetAsync(key, value);
    return $"Saved {key} = {value}";
}

[McpTool("get_preference")]
public async Task<string> GetPreference(string key, McpContext context)
{
    var value = await context.Storage.GetAsync<string>(key);
    return value ?? "Preference not found";
}
```

### 2. Built-in Implementations

The framework comes with a default **In-Memory** implementation useful for testing and simple stateless servers.

-   **InMemoryMcpStorage**: Stores data in a `ConcurrentDictionary`. Data is **lost** when the server restarts.

This is registered by default if you don't provide your own.

### 3. Custom Implementation (Persistent)

To save data to a file or database, implement `IMcpStorage`.

```csharp
public class FileMcpStorage : IMcpStorage
{
    private readonly string _filePath = "storage.json";

    public async Task<T?> GetAsync<T>(string key)
    {
        // Read file, deserialize, return value
    }

    public async Task SetAsync<T>(string key, T value)
    {
        // Update dict, serialize, write to file
    }

    public async Task DeleteAsync(string key)
    {
        // Remove key, write to file
    }
}
```

### 4. Registration

Register your custom storage in `Program.cs`:

```csharp
var server = new FastMCPServer("MyServer");
var builder = McpServerBuilder.Create(server, args);

// Register custom storage
builder.AddMcpStorage<FileMcpStorage>(); 

var app = builder.Build();
```

## 🏗️ API Reference

### `IMcpStorage` Interface

```csharp
public interface IMcpStorage
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value);
    Task DeleteAsync(string key);
}
```

## 🧪 Validation & Testing

To verify your storage implementation involves:

1.  **Unit Testing**: Mock `IMcpStorage` to test tool logic without side effects.
2.  **Integration Testing**: Use `InMemoryMcpStorage` to verify the flow.

### Example Test Trace

```csharp
// 1. Set Value
await context.Storage.SetAsync("user_theme", "dark");

// 2. Get Value
var theme = await context.Storage.GetAsync<string>("user_theme");
Assert.Equal("dark", theme);
```
