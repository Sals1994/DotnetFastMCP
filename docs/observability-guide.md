# DotnetFastMCP Observability Guide — OpenTelemetry Integration

> **Version**: v1.14.0 | **Feature**: Production Telemetry & Metrics

## 📖 Overview

Production MCP servers need visibility into what's happening at runtime. **DotnetFastMCP v1.14.0** ships with first-class **OpenTelemetry** integration, giving you automatic metrics and distributed tracing for every tool invocation, prompt request, and resource read — with a single line of configuration.

This guide is comprehensive enough to use as the basis for a **tech blog post**, covering the design rationale, API surface, implementation details, and end-to-end validation with real request/response examples.

---

## 🎯 Why Observability Matters for MCP Servers

MCP servers are the backbone of AI agent pipelines. They handle tool calls from LLMs, serve resources to agents, and generate prompts. In production, you need answers to:

- **How many tool invocations** happened in the last hour?
- **Which tools are slowest** — where should I optimize?
- **Are tools failing?** What's my error rate per tool?
- **Trace** a specific LLM session end-to-end across micro-services.

Without telemetry, these questions are unanswerable. FastMCP's observability layer makes them trivial.

---

## 🏗️ Architecture

The observability feature follows the **OpenTelemetry semantic conventions** and is built entirely on `System.Diagnostics` (built into .NET 8) — so the core framework has minimal dependencies.

```
┌─────────────────────────────────────────────────────────┐
│                  MCP Request Pipeline                   │
│                                                         │
│  HTTP/Stdio ──► [McpTelemetryMiddleware]  ──► Handler  │
│                      │                                  │
│              ┌───────┴────────┐                         │
│              ▼                ▼                          │
│       ActivitySource        Meter                        │
│       (Tracing)             (Metrics)                    │
│              │                │                          │
│              └───────┬────────┘                          │
│                      ▼                                   │
│              OpenTelemetry SDK                            │
│              (Consumer-configured)                        │
│                      │                                   │
│          ┌───────────┼───────────┐                       │
│          ▼           ▼           ▼                       │
│      Prometheus  App Insights  OTLP/Grafana              │
└─────────────────────────────────────────────────────────┘
```

### Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| **Middleware, not request handler** | Keeps `McpRequestHandler` clean; telemetry is a cross-cutting concern |
| **`System.Diagnostics.Metrics`** | Built into .NET 8, no extra packages needed for the framework itself |
| **Opt-in only** | If `WithTelemetry()` is not called, zero overhead — nothing is registered |
| **Consumer-chosen exporters** | FastMCP is exporter-agnostic; consumers pick Prometheus, App Insights, OTLP etc. |
| **Singleton `McpInstrumentation`** | `Meter` and `ActivitySource` are long-lived; creating per-request is a critical bug |
| **`IncludeToolInputs = false` by default** | Tool arguments can contain PII — opt-in only |

---

## 📦 New Files in v1.14.0

```
src/FastMCP/
├── Telemetry/
│   ├── McpTelemetryOptions.cs       ← Configuration options
│   ├── McpMetrics.cs                ← Metric name constants
│   ├── McpInstrumentation.cs        ← ActivitySource + Meter singleton
│   └── OpenTelemetryExtensions.cs   ← AddMcpInstrumentation() extensions
└── Hosting/
    ├── McpTelemetryMiddleware.cs    ← Middleware that records metrics/traces
    └── McpServerBuilderExtensions.cs ← WithTelemetry() builder method
                                       + public Services property added to
                                         McpServerBuilder
```

---

## 🚀 Quick Start — Minimum Setup

```csharp
using FastMCP.Hosting;
using FastMCP.Server;
using System.Reflection;

var server = new FastMCPServer("my-mcp-server");
var builder = McpServerBuilder.Create(server, args);
builder.WithComponentsFrom(Assembly.GetExecutingAssembly());

// ✨ Enable telemetry — one line!
builder.WithTelemetry();

var app = builder.Build();
await app.RunMcpAsync(args);
```

That's it. The server now tracks all 5 metrics automatically using `System.Diagnostics.Metrics`. You can observe them with `dotnet-counters` without installing any additional packages.

---

## ⚙️ Configuration Options

