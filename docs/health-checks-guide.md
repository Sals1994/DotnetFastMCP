# DotnetFastMCP Health Checks & Diagnostics Guide

> **Version**: v1.15.0 | **Feature**: Built-In Health Monitoring

## 📖 Overview

Production MCP servers run inside Kubernetes pods, Docker containers, and Azure Container Apps — all of which **continuously probe your service** to decide whether to route traffic to it, restart it, or drain it. Without a dedicated health endpoint, these systems resort to TCP-level checks that cannot detect application-level problems like a broken database connection, an offline LLM provider, or memory exhaustion.

**DotnetFastMCP v1.15.0** ships with a **first-class health check system** that:

- Exposes a standards-compliant `GET /mcp/health` endpoint **automatically**
- Works **out of the box with zero code** — just call `.WithHealthChecks()`
- Allows consumers to plug in any custom check as a **simple lambda** (no interfaces to implement)
- Returns a **structured JSON payload** with per-check status, duration, and server diagnostics
- Maps to correct **HTTP status codes** understood by Kubernetes, load balancers, and APM tools
- Is **unauthenticated by design** so infrastructure probes always reach it
- Has **zero overhead** when not configured (fully opt-in)

This guide is comprehensive enough to serve as the basis for a **tech blog post**, covering the design rationale, API surface, implementation details, and end-to-end validation with real request/response examples.

---

## 🎯 Why Health Checks Matter for MCP Servers

MCP servers are the backbone of AI agent pipelines. They serve tools to LLMs, expose resources to agents, and handle prompt requests. In production, failures are silent without a health signal:

| Problem | Symptom (without health checks) | Signal (with health checks) |
|---|---|---|
| LLM provider goes offline | Tool calls start returning errors | `llm_provider: Unhealthy` immediately |
| Database connection pool exhausted | Slow / failing queries after a delay | `database: Unhealthy` before users notice |
| Memory leak nearing OOM | Pod killed by kubelet, cold restart | `memory: Degraded` before OOM hit |
| Code deployment broke routing | All requests return 500 | `mcp_server: Healthy` + dependent checks fail |
| Slow external API dependency | Cascading timeout chain | `external_api: Degraded` (timed out) |

Without health checks, `kubectl` sees a running process and keeps routing traffic. With health checks, Kubernetes **automatically stops routing to failing pods** and restarts them.

---

## 🏗️ Architecture

The health check system follows the same architectural principles as the Observability feature:
- **Opt-in only** — if `.WithHealthChecks()` is never called, the endpoint does not exist
- **Options pattern** — a dedicated `McpHealthCheckOptions` class captures configuration
- **Singleton engine** — `McpHealthCheckRegistry` is created once, reused on every request
- **Route registered in `Build()`** — consistent with every other framework endpoint

```
┌──────────────────────────────────────────────────────────┐
│                  Kubernetes / Load Balancer               │
│                                                          │
│          GET /mcp/health  (every 10-30 seconds)          │
└───────────────────────────┬──────────────────────────────┘
                            │
┌───────────────────────────▼──────────────────────────────┐
│                  ASP.NET Core Pipeline                    │
│                                                          │
│  app.MapGet("/mcp/health")  ← registered in Build()      │
│         ↓  AllowAnonymous() ← bypasses auth              │
│                                                          │
│         McpHealthCheckRegistry.RunAllChecksAsync()       │
│                                                          │
│    ┌─────────────────────────────────────────────┐       │
│    │   Task.WhenAll (all checks in parallel)     │       │
│    │                                             │       │
│    │   [mcp_server]  → built-in, always Healthy  │       │
│    │   [database]    → consumer's async lambda   │       │
│    │   [llm]         → consumer's async lambda   │       │
│    │   [memory]      → consumer's sync lambda    │       │
│    └─────────────────────────────────────────────┘       │
│                                                          │
│    Rollup:   Healthy → 200  Degraded → 207  Unhealthy → 503
└──────────────────────────────────────────────────────────┘
```

### Key Design Decisions

