# Protocol Discovery in DotnetFastMCP

DotnetFastMCP now supports **Protocol Discovery**, enabling MCP clients to dynamically discover the tools and resources available on your server. This implements the `tools/list` and `resources/list` methods of the MCP specification.

## 🚀 Overview

Before this feature, clients had to "know" tool names in advance. With Protocol Discovery, your server can now be fully explored by generic clients like **Claude Desktop**, allowing them to list and display your tools automatically.

## 🛠️ Implementation Details

The discovery logic is handled automatically by the framework's `McpRequestHandler`.

### 1. Tools Discovery (`tools/list`)
The framework reflects on all methods marked with `[McpTool]` and generates a standard JSON schema for them.

**Example Request:**
```json
{ "jsonrpc": "2.0", "method": "tools/list", "id": 1 }
```

**Example Response:**
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": {
    "tools": [
      {
        "name": "add_numbers",
        "description": "Adds two numbers",
        "inputSchema": {
          "type": "object",
          "properties": {
             "a": { "type": "integer" },
             "b": { "type": "integer" }
          },
          "required": ["a", "b"]
        }
      }
    ]
  }
}
```

### 2. Resources Discovery (`resources/list`)
Similarly, methods marked with `[McpResource]` are listed.

**Attribute Updates:**
The `[McpResource]` attribute now supports metadata:
```csharp
[McpResource("file:///logs/app.log", Name = "App Logs", MimeType = "text/plain")]
public static string ReadLogs() { ... }
```

## 💻 Usage

This feature is **enabled by default**. You do not need to change any configuration.

### listing Tools
Just define your tools as usual:

```csharp
[McpTool("calculate_sum", Description = "Calculates the sum of two integers")]
public static int Calculate(int a, int b) => a + b;
```

The framework will automatically expose this in `tools/list`.

## 🧪 Verification

You can verify Protocol Discovery manually using PowerShell piping.

### Verify Tools List

```powershell
echo '{"jsonrpc": "2.0", "method": "tools/list", "id": 1}' | .\DotnetFastMCP\examples\BasicServer\bin\Debug\net8.0\BasicServer.exe --stdio
```

**Expected Output:**
A JSON response containing a list of your tools (e.g., `add_numbers`).

### Verify Resources List

```powershell
echo '{"jsonrpc": "2.0", "method": "resources/list", "id": 2}' | .\DotnetFastMCP\examples\BasicServer\bin\Debug\net8.0\BasicServer.exe --stdio
```

**Expected Output:**
A JSON response containing a list of your resources (e.g., `file:///logs/app.log`).