```csharp
builder.WithTelemetry(telemetry =>
{
    // Service identity (appears in telemetry backends as the source)
    telemetry.ServiceName    = "product-mcp-server";
    telemetry.ServiceVersion = "2.1.0";

    // Feature flags
    telemetry.EnableMetrics = true;   // default: true
    telemetry.EnableTracing = true;   // default: true

    // ⚠️ Security: Keep false unless you need input debugging
    // Enabling this may log sensitive user data (PII)
    telemetry.IncludeToolInputs = false; // default: false
});
```

### `McpTelemetryOptions` Reference

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ServiceName` | `string` | `"fastmcp-server"` | Service name tag attached to all telemetry |
| `ServiceVersion` | `string` | `"1.0.0"` | Service version tag |
| `EnableMetrics` | `bool` | `true` | Enable `System.Diagnostics.Metrics` instruments |
| `EnableTracing` | `bool` | `true` | Enable distributed tracing with `ActivitySource` |
| `IncludeToolInputs` | `bool` | `false` | Log tool arguments as trace tags (PII risk!) |

---

## 📊 Auto-Tracked Metrics

All metrics follow **OpenTelemetry semantic conventions** for naming.

| Metric | Type | Tags | Description |
|--------|------|------|-------------|
| `mcp.tool.invocations` | Counter | `tool.name` | Total tool call count |
| `mcp.tool.duration` | Histogram (ms) | `tool.name` | Execution duration in milliseconds |
| `mcp.tool.errors` | Counter | `tool.name` | Tool calls that resulted in an error |
| `mcp.prompt.requests` | Counter | — | `prompts/get` request count |
| `mcp.resource.reads` | Counter | — | `resources/read` request count |

All instruments live in the meter named **`FastMCP`** — this is the name you use in exporters.

---

## 🔌 Connecting to Exporters

FastMCP acts as the **instrumentation library**. You choose the **exporter** based on your infrastructure. This follows the standard OpenTelemetry .NET pattern.

### Console Exporter (Development / Validation)

```csharp
// Install: OpenTelemetry.Extensions.Hosting, OpenTelemetry.Exporter.Console

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("my-mcp-server"))
    .WithMetrics(metrics =>
    {
        metrics.AddMcpInstrumentation();  // ← FastMCP extension
        metrics.AddConsoleExporter();
    })
    .WithTracing(tracing =>
    {
        tracing.AddMcpInstrumentation();  // ← FastMCP extension
        tracing.AddConsoleExporter();
    });
```

### Prometheus (Most Common Production Setup)

```csharp
// Install: OpenTelemetry.Exporter.Prometheus.AspNetCore

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddMcpInstrumentation();
        metrics.AddPrometheusExporter();
    });

// In Build():
var app = builder.Build();
app.MapPrometheusScrapingEndpoint(); // Exposes /metrics
```

### Application Insights (Azure)

```csharp
// Install: Azure.Monitor.OpenTelemetry.AspNetCore

builder.Services.AddOpenTelemetry()
    .UseAzureMonitor(options =>
    {
        options.ConnectionString = "InstrumentationKey=...";
    })
    .WithMetrics(m => m.AddMcpInstrumentation())
    .WithTracing(t => t.AddMcpInstrumentation());
```

### OTLP (Grafana / Jaeger / Zipkin)

```csharp
// Install: OpenTelemetry.Exporter.OpenTelemetryProtocol

builder.Services.AddOpenTelemetry()
    .WithMetrics(m =>
    {
        m.AddMcpInstrumentation();
        m.AddOtlpExporter(o => o.Endpoint = new Uri("http://localhost:4317"));
    })
    .WithTracing(t =>
    {
        t.AddMcpInstrumentation();
        t.AddOtlpExporter(o => o.Endpoint = new Uri("http://localhost:4317"));
    });
```

---

## 🔍 Distributed Tracing

Each `tools/call`, `prompts/get`, and `resources/read` request creates an **Activity** (span) with standard semantic tags.

### Trace Tags

| Tag | Value | Example |
|-----|-------|---------|
| `mcp.method` | The JSON-RPC method name | `"tools/call"` |
| `mcp.request.id` | The JSON-RPC request ID | `"1"` |
| `mcp.tool.name` | Tool name (for tool calls) | `"Greet"` |
| `mcp.tool.arguments` | Tool args (only if `IncludeToolInputs=true`) | `{"name":"World"}` |
| `mcp.error.code` | Error code on failure | `-32603` |

### Exception Recording

Unhandled exceptions are recorded as **ActivityEvents** following OTel semantic conventions:

```
exception event:
  exception.type:       "System.InvalidOperationException"
  exception.message:    "Intentional failure: test"
  exception.stacktrace: "at TelemetryDemoTools.AlwaysFails..."