| Decision | Rationale |
|---|---|
| **`GET` not `POST`** | Health probes always use GET. POST would require body parsing and would break all standard probe tools |
| **`AllowAnonymous()`** | Infrastructure probes don't carry auth tokens. The health endpoint must be reachable regardless of auth configuration |
| **Parallel `Task.WhenAll`** | All checks run concurrently — a slow database check does not delay a fast memory check |
| **Per-check timeout** | A hanging check is reported as `Degraded` after `MaxResponseTimeMs`, not left to block indefinitely |
| **`GetService<>` guard in `Build()`** | The endpoint is only mapped if `.WithHealthChecks()` was called. True opt-in with zero overhead otherwise |
| **Singleton `McpHealthCheckRegistry`** | Checks are stateless functions; creating per-request would be wasteful. Consistent with `McpInstrumentation` |
| **Lambda API, not interfaces** | Consumers shouldn't implement `IHealthCheck`. A lambda is 1 line. Interfaces are 5+ lines |

---

## 📦 New Files in v1.15.0

```
src/FastMCP/
└── Health/
    ├── McpHealthCheckOptions.cs     ← Configuration + fluent check registration API
    ├── McpHealthCheckResult.cs      ← Typed JSON response contract
    ├── McpHealthCheckRegistry.cs    ← Runtime engine (parallel execution + timeout)
    └── McpHealthCheckExtensions.cs  ← WithHealthChecks() builder extension
src/FastMCP/
└── Hosting/
    └── McpServerBuilder.cs          ← ~12 lines added in Build() to map GET /mcp/health
examples/
└── HealthChecksDemo/
    ├── HealthChecksDemo.csproj
    ├── Program.cs
    └── HealthChecksDemoTools.cs
```

---

## 🚀 Quick Start — Zero Configuration

```csharp
using FastMCP.Health;
using FastMCP.Hosting;
using FastMCP.Server;
using System.Reflection;

var server = new FastMCPServer("my-mcp-server");
var builder = McpServerBuilder.Create(server, args);
builder.WithComponentsFrom(Assembly.GetExecutingAssembly());

// ✨ Enable health checks — one line!
builder.WithHealthChecks();

var app = builder.Build();
await app.RunMcpAsync(args);
```

That's it. The server now exposes `GET /mcp/health` and automatically reports:
- Built-in `mcp_server` self-check (always Healthy)
- Server diagnostics: name, framework version, tool/resource/prompt counts, uptime

---

## ⚙️ Configuration Options

```csharp
builder.WithHealthChecks(checks =>
{
    // --- Built-in options ---

    // Path at which the endpoint is exposed. Default: "/mcp/health"
    // Matches the Kubernetes liveness/readiness probe convention.
    checks.HealthEndpointPath = "/mcp/health";

    // Maximum time (ms) any single check may run before being marked Degraded.
    // Default: 5000 ms. Prevents a hanging dependency from blocking the probe.
    checks.MaxResponseTimeMs = 5_000;

    // When true (default), response includes server name, framework version,
    // tool/resource/prompt counts, and uptime.
    // Set to false in environments where metadata must not be exposed.
    checks.IncludeServerDiagnostics = true;

    // --- Custom checks (lambdas — no interface required) ---

    // Synchronous check: returns true = Healthy, false = Unhealthy
    checks.AddCheck("memory", () =>
        GC.GetTotalMemory(false) < 500_000_000L); // < 500 MB

    // Async check: I/O-bound (database, HTTP, etc.)
    checks.AddAsyncCheck("database", async ct =>
        await dbContext.Database.CanConnectAsync(ct));

    checks.AddAsyncCheck("llm_provider", async ct =>
        await llmProvider.IsHealthyAsync(ct));
});
```

### `McpHealthCheckOptions` Reference

| Property | Type | Default | Description |
|---|---|---|---|
| `HealthEndpointPath` | `string` | `"/mcp/health"` | HTTP path of the health endpoint |
| `MaxResponseTimeMs` | `int` | `5000` | Per-check timeout in milliseconds |
| `IncludeServerDiagnostics` | `bool` | `true` | Include automatic server metadata in response |
| `AddCheck(name, Func<bool>)` | method | — | Register a synchronous health check |
| `AddAsyncCheck(name, Func<CancellationToken, Task<bool>>)` | method | — | Register an async health check |

---

## 📋 JSON Response Format

### Healthy Response (HTTP 200)

