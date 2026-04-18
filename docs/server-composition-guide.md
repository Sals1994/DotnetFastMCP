# Server Composition

## Overview

**Server Composition** is a powerful feature in DotnetFastMCP 1.7.0+ that allows you to "mount" or "import" other MCP servers into your main server instance. This enables you to build complex systems from smaller, modular components (Micro-MCPs).

Instead of building one monolithic server with hundreds of tools, you can build specialized servers (e.g., a "GitHub Server", a "Database Server", a "File System Server") and compose them together into a single unified endpoint for your AI client.

## Key Features

*   **Modular Architecture**: Break down your MCP tools into logical groups.
*   **Namespacing**: Automatically prefix imported tools to avoid naming conflicts (e.g., `github_createIssue`, `db_executeQuery`).
*   **Zero-Overhead**: Uses internal dictionary refactoring for O(1) performance; no extra network hops or serialization costs.

## Usage

### 1. Basic Import

Use the `AddServer` extension method on `McpServerBuilder`.

```csharp
var mainServer = new FastMCPServer("MainHub");
var builder = McpServerBuilder.Create(mainServer, args);

// Create or load another server instance
var githubServer = new FastMCPServer("GitHubTools");
// ... register tools to githubServer ...

// Import it!
builder.AddServer(githubServer);
```

### 2. Namespacing (Prefixing)

To prevent naming collisions (e.g., two servers both having a `GetStatus` tool) and to organize tools logically, use the `prefix` argument.

```csharp
// Import with "gh" prefix
builder.AddServer(githubServer, prefix: "gh");

// Resulting Tools:
// - gh_CreateIssue
// - gh_GetRepo
```

### 3. Usage Pattern: Class-Based Modules

A common pattern is to define "modules" as static classes and load them into temporary servers before importing.

```csharp
// 1. Define Module
public static class FileSystemModule 
{
    [McpTool] public static string ReadFile(string path) => ...;
}

// 2. Composition
var fsServer = new FastMCPServer("FS");
// ... load module into fsServer ...

builder.AddServer(fsServer, prefix: "fs");
```

## How It Works

When you call `Import` or `AddServer`:
1.  **Tools**: Iterates over the source server's tools.
2.  **Renaming**: Prepends the prefix (if provided) to the tool's name key.
3.  **Registration**: Adds the tool to the main server's `Tools` dictionary.
4.  **Metadata**: Clones and updates the tool's input schema and description.

This "Flattening" strategy means that at runtime, the Client sees a single list of tools. There is no nested routing logic, ensuring maximum compatibility with all MCP clients (Claude, Cursor, generic clients).

## Example Scenario

Imagine a **DevOps Assistant** server:

```csharp
var devOpsServer = new FastMCPServer("DevOps-Assistant");
var builder = McpServerBuilder.Create(devOpsServer, args);

// 1. Mount AWS Tools
var awsServer = CreateAwsServer();
builder.AddServer(awsServer, prefix: "aws");

// 2. Mount Kubernetes Tools
var k8sServer = CreateK8sServer();
builder.AddServer(k8sServer, prefix: "k8s");

// 3. Mount Database Tools
var dbServer = CreateDbServer();
builder.AddServer(dbServer, prefix: "db");

await builder.Build().RunMcpAsync(args);
```

The AI Client will see:
*   `aws_ListInstances`
*   `k8s_GetPods`
*   `db_Query`

This provides a clean, organized, and powerful toolset for the LLM.
