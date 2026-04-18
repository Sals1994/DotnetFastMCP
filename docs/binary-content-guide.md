# Binary Content Support Guide

## Overview

**Binary Content Support** (v1.11.0+) enables standard MCP tools and prompts to return rich content types, specifically **Images** and **Embedded Resources**, in addition to standard text. This feature is critical for building multimodal agents that can process and understand visual information.

## Usage

### 1. Returning Images from Tools

To return an image from a tool, return a `CallToolResult` containing an `ImageContent` item.

```csharp
[McpTool("get_screenshot")]
public static CallToolResult GetScreenshot()
{
    // Load image data (Base64)
    string base64Data = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKwM+AAAAABJRU5ErkJggg==";
    
    return new CallToolResult 
    {
        Content = new List<ContentItem>
        {
            new TextContent { Text = "Here is the screenshot:" },
            new ImageContent 
            { 
                Data = base64Data, 
                MimeType = "image/png" 
            }
        }
    };
}
```

### 2. Using Images in Prompts

Prompts can now include images in the `messages` list, providing context to the LLM.

```csharp
[McpPrompt("analyze_diagram")]
public static GetPromptResult AnalyzeDiagram()
{
    return new GetPromptResult
    {
        Description = "Analyze the architecture diagram",
        Messages = new List<PromptMessage>
        {
            new PromptMessage 
            { 
                Role = "user", 
                Content = new ImageContent 
                { 
                    Data = "...", 
                    MimeType = "image/png" 
                } 
            },
            new PromptMessage 
            { 
                Role = "user", 
                Content = new TextContent { Text = "What architectural pattern is shown here?" } 
            }
        }
    };
}
```

## Validation & Testing

You can verify binary content support using the Stdio transport.

### Test Case 1: Tool Returning Image
**Request:** `tools/call` for a tool returning an image.
**Expected Result:** The `result.content` array should contain an object where `"type": "image"`, `"data": "..."`, and `"mimeType": "..."`.

### Test Case 2: Prompt with Image
**Request:** `prompts/get` for a prompt with an image.
**Expected Result:** The `messages` array should contain a message where `content.type` is `"image"`.