```json
{
  "status": "Healthy",
  "timestamp": "2026-04-19T20:00:00.000+00:00",
  "checks": [
    {
      "name": "mcp_server",
      "status": "Healthy",
      "description": "MCP server process is running",
      "durationMs": 0
    },
    {
      "name": "memory",
      "status": "Healthy",
      "durationMs": 0.12
    },
    {
      "name": "database",
      "status": "Healthy",
      "durationMs": 4.87
    },
    {
      "name": "llm_provider",
      "status": "Healthy",
      "durationMs": 22.3
    }
  ],
  "diagnostics": {
    "serverName": "my-mcp-server",
    "frameworkVersion": "1.15.0.0",
    "toolCount": 12,
    "resourceCount": 3,
    "promptCount": 2,
    "uptimeSeconds": 3721.4
  }
}
```

### Degraded Response (HTTP 207) — Check Timed Out

```json
{
  "status": "Degraded",
  "timestamp": "2026-04-19T20:01:00.000+00:00",
  "checks": [
    { "name": "mcp_server", "status": "Healthy", "durationMs": 0 },
    { "name": "memory",     "status": "Healthy", "durationMs": 0.09 },
    {
      "name": "database",
      "status": "Degraded",
      "description": "Check timed out after 5000ms",
      "durationMs": 5001.2
    }
  ],
  "diagnostics": { "serverName": "my-mcp-server", "uptimeSeconds": 3801.1 }
}
```

### Unhealthy Response (HTTP 503)

```json
{
  "status": "Unhealthy",
  "timestamp": "2026-04-19T20:02:00.000+00:00",
  "checks": [
    { "name": "mcp_server",   "status": "Healthy",   "durationMs": 0 },
    { "name": "memory",       "status": "Healthy",   "durationMs": 0.1 },
    {
      "name": "llm_provider",
      "status": "Unhealthy",
      "description": "Connection refused (localhost:11434)",
      "durationMs": 15.6
    }
  ],
  "diagnostics": { "serverName": "my-mcp-server", "uptimeSeconds": 3900.7 }
}
```

### Status → HTTP Code Mapping

| Overall Status | HTTP Code | When? |
|---|---|---|
| `Healthy` | **200 OK** | All checks passed |
| `Degraded` | **207 Multi-Status** | Server is up, but ≥1 check timed out |
| `Unhealthy` | **503 Service Unavailable** | ≥1 check returned false or threw an exception |

> **Why 207 for Degraded?** Unlike 200 (all good) or 503 (route away), 207 tells the load balancer "something is partial — log a warning but keep routing". It is the most expressive standard code for this partial-success state.

---

## 🔌 Kubernetes Integration

Configure liveness and readiness probes to use `/mcp/health`:

```yaml
# kubernetes/deployment.yaml
spec:
  containers:
  - name: my-mcp-server
    image: myrepo/my-mcp-server:1.15.0
    livenessProbe:
      httpGet:
        path: /mcp/health
        port: 5000
      initialDelaySeconds: 15   # give app time to warm up
      periodSeconds: 30          # probe every 30s
      failureThreshold: 3        # restart after 3 consecutive failures
    readinessProbe:
      httpGet:
        path: /mcp/health
        port: 5000
      initialDelaySeconds: 5
      periodSeconds: 10
      failureThreshold: 2        # stop routing after 2 failures
```

---

## 🔌 Docker Compose Integration

```yaml
# docker-compose.yml
services:
  mcp-server:
    image: myrepo/my-mcp-server:1.15.0
    ports:
      - "5000:5000"
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:5000/mcp/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 20s
```

---

## 🔌 Azure Container Apps Integration

```yaml
# Azure Container Apps health probe
properties:
  template:
    containers:
    - name: my-mcp-server
      probes:
      - type: Liveness
        httpGet:
          path: "/mcp/health"
          port: 5000
        initialDelaySeconds: 10
        periodSeconds: 30
      - type: Readiness
        httpGet:
          path: "/mcp/health"
          port: 5000
        initialDelaySeconds: 5
        periodSeconds: 10
```

---

## 🧪 Complete Validation Walkthrough

This section documents the complete end-to-end validation performed against the **HealthChecksDemo** example project (`examples/HealthChecksDemo`).

### Environment Setup

| Terminal | Role |
|---|---|
| Terminal 1 | Run the MCP server |
| Terminal 2 | Send test requests with `Invoke-RestMethod` |

---

### Step 1 — Start the HealthChecksDemo Server

**Terminal 1:**
```powershell
cd c:\pocs\FastMCP\DotnetFastMCP\examples\HealthChecksDemo
dotnet run
```