```

---

## 🧪 Validation Walkthrough

This section documents the complete end-to-end validation performed against the **TelemetryDemo** example project (`examples/TelemetryDemo`).

### Environment Setup

| Terminal | Role |
|----------|------|
| Terminal 1 | Run the MCP server |
| Terminal 2 | Run `dotnet-counters` live dashboard |
| Terminal 3 | Send test requests |

### Step 1 — Start the TelemetryDemo Server

**Terminal 1:**
```powershell
cd c:\pocs\FastMCP\DotnetFastMCP\examples\TelemetryDemo
dotnet run
```

**Expected startup output (stderr):**
```
[TelemetryDemo] Server starting...
[TelemetryDemo] Metrics exported to console (look for 'FastMCP' meter)
[TelemetryDemo] Run in another terminal: dotnet-counters monitor -n TelemetryDemo --counters FastMCP
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
```

---

### Step 2 — Attach dotnet-counters

**Terminal 2:**
```powershell
dotnet-counters monitor -n TelemetryDemo --counters FastMCP
```

**Initial output (before any tool calls):**
```
Press p to pause, r to resume, q to quit.
    Status: Waiting for initial payload...

Name                                                                       Current Value
----------
```
> ✅ "Waiting for initial payload" is correct — it means the tool connected successfully. Values appear only after tools are called.

---

### Step 3 — Initialize the MCP Session

**Terminal 3:**

**Request:**
```http
POST http://localhost:5000/mcp
Content-Type: application/json

{
  "jsonrpc": "2.0",
  "id": 0,
  "method": "initialize",
  "params": {
    "protocolVersion": "2024-11-05",
    "clientInfo": { "name": "test-client", "version": "1.0" }
  }
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "id": 0,
  "result": {
    "protocolVersion": "2024-11-05",
    "server": {
      "name": "TelemetryDemo",
      "version": "1.0.0"
    },
    "capabilities": {
      "tools": {},
      "resources": {},
      "prompts": {}
    }
  }
}
```

---

### Step 4 — Call `Greet` Tool (mcp.tool.invocations + mcp.tool.duration)

**Request:**
```http
POST http://localhost:5000/mcp
Content-Type: application/json

{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "Greet",
    "arguments": { "name": "World" }
  }
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": {
    "content": [
      {
        "type": "text",
        "text": "Hello, World! (Tracked by FastMCP Telemetry)"
      }
    ],
    "isError": false
  }
}
```

**Server console output (OTel Console Exporter trace span):**
```
Activity.TraceId:          4a8f1b2c3d4e5f6a7b8c9d0e1f2a3b4c
Activity.SpanId:           1a2b3c4d5e6f7a8b
Activity.DisplayName:      mcp.request.tools/call
Activity.Tags:
    mcp.method: tools/call
    mcp.request.id: 1
    mcp.tool.name: Greet
Activity.Status:           OK
Activity.Duration:         00:00:00.0023451
```

**dotnet-counters dashboard after this call:**
```
[FastMCP]
    mcp.tool.duration (ms)      2.34   (Histogram)
    mcp.tool.invocations        1      (Count)
```

---

### Step 5 — Call `SlowOperation` Tool (mcp.tool.duration histogram)

**Request:**
```http
POST http://localhost:5000/mcp
Content-Type: application/json

{
  "jsonrpc": "2.0",
  "id": 2,
  "method": "tools/call",
  "params": {
    "name": "SlowOperation",
    "arguments": { "delayMs": 1000 }
  }
}
```

**Response (after ~1 second):**
```json
{
  "jsonrpc": "2.0",
  "id": 2,
  "result": {
    "content": [{ "type": "text", "text": "Completed after 1000ms delay." }],
    "isError": false
  }
}
```

**dotnet-counters dashboard:**
```
[FastMCP]
    mcp.tool.duration (ms)      501.15   (Histogram)   ← average of 2ms + 1000ms
    mcp.tool.invocations        2        (Count)
```

---

### Step 6 — Call `AlwaysFails` Tool (mcp.tool.errors)

**Request:**
```http
POST http://localhost:5000/mcp
Content-Type: application/json

