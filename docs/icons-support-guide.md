# Icons Support Guide

## Overview

**Icons Support** (v1.10.0+) allows MCP servers to provide visual identities for the Server itself, as well as individual Tools, Resources, and Prompts. This enables generic MCP clients (like Claude Desktop) to render a richer, more intuitive user interface.

## Usage

### 1. Server Icon

You can define a global icon for your MCP server instance.

```csharp
var server = new FastMCPServer("My Server");
server.Icon = "https://example.com/logo.png"; // Set the server icon
```

### 2. Tool Icons

Use the `Icon` property on the `[McpTool]` attribute.

```csharp
[McpTool(Icon = "https://example.com/tools/calculator.png")]
public static int Add(int a, int b) => a + b;
```

### 3. Resource Icons

Use the `Icon` property on the `[McpResource]` attribute.

```csharp
[McpResource("docs/readme", Icon = "https://example.com/resources/doc.png")]
public static string GetReadme() => "Content...";
```

### 4. Prompt Icons

Use the `Icon` property on the `[McpPrompt]` attribute.

```csharp
[McpPrompt("summarize", Icon = "https://example.com/prompts/text.png")]
public static GetPromptResult GetSummaryPrompt() { ... }
```

## Validation & Testing

To test icons manually, you can use the Stdio transport and inspect the JSON responses.

### Test Case 1: Server Icon
**Request:** `initialize`
**Expected Result:** The `result.server` object should contain `"icon": "..."`.

### Test Case 2: Tool Icon
**Request:** `tools/list`
**Expected Result:** Each tool object should contain `"icon": "..."`.

### Test Case 3: Resource Icon
**Request:** `resources/list`
**Expected Result:** Each resource object should contain `"icon": "..."`.