**Expected startup output (stderr):**
```
[HealthChecksDemo] Server starting...
[HealthChecksDemo] Health endpoint → GET http://localhost:5000/mcp/health
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
```

---

### Step 2 — Verify Healthy Response (HTTP 200)

**Terminal 2:**
```powershell
Invoke-RestMethod http://localhost:5000/mcp/health | ConvertTo-Json -Depth 5
```

**Expected response:**
```json
{
  "status": "Healthy",
  "timestamp": "2026-04-19T...",
  "checks": [
    { "name": "mcp_server",             "status": "Healthy", "description": "MCP server process is running", "durationMs": 0 },
    { "name": "memory",                 "status": "Healthy", "durationMs": 0.1 },
    { "name": "simulated_database",     "status": "Healthy", "durationMs": 10.4 },
    { "name": "simulated_llm_provider", "status": "Healthy", "durationMs": 20.7 }
  ],
  "diagnostics": {
    "serverName": "HealthChecksDemo",
    "frameworkVersion": "1.15.0.0",
    "toolCount": 1,
    "resourceCount": 0,
    "promptCount": 0,
    "uptimeSeconds": 4.2
  }
}
```

**Verify the HTTP status code explicitly:**
```powershell
(Invoke-WebRequest http://localhost:5000/mcp/health).StatusCode
# ✅ Expected: 200
```

---

### Step 3 — Verify Unhealthy Response (HTTP 503)

In `Program.cs`, temporarily change `simulated_database` to return `false`:
```csharp
checks.AddAsyncCheck("simulated_database", async ct =>
{
    await Task.Delay(10, ct);
    return false;   // ← temporarily changed to simulate failure
});
```

Stop (Ctrl+C) and restart the server (`dotnet run`), then:

```powershell
(Invoke-WebRequest -Uri http://localhost:5000/mcp/health -SkipHttpErrorCheck).StatusCode
# ✅ Expected: 503
```

```powershell
Invoke-RestMethod -Uri http://localhost:5000/mcp/health -SkipHttpErrorCheck | ConvertTo-Json -Depth 5
```

**Expected response:**
```json
{
  "status": "Unhealthy",
  "checks": [
    { "name": "mcp_server",         "status": "Healthy"   },
    { "name": "memory",             "status": "Healthy"   },
    { "name": "simulated_database", "status": "Unhealthy" },
    { "name": "simulated_llm_provider", "status": "Healthy" }
  ]
}
```

> ✅ Restore `return true;` when done.

---

### Step 4 — Verify Degraded Response (HTTP 207)

In `Program.cs`, set a tight timeout and a slow check:
```csharp
checks.MaxResponseTimeMs = 5; // only 5 ms allowed

checks.AddAsyncCheck("simulated_database", async ct =>
{
    await Task.Delay(100, ct); // 100 ms — will exceed 5 ms timeout
    return true;
});
```

Restart and hit the endpoint:

```powershell
(Invoke-WebRequest -Uri http://localhost:5000/mcp/health -SkipHttpErrorCheck).StatusCode
# ✅ Expected: 207
```

```powershell
Invoke-RestMethod -Uri http://localhost:5000/mcp/health -SkipHttpErrorCheck | ConvertTo-Json -Depth 5
```

**Expected response (key section):**
```json
{
  "status": "Degraded",
  "checks": [
    { "name": "mcp_server",         "status": "Healthy"  },
    {
      "name": "simulated_database",
      "status": "Degraded",
      "description": "Check timed out after 5ms",
      "durationMs": 5.3
    }
  ]
}
```

> ✅ Restore `MaxResponseTimeMs = 5_000` and the original delay when done.

---

### Step 5 — Verify Exception Handling (Unhealthy with message)

Add a throwing check temporarily:
```csharp
checks.AddAsyncCheck("broken_service", _ =>
    throw new InvalidOperationException("Connection refused"));
```

Restart and hit the endpoint:

```powershell
Invoke-RestMethod -Uri http://localhost:5000/mcp/health -SkipHttpErrorCheck | ConvertTo-Json -Depth 5
```

**Expected:**
```json
{
  "status": "Unhealthy",
  "checks": [
    {
      "name": "broken_service",
      "status": "Unhealthy",
      "description": "Connection refused"
    }
  ]
}
```

> ✅ The exception message appears in `description`, not in a stack trace exposed to the client. The registry catches it and logs it server-side via `ILogger`.

---