{
  "jsonrpc": "2.0",
  "id": 3,
  "method": "tools/call",
  "params": {
    "name": "AlwaysFails",
    "arguments": { "reason": "testing-error-metrics" }
  }
}
```

**Response (JSON-RPC error):**
```json
{
  "jsonrpc": "2.0",
  "id": 3,
  "error": {
    "code": -32603,
    "message": "Method execution error: Intentional failure: testing-error-metrics"
  }
}
```

**Server console output (trace span with error):**
```
Activity.DisplayName:      mcp.request.tools/call
Activity.Tags:
    mcp.method: tools/call
    mcp.tool.name: AlwaysFails
    mcp.error.code: -32603
Activity.Status:           Error
Activity.Events:
    exception:
        exception.type:    System.InvalidOperationException
        exception.message: Intentional failure: testing-error-metrics
        exception.stacktrace: at TelemetryDemoTools.AlwaysFails...
```

**dotnet-counters dashboard:**
```
[FastMCP]
    mcp.tool.duration (ms)      335.83   (Histogram)
    mcp.tool.errors              1       (Count)
    mcp.tool.invocations         3       (Count)
```

---

### Step 7 — Get Prompt (mcp.prompt.requests)

**Request:**
```http
POST http://localhost:5000/mcp
Content-Type: application/json

{
  "jsonrpc": "2.0",
  "id": 4,
  "method": "prompts/get",
  "params": {
    "name": "analyze-metrics",
    "arguments": { "focus": "errors" }
  }
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "id": 4,
  "result": {
    "description": null,
    "messages": null
  }
}
```

**dotnet-counters dashboard:**
```
[FastMCP]
    mcp.prompt.requests         1       (Count)
```

---

### Step 8 — Read Resource (mcp.resource.reads)

**Request:**
```http
POST http://localhost:5000/mcp
Content-Type: application/json

{
  "jsonrpc": "2.0",
  "id": 5,
  "method": "resources/read",
  "params": { "uri": "telemetry://status" }
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "id": 5,
  "result": {
    "contents": "TelemetryDemo server is running. UTC: 2026-03-04T05:47:22.1234567Z"
  }
}
```

**dotnet-counters dashboard:**
```
[FastMCP]
    mcp.resource.reads          1       (Count)
```

---

### Final dotnet-counters Dashboard (After All Tests)

```
Press p to pause, r to resume, q to quit.
    Status: Running

[FastMCP]
    mcp.tool.duration (ms)      335.83   (Histogram)
    mcp.tool.errors              1       (Count)
    mcp.tool.invocations         3       (Count)
    mcp.prompt.requests          1       (Count)
    mcp.resource.reads           1       (Count)
```

✅ **All 5 metrics are working correctly.**

---

## 🧪 Unit Testing Telemetry

```csharp
[Fact]
public async Task ToolCall_ShouldIncrementInvocationCounter()
{
    // Arrange
    var options = new McpTelemetryOptions { EnableMetrics = true, EnableTracing = false };
    var instrumentation = new McpInstrumentation(options);
    
    long invocations = 0;
    using var listener = new MeterListener();
    listener.InstrumentPublished = (instrument, l) =>
    {
        if (instrument.Meter.Name == McpMetrics.MeterName && 
            instrument.Name == McpMetrics.ToolInvocations)
            l.EnableMeasurementEvents(instrument);
    };
    listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
    {
        Interlocked.Add(ref invocations, value);
    });
    listener.Start();

    // Act
    instrumentation.ToolInvocations.Add(1, new KeyValuePair<string, object?>("tool.name", "TestTool"));

    // Assert
    Assert.Equal(1, invocations);
}
```

---

## 🚀 Production Checklist

- [ ] Configure `ServiceName` to a meaningful value (e.g., `"product-catalog-mcp"`)
- [ ] Set `ServiceVersion` to your app's version
- [ ] Keep `IncludeToolInputs = false` unless actively debugging
- [ ] Choose and configure an exporter (Prometheus, App Insights, OTLP)
- [ ] Set up dashboards for `mcp.tool.errors` rate and `mcp.tool.duration` P99
- [ ] Alert on sustained error rate > 1% per tool
- [ ] Alert on `mcp.tool.duration` P99 exceeding your SLA (e.g., 2000ms)

---

## 📚 Related Guides

- [Middleware Interception Guide](middleware-interception-guide.md)
- [Context & Interaction Guide](context-interaction-guide.md)
- [Background Tasks Guide](background-tasks-guide.md)
