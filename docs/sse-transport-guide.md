# Server-Sent Events (SSE) Transport Guide

> **New in v1.3.0**: Enterprise-grade real-time communication for HTTP-based MCP servers.

The **SSE Transport** feature enables your DotnetFastMCP server to push real-time updates—such as logs, progress reports, and intermediate results—to clients over standard HTTP, without requiring them to poll the server. This is critical for building responsive AI agents and tools that perform long-running tasks.

## 🚀 Why SSE?

In the standard Model Context Protocol (MCP), clients need a way to receive asynchronous notifications.
*   **Stdio**: Easy for local processes, but hard to scale or deploy remotely.
*   **HTTP POST**: Good for request/response, but the server cannot "initiate" a message to the client.
*   **SSE**: The perfect middle ground. It keeps a lightweight, uni-directional channel open for the server to continuously stream events to the client.

## ARCHITECTURE

Our implementation follows the official MCP HTTP transport specification:

1.  **Handshake (`GET /sse`)**:
    *   The client establishes a long-lived connection to the `/sse` endpoint.
    *   The server responds with an `endpoint` event containing a unique Session ID.
    *   *Example Event:* `event: endpoint\ndata: /message?sessionId=xyz...`

2.  **Command Channel (`POST /message`)**:
    *   The client sends JSON-RPC requests to `/message?sessionId=...`.
    *   These requests return `202 Accepted` immediately.

3.  **Result Stream**:
    *   The actual execution results (and any logs/progress) are streamed back asynchronously over the open `/sse` connection.

## 🛠️ Usage

Good news! If you are using `McpServerBuilder`, **SSE is enabled automatically**.

### Server Setup
No extra code is usually required. Ensure your `Program.cs` builds the server normally:

```csharp
var builder = McpServerBuilder.Create(
    new FastMCPServer("My Server", "1.0.0"), 
    args
);

// ... register tools ...

var app = builder.Build();
app.Run();
```

Your server now listens on:
*   `http://localhost:5000/sse`
*   `http://localhost:5000/message`

## 🧪 Manual Verification Guide

You can verify the SSE transport works correctly using command-line tools like `curl` (Linux/Mac) or PowerShell (Windows).

### Prerequisites
*   Keep your server running on `http://localhost:5000`.
*   Open **two** terminal windows.

### Step 1: Subscribe to the Stream (Terminal 1)
This terminal acts as the **Client Listener**. It will display the raw event stream.

**Command:**
```powershell
curl -N -v http://localhost:5000/sse
```

**What to look for:**
You should see the headers and an initial `endpoint` event:
```text
event: endpoint
data: "/message?sessionId=b9d0e7a1-..."
```
**Copy that Session ID!** You will need it for the next step.

### Step 2: proper Send a Request (Terminal 2)
This terminal acts as the **Command Sender**. We will invoke the `add_numbers` tool.

**PowerShell Command:**
```powershell
# 1. Set your session ID (pasted from Terminal 1)
$sessionId = "YOUR_SESSION_ID_HERE"

# 2. Define the JSON-RPC Payload
$body = '{ "jsonrpc": "2.0", "method": "add_numbers", "params": { "a": 15, "b": 25 }, "id": 1 }'

# 3. Send the POST request
Invoke-RestMethod -Uri "http://localhost:5000/message?sessionId=$sessionId" `
    -Method Post `
    -ContentType "application/json" `
    -Body $body
```

**Bash/Curl Command:**
```bash
sessionId="YOUR_SESSION_ID_HERE"
curl -X POST "http://localhost:5000/message?sessionId=$sessionId" \
     -H "Content-Type: application/json" \
     -d '{ "jsonrpc": "2.0", "method": "add_numbers", "params": { "a": 15, "b": 25 }, "id": 1 }'
```

### Step 3: Verify the Result (Terminal 1)
Look back at your first terminal. You should see the calculation result arrive in the stream:

```text
event: message
data: {"jsonrpc":"2.0","id":1,"result":40}
```

## 🔍 Troubleshooting

| Issue | Solution |
| :--- | :--- |
| **404 Not Found** on POST | Double-check the `sessionId`. It changes every time you restart the `curl` listener. |
| **Method not found** | Ensure you are using the correct internal name of the tool (e.g., `add_numbers`). |
| **CORS Errors** | If calling from a browser, ensure `WithCorsPolicy` is configured in your builder. |