### Step 6 — Verify Opt-In: Existing Servers Are Unaffected

Run `TelemetryDemo` (which does **not** call `WithHealthChecks()`):

```powershell
cd c:\pocs\FastMCP\DotnetFastMCP\examples\TelemetryDemo
dotnet run
# In Terminal 2:
(Invoke-WebRequest -Uri http://localhost:5000/mcp/health -SkipHttpErrorCheck).StatusCode
# ✅ Expected: 404
```

This confirms the feature is **truly opt-in** — no endpoint is mapped, no overhead, nothing changes for existing consumers.

---

### Step 7 — Verify MCP Tools Still Work (Regression Check)

```powershell
$body = '{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"ping","arguments":{}}}'
Invoke-RestMethod -Method Post -Uri http://localhost:5000/mcp `
    -ContentType "application/json" -Body $body | ConvertTo-Json
```

**Expected:**
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": {
    "content": [{ "type": "text", "text": "pong from HealthChecksDemo" }],
    "isError": false
  }
}
```

> ✅ Health checks are a completely separate route. They do not interact with the MCP JSON-RPC pipeline.

---

### Step 8 — Verify Diagnostics Can Be Disabled

In `Program.cs`:
```csharp
builder.WithHealthChecks(checks =>
{
    checks.IncludeServerDiagnostics = false; // ← disable metadata
});
```

Restart. The response should have **no `diagnostics` field**:
```json
{
  "status": "Healthy",
  "timestamp": "...",
  "checks": [
    { "name": "mcp_server", "status": "Healthy", "durationMs": 0 }
  ]
}
```

> ✅ The `diagnostics` property is `null` and is serialized with `[JsonIgnore(WhenWritingNull)]`, so it is completely absent from the JSON.

---

### ✅ Validation Summary

| Step | Scenario | Expected Status Code | Result |
|---|---|---|---|
| 2 | All checks pass | 200 | ✅ |
| 3 | One check returns `false` | 503 | ✅ |
| 4 | Check exceeds timeout | 207 | ✅ |
| 5 | Check throws exception | 503 with message | ✅ |
| 6 | No `WithHealthChecks()` called | 404 | ✅ |
| 7 | Tool call regression | Tool executes normally | ✅ |
| 8 | `IncludeServerDiagnostics = false` | No `diagnostics` key | ✅ |

---

## 🧪 Unit Testing the Health Check Engine

The `McpHealthCheckRegistry` can be tested in complete isolation — no HTTP stack, no web server needed.

```csharp
// Tests/Health/McpHealthCheckRegistryTests.cs
using FastMCP.Health;
using FastMCP.Server;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public class McpHealthCheckRegistryTests
{
    private static McpHealthCheckRegistry CreateRegistry(McpHealthCheckOptions options)
    {
        var server = new FastMCPServer("test-server");
        return new McpHealthCheckRegistry(
            options, server, NullLogger<McpHealthCheckRegistry>.Instance);
    }

    [Fact]
    public async Task NoCustomChecks_ReturnsHealthy()
    {
        var registry = CreateRegistry(new McpHealthCheckOptions());
        var result = await registry.RunAllChecksAsync();

        Assert.Equal("Healthy", result.Status);
        Assert.Single(result.Checks); // only the built-in mcp_server check
        Assert.Equal("mcp_server", result.Checks[0].Name);
    }

    [Fact]
    public async Task AllChecksPass_ReturnsHealthy()
    {
        var options = new McpHealthCheckOptions()
            .AddCheck("check_a", () => true)
            .AddCheck("check_b", () => true);

        var result = await CreateRegistry(options).RunAllChecksAsync();

        Assert.Equal("Healthy", result.Status);
        Assert.Equal(3, result.Checks.Count); // mcp_server + 2 custom
    }

    [Fact]
    public async Task OneCheckFails_ReturnsUnhealthy()
    {
        var options = new McpHealthCheckOptions()
            .AddCheck("will_fail", () => false);

        var result = await CreateRegistry(options).RunAllChecksAsync();

        Assert.Equal("Unhealthy", result.Status);
        Assert.Contains(result.Checks, c => c.Name == "will_fail" && c.Status == "Unhealthy");
    }

    [Fact]
    public async Task CheckTimesOut_ReturnsDegraded()
    {
        var options = new McpHealthCheckOptions { MaxResponseTimeMs = 50 };
        options.AddAsyncCheck("slow_check", async ct =>
        {
            await Task.Delay(5_000, ct); // always times out
            return true;
        });

        var result = await CreateRegistry(options).RunAllChecksAsync();

        Assert.Equal("Degraded", result.Status);
        var entry = Assert.Single(result.Checks, c => c.Name == "slow_check");
        Assert.Equal("Degraded", entry.Status);
        Assert.Contains("timed out", entry.Description);
    }

    [Fact]
    public async Task CheckThrows_ReturnsUnhealthyWithMessage()
    {
        var options = new McpHealthCheckOptions();
        options.AddAsyncCheck("bad_check", _ =>
            throw new InvalidOperationException("db is down"));

        var result = await CreateRegistry(options).RunAllChecksAsync();

        Assert.Equal("Unhealthy", result.Status);
        var entry = Assert.Single(result.Checks, c => c.Name == "bad_check");
        Assert.Equal("Unhealthy", entry.Status);
        Assert.Equal("db is down", entry.Description);
    }

    [Fact]
    public async Task DiagnosticsEnabled_IncludesServerMetadata()
    {
        var options = new McpHealthCheckOptions { IncludeServerDiagnostics = true };
        var result = await CreateRegistry(options).RunAllChecksAsync();

        Assert.NotNull(result.Diagnostics);
        Assert.Equal("test-server", result.Diagnostics!.ServerName);
        Assert.False(string.IsNullOrEmpty(result.Diagnostics.FrameworkVersion));
    }

    [Fact]
    public async Task DiagnosticsDisabled_OmitsDiagnosticsFromResponse()
    {
        var options = new McpHealthCheckOptions { IncludeServerDiagnostics = false };
        var result = await CreateRegistry(options).RunAllChecksAsync();

        Assert.Null(result.Diagnostics);
    }
}
```

