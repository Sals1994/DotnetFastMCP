# Stdio Transport Support in DotnetFastMCP

This guide details the **Stdio Transport** implementation in DotnetFastMCP, enabling your MCP servers to communicate via Standard Input/Output (Stdin/Stdout). This is the primary transport mechanism used by local LLM clients like **Claude Desktop**.

## 🚀 Overview

The Model Context Protocol (MCP) supports multiple transports. While HTTP (SSE) is great for remote servers, **Stdio** is the standard for local integrations. It allows an LLM client to spawn your server process directly and communicate via JSON-RPC messages piped through the console.

**DotnetFastMCP** now supports Stdio out-of-the-box, allowing you to run the *same* server application in either HTTP mode or Stdio mode using a simple command-line flag.

## 🛠️ implementation Details

The Stdio transport is built on a decoupled architecture that separates the transport layer from the core protocol logic.

### 1. `McpRequestHandler`
We extracted the core MCP logic (request validation, tool discovery, execution) from the ASP.NET middleware into a dedicated `McpRequestHandler` service. This service is transport-agnostic, meaning it doesn't care if the request came from HTTP or Stdio.

### 2. `McpStdioTransport`
This class is responsible for the transport-specific loop:
-   **Reading**: Listens to `Console.In` for JSON-RPC messages (line-by-line).
-   **Processing**: Deserializes requests and delegates them to `McpRequestHandler`.
-   **Writing**: Serializes responses and writes them to `Console.Out`.

### 3. Dual-Mode Support
We introduced the `RunMcpAsync(args)` extension method for `WebApplication`. This smart runner checks the command-line arguments:
-   If `--stdio` is present: It runs the `McpStdioTransport` loop.
-   If missing: It runs the standard ASP.NET Core HTTP server (`app.RunAsync()`).

## 💻 Usage Guide

Enabling Stdio support in your DotnetFastMCP server is straightforward.

### 1. Update `Program.cs`

Modify your entry point to use `RunMcpAsync` and ensuring logging is safe (see below).

```csharp
using FastMCP.Hosting; // Import the namespace

var builder = McpServerBuilder.Create(server, args);
// ... configure tools and resources ...

var app = builder.Build();

// REPLACE app.Run() or app.RunAsync() with:
await app.RunMcpAsync(args);
```

### 2. ⚠️ Critical: Handling Logging

In Stdio mode, **Standard Output (stdout)** is reserved exclusively for the JSON-RPC protocol.
If your application prints **ANYTHING** else to stdout (e.g., `Console.WriteLine("Server Started")`), it will corrupt the protocol stream, causing the client (like Claude Desktop) to disconnect or error out.

**Rules for Logging:**
1.  **Use `Console.Error`**: For manual logs, always use `Console.Error.WriteLine(...)`. This writes to Stderr, which is safe and visible in client logs without breaking the protocol.
2.  **Framework Logging**: DotnetFastMCP automatically disables the default `ConsoleLogger` (which writes to stdout) when running in `--stdio` mode.

**Correct Example:**
```csharp
// ✅ GOOD: Writes to Stderr
Console.Error.WriteLine($"[BasicServer] Registered {mcpServer.Tools.Count} tools");

// ❌ BAD: Writes to Stdout (Breaks Stdio!)
// Console.WriteLine("Server Started"); 
```

### 3. Running the Server

#### HTTP Mode (Default)
Run as usual. The server listens on the configured port (e.g., 5000).
```bash
./BasicServer.exe
```

#### Stdio Mode
Pass the `--stdio` flag. The server will start and wait for JSON-RPC input via Stdin.
```bash
./BasicServer.exe --stdio
```

## 🔌 Integration with Claude Desktop

To use your DotnetFastMCP server with Claude Desktop:

1.  Locate your `claude_desktop_config.json` (usually in `%APPDATA%\Claude` on Windows).
2.  Add your server configuration:

```json
{
  "mcpServers": {
    "dotnet-server": {
      "command": "C:\\path\\to\\your\\BasicServer.exe",
      "args": ["--stdio"]
    }
  }
}
```
3.  Restart Claude Desktop. Your .NET tools should now be available to Claude!

## 🧩 Architecture Diagram

```mermaid
graph TD
    Client[Client (Claude/HTTP)] -->|Input| Transport
    
    subgraph DotnetFastMCP Server
        Transport{Transport Layer}
        Transport -->|--stdio| Stdio[McpStdioTransport]
        Transport -->|Default| HTTP[McpProtocolMiddleware]
        
        Stdio --> Handler[McpRequestHandler]
        HTTP --> Handler
        
        Handler --> Logic[Tool/Resource Execution]
    end
    
    Logic --> Handler
    Handler --> Transport
    Transport -->|Output| Client
```

## 📝 Troubleshooting

**Symtom**: Client connects but immediately disconnects or shows "Protocol Error".
**Cause**: You are likely printing to stdout.
**Fix**: Check for any `Console.WriteLine` calls in your startup code or tools and change them to `Console.Error.WriteLine`.

**Symtom**: "Method not found" error.
**Cause**: Ensure your tool attributes `[McpTool("name")]` match what you are calling. Note that transport-level methods like `tools/list` need to be implemented by the handler (currently in progress).

## 🧪 How to Test Stdio

Since Stdio relies on input pipes, you cannot test it with browser-based tools or `.rest` files. Use one of the following methods for manual verification.

### Method 1: PowerShell Piping (Quick Test)

You can pipe a JSON string directly into the executable.

```powershell
echo '{"jsonrpc": "2.0", "id": 1, "method": "add_numbers", "params": {"a": 10, "b": 20}}' | .\BasicServer.exe --stdio
```

**Expected Output:**
```json
{"jsonrpc":"2.0","result":30,"id":1}
```