**Run unit tests:**
```powershell
cd c:\pocs\FastMCP\DotnetFastMCP
dotnet test
```

---

## 📖 Common Integration Patterns

### With LLM Provider Health Check

```csharp
builder.WithHealthChecks(checks =>
{
    checks.AddAsyncCheck("llm_provider", async ct =>
    {
        var llm = app.Services.GetRequiredService<ILLMProvider>();
        return await llm.IsHealthyAsync(ct);
    });
});
```

### With Entity Framework Database

```csharp
builder.WithHealthChecks(checks =>
{
    checks.AddAsyncCheck("database", async ct =>
    {
        var db = app.Services.GetRequiredService<AppDbContext>();
        return await db.Database.CanConnectAsync(ct);
    });
});
```

### With HTTP Dependency (External API)

```csharp
builder.WithHealthChecks(checks =>
{
    checks.AddAsyncCheck("payment_gateway", async ct =>
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        try
        {
            var response = await http.GetAsync("https://api.payments.com/health", ct);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    });
});
```

### Memory Guard (Synchronous)

```csharp
builder.WithHealthChecks(checks =>
{
    // Report degraded if GC heap exceeds 1 GB
    checks.AddCheck("memory", () =>
        GC.GetTotalMemory(false) < 1_073_741_824L);
});
```

### Custom Endpoint Path

```csharp
builder.WithHealthChecks(checks =>
{
    checks.HealthEndpointPath = "/healthz"; // Kubernetes default convention
});
```

---

## 🚀 Production Checklist

- [ ] Call `builder.WithHealthChecks()` in `Program.cs`
- [ ] Add a check for every external dependency (database, cache, LLM, external APIs)
- [ ] Set `MaxResponseTimeMs` to ≤ Kubernetes probe timeout (e.g., 3 000 ms if probe timeout is 5 s)
- [ ] Set `IncludeServerDiagnostics = false` if your security policy prohibits metadata exposure
- [ ] Configure Kubernetes `livenessProbe` and `readinessProbe` pointing to `/mcp/health`
- [ ] Add a Grafana alert for 207 (Degraded) and 503 (Unhealthy) response codes
- [ ] Verify that `GET /mcp/health` returns 404 on servers that do NOT call `WithHealthChecks()` (regression test for opt-in)

---

## 📚 Related Guides

- [Observability Guide](observability-guide.md) — OpenTelemetry metrics and tracing
- [Middleware Interception Guide](middleware-interception-guide.md)
- [Context & Interaction Guide](context-interaction-guide.md)
- [Background Tasks Guide](background-tasks-guide.md)
