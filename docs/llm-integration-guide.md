# LLM Provider Integration Guide

**DotnetFastMCP** now includes a powerful, extensible LLM integration system that allows your MCP tools to leverage AI capabilities from multiple providers. This guide covers the architecture, supported providers, and how to integrate LLMs into your MCP server.

## 🎯 Overview

The LLM integration system provides:

- ✅ **8 LLM Providers** - Ollama, OpenAI, Azure OpenAI, Anthropic Claude, Google Gemini, Cohere, Hugging Face, Deepseek
- ✅ **Unified Interface** - Single `ILLMProvider` interface for all providers
- ✅ **Plug-and-Play** - Simple extension methods for registration
- ✅ **Streaming Support** - Real-time token streaming with `IAsyncEnumerable<string>`
- ✅ **Resilience** - Built-in retry policies with Polly
- ✅ **Type-Safe** - Full C# type system integration
- ✅ **Production-Ready** - HttpClientFactory, connection pooling, proper resource management

## 🏗️ Architecture

### Core Components

```
FastMCP.AI/
├── ILLMProvider.cs              # Core interface
├── LLMMessage.cs                # Message abstraction
├── LLMGenerationOptions.cs      # Generation parameters
├── LLMProviderExtensions.cs     # Registration extensions
└── Providers/
    ├── OllamaProvider.cs        # Local models
    ├── OpenAIProvider.cs        # GPT models
    ├── AzureOpenAIProvider.cs   # Azure-hosted GPT
    ├── AnthropicProvider.cs     # Claude models (Feb 2026)
    ├── GeminiProvider.cs        # Google Gemini (Feb 2026)
    ├── CohereProvider.cs        # Cohere Command (Feb 2026)
    ├── HuggingFaceProvider.cs   # HF Inference (Feb 2026)
    └── DeepseekProvider.cs      # Deepseek V3.2 (Feb 2026)
```

### Interface Design

All providers implement the `ILLMProvider` interface:

```csharp
public interface ILLMProvider
{
    /// <summary>
    /// Generate a single response from a prompt
    /// </summary>
    Task<string> GenerateAsync(
        string prompt,
        LLMGenerationOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stream tokens in real-time
    /// </summary>
    IAsyncEnumerable<string> StreamAsync(
        string prompt,
        LLMGenerationOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Multi-turn conversation
    /// </summary>
    Task<string> ChatAsync(
        IEnumerable<LLMMessage> messages,
        LLMGenerationOptions? options = null,
        CancellationToken cancellationToken = default);
}
```

## 🚀 Quick Start

### 1. Choose Your Provider

```csharp
using FastMCP.Hosting;
using FastMCP.Server;
using FastMCP.AI;

var server = new FastMCPServer("AI-Powered MCP Server");
var builder = McpServerBuilder.Create(server, args);

// Option 1: Local (Ollama)
builder.AddOllamaProvider(options =>
{
    options.BaseUrl = "http://localhost:11434";
    options.DefaultModel = "llama3.1:8b";
});

// Option 2: Cloud (OpenAI)
builder.AddOpenAIProvider(options =>
{
    options.ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")!;
    options.DefaultModel = "gpt-4";
});

// Option 3: Latest (Anthropic Claude Opus 4.6)
builder.AddAnthropicProvider(options =>
{
    options.ApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")!;
    options.DefaultModel = "claude-opus-4.6";
});

var app = builder.Build();
await app.RunAsync();
```

### 2. Use in MCP Tools

```csharp
using FastMCP.Attributes;
using FastMCP.AI;

public class AITools
{
    private readonly ILLMProvider _llm;

    public AITools(ILLMProvider llm)
    {
        _llm = llm;
    }

    [McpTool("generate_story")]
    public async Task<string> GenerateStory(string topic)
    {
        var options = new LLMGenerationOptions
        {
            SystemPrompt = "You are a creative storyteller.",
            Temperature = 0.8,
            MaxTokens = 500
        };

        return await _llm.GenerateAsync(
            $"Write a short story about {topic}",
            options);
    }

    [McpTool("chat_with_ai")]
    public async Task<string> Chat(string userMessage)
    {
        var messages = new[]
        {
            LLMMessage.System("You are a helpful assistant."),
            LLMMessage.User(userMessage)
        };

        return await _llm.ChatAsync(messages);
    }
}
```

## 📦 Supported Providers

### 1. Ollama (Local Models)

**Best For**: Privacy, offline usage, custom models

```csharp
builder.AddOllamaProvider(options =>
{
    options.BaseUrl = "http://localhost:11434";
    options.DefaultModel = "llama3.1:8b";
    options.TimeoutSeconds = 120;
});
```

**Environment Variables**:
```bash
OLLAMA_BASE_URL=http://localhost:11434
OLLAMA_DEFAULT_MODEL=llama3.1:8b
```

**Popular Models**: `llama3.1:8b`, `phi3`, `mistral`, `codellama`

---

### 2. OpenAI (GPT Models)

**Best For**: Production apps, GPT-4, function calling

```csharp
builder.AddOpenAIProvider(options =>
{
    options.ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")!;
    options.DefaultModel = "gpt-4-turbo";
});
```

**Environment Variables**:
```bash
OPENAI_API_KEY=sk-...
OPENAI_DEFAULT_MODEL=gpt-4-turbo
```

**Popular Models**: `gpt-4-turbo`, `gpt-4`, `gpt-3.5-turbo`

---

### 3. Azure OpenAI

**Best For**: Enterprise, compliance, Azure ecosystem

```csharp
builder.AddAzureOpenAIProvider(options =>
{
    options.Endpoint = "https://your-resource.openai.azure.com";
    options.ApiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")!;
    options.DeploymentName = "gpt-4";
    options.ApiVersion = "2024-02-15-preview";
});
```

**Environment Variables**:
```bash
AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com
AZURE_OPENAI_API_KEY=...
AZURE_OPENAI_DEPLOYMENT_NAME=gpt-4
```

---

### 4. Anthropic Claude (Feb 2026)

**Best For**: Deep reasoning, complex tasks, 1M token context

```csharp
builder.AddAnthropicProvider(options =>
{
    options.ApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")!;
    options.DefaultModel = "claude-opus-4.6"; // Latest (Feb 5, 2026)
    options.InferenceGeo = "us"; // Optional: US-only inference
});
```

**Environment Variables**:
```bash
ANTHROPIC_API_KEY=sk-ant-...
ANTHROPIC_DEFAULT_MODEL=claude-opus-4.6
```

**Available Models** (Feb 2026):
- `claude-opus-4.6` - Most intelligent, 1M context (beta), $5/$25 per 1M tokens
- `claude-sonnet-4.5` - Balanced performance, coding, $3/$15 per 1M tokens
- `claude-haiku-4.5` - Fast, cost-effective, $0.25/$1.25 per 1M tokens

**New Features**:
- ✅ Structured outputs (GA)
- ✅ Compaction API for infinite conversations (beta)
- ✅ Data residency controls

---

### 5. Google Gemini (Feb 2026)

**Best For**: Multimodal, high-volume, low-latency

```csharp
builder.AddGeminiProvider(options =>
{
    options.ApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")!;
    options.DefaultModel = "gemini-3-flash"; // Latest (2026)
    options.ApiVersion = "v1beta"; // Recommended
});
```

**Environment Variables**:
```bash
GEMINI_API_KEY=...
GEMINI_DEFAULT_MODEL=gemini-3-flash
```

**Available Models** (Feb 2026):
- `gemini-3-pro` - Multimodal, agentic, state-of-the-art reasoning
- `gemini-3-flash` - Price/performance balance, 1M context
- `gemini-3-deep-think` - Complex science/research
- `gemini-2.5-pro` - Enterprise stable
- `gemini-2.5-flash` - Cost-efficient, high-volume

---

### 6. Cohere (Feb 2026)

**Best For**: Enterprise RAG, multi-tool use, reranking

```csharp
builder.AddCohereProvider(options =>
{
    options.ApiKey = Environment.GetEnvironmentVariable("COHERE_API_KEY")!;
    options.DefaultModel = "command-a"; // Latest (Mar 2025)
    options.ApiVersion = "v2"; // Required
});
```

**Environment Variables**:
```bash
COHERE_API_KEY=...
COHERE_DEFAULT_MODEL=command-a
```

**Available Models** (Feb 2026):
- `command-a` - Agentic tasks, enterprise efficiency
- `command-a-reasoning` - Complex reasoning, AI agents
- `command-r-plus` - Advanced RAG, multi-tool use

**Note**: Cohere API V2 requires mandatory model versions.

---

### 7. Hugging Face Inference (Feb 2026)

**Best For**: Open-source models, flexibility, 100k+ models

```csharp
builder.AddHuggingFaceProvider(options =>
{
    options.ApiToken = Environment.GetEnvironmentVariable("HF_TOKEN")!;
    options.DefaultModel = "meta-llama/Llama-3.1-8B-Instruct";
    options.TimeoutSeconds = 120; // HF can be slower for cold starts
});
```

**Environment Variables**:
```bash
HF_TOKEN=hf_...
HF_DEFAULT_MODEL=meta-llama/Llama-3.1-8B-Instruct
```

**Popular Models**:
- `meta-llama/Llama-3.1-8B-Instruct`
- `mistralai/Mistral-7B-Instruct-v0.3`
- `google/gemma-7b-it`

**Features**:
- ✅ Access to 100,000+ models
- ✅ Inference Providers (Fal.ai, Replicate, Together AI)
- ✅ Custom Inference Endpoints

---

### 8. Deepseek (Feb 2026)

**Best For**: Cost-effective, reasoning, OpenAI-compatible

```csharp
builder.AddDeepseekProvider(options =>
{
    options.ApiKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY")!;
    options.DefaultModel = "deepseek-chat"; // Fast mode
    // OR: options.DefaultModel = "deepseek-reasoner"; // Thinking mode
});
```

**Environment Variables**:
```bash
DEEPSEEK_API_KEY=...
DEEPSEEK_DEFAULT_MODEL=deepseek-chat
```

**Available Models** (Feb 2026):
- `deepseek-chat` - Non-thinking mode, fast conversations
- `deepseek-reasoner` - Thinking mode, multi-step reasoning
- `deepseek-v3.2-speciale` - Experimental, maximal reasoning

**Features**:
- ✅ 1M token context window
- ✅ OpenAI-compatible API
- ✅ $0.30/$1.20 per 1M tokens

---

## 🔧 Advanced Usage

### Generation Options

```csharp
var options = new LLMGenerationOptions
{
    Model = "gpt-4-turbo",              // Override default model
    SystemPrompt = "You are an expert.", // System instructions
    Temperature = 0.7,                   // Creativity (0.0-2.0)
    MaxTokens = 1000,                    // Max response length
    TopP = 0.9,                          // Nucleus sampling
    StopSequences = new[] { "\n\n" }     // Stop generation
};

var response = await llm.GenerateAsync("Your prompt", options);
```

### Streaming Responses

```csharp
[McpTool("stream_story")]
public async IAsyncEnumerable<string> StreamStory(string topic)
{
    var options = new LLMGenerationOptions
    {
        SystemPrompt = "You are a storyteller.",
        Temperature = 0.8
    };

    await foreach (var token in _llm.StreamAsync($"Tell a story about {topic}", options))
    {
        yield return token;
    }
}
```

### Multi-Turn Conversations

```csharp
var conversation = new List<LLMMessage>
{
    LLMMessage.System("You are a helpful coding assistant."),
    LLMMessage.User("How do I reverse a string in C#?"),
    LLMMessage.Assistant("You can use `string.Reverse()` or a for loop."),
    LLMMessage.User("Show me the for loop version.")
};

var response = await llm.ChatAsync(conversation);
```

### Multiple Providers

```csharp
// Register multiple providers
builder.AddOllamaProvider();  // For local/fast tasks
builder.AddOpenAIProvider();  // For complex reasoning

// Use named services
services.AddSingleton<ILLMProvider>(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    // Return specific provider based on logic
});
```

## 🧪 Validation & Testing

### Unit Testing

```csharp
public class AIToolsTests
{
    [Fact]
    public async Task GenerateStory_ReturnsStory()
    {
        // Arrange
        var mockLLM = new Mock<ILLMProvider>();
        mockLLM.Setup(x => x.GenerateAsync(
            It.IsAny<string>(),
            It.IsAny<LLMGenerationOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync("Once upon a time...");

        var tools = new AITools(mockLLM.Object);

        // Act
        var result = await tools.GenerateStory("dragons");

        // Assert
        Assert.Contains("Once upon a time", result);
    }
}
```

### Integration Testing

```csharp
[Fact]
public async Task Ollama_Integration_Test()
{
    // Requires Ollama running locally
    var options = new OllamaProviderOptions
    {
        BaseUrl = "http://localhost:11434",
        DefaultModel = "llama3.1:8b"
    };

    var httpClient = new HttpClient { BaseAddress = new Uri(options.BaseUrl) };
    var logger = new Mock<ILogger<OllamaProvider>>().Object;
    var provider = new OllamaProvider(httpClient, options, logger);

    var response = await provider.GenerateAsync("Say hello!");

    Assert.NotEmpty(response);
}
```

### Validation Checklist

- ✅ **API Key Validation**: Ensure keys are set before registration
- ✅ **Model Availability**: Verify default model exists
- ✅ **Timeout Configuration**: Set appropriate timeouts for each provider
- ✅ **Error Handling**: Test retry policies with transient failures
- ✅ **Streaming**: Verify streaming works end-to-end
- ✅ **System Prompts**: Test system prompt handling for each provider

## 🔐 Security Best Practices

### API Key Management

```csharp
// ✅ GOOD: Environment variables
var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? throw new InvalidOperationException("API key not set");

// ❌ BAD: Hardcoded
var apiKey = "sk-..."; // NEVER DO THIS
```

### Production Secrets

```bash
# Development: .env file (gitignored)
OPENAI_API_KEY=sk-...
ANTHROPIC_API_KEY=sk-ant-...

# Production: Azure Key Vault, AWS Secrets Manager, etc.
```

### Rate Limiting

```csharp
// Built-in retry policy handles transient errors
// For rate limiting, implement custom middleware
builder.AddMcpMiddleware<RateLimitMiddleware>();
```

## 📊 Performance Optimization

### Connection Pooling

All providers use `HttpClientFactory` for efficient connection pooling:

```csharp
// Automatically configured
services.AddHttpClient(nameof(OpenAIProvider), client =>
{
    client.BaseAddress = new Uri("https://api.openai.com");
    client.Timeout = TimeSpan.FromSeconds(60);
})
.AddPolicyHandler(GetRetryPolicy()); // Polly retry policy
```

### Caching Responses

```csharp
public class CachedLLMProvider : ILLMProvider
{
    private readonly ILLMProvider _inner;
    private readonly IMemoryCache _cache;

    public async Task<string> GenerateAsync(string prompt, ...)
    {
        var cacheKey = $"llm:{prompt.GetHashCode()}";
        if (_cache.TryGetValue(cacheKey, out string? cached))
            return cached!;

        var response = await _inner.GenerateAsync(prompt, ...);
        _cache.Set(cacheKey, response, TimeSpan.FromMinutes(10));
        return response;
    }
}
```

## 🐛 Troubleshooting

### Common Issues

| Issue | Solution |
|-------|----------|
| **Ollama connection refused** | Ensure Ollama is running: `ollama serve` |
| **OpenAI 401 Unauthorized** | Verify API key is valid and has credits |
| **Azure 404 Not Found** | Check deployment name and endpoint URL |
| **Timeout errors** | Increase `TimeoutSeconds` for large models |
| **Streaming not working** | Ensure `stream = true` in request body |
| **System prompt ignored** | Check provider-specific handling (prepend vs native) |

### Debug Logging

```csharp
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Debug);
});
```

## 📚 Examples

### Complete Example: AI-Powered MCP Server

```csharp
using FastMCP.Hosting;
using FastMCP.Server;
using FastMCP.AI;
using FastMCP.Attributes;
using System.Reflection;

// 1. Create Server
var server = new FastMCPServer("AI Assistant");
var builder = McpServerBuilder.Create(server, args);

// 2. Add LLM Provider
builder.AddAnthropicProvider(options =>
{
    options.ApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")!;
    options.DefaultModel = "claude-opus-4.6";
});

// 3. Register Tools
builder.WithComponentsFrom(Assembly.GetExecutingAssembly());

// 4. Build & Run
var app = builder.Build();
await app.RunAsync();

// 5. Define AI Tools
public class AITools
{
    private readonly ILLMProvider _llm;

    public AITools(ILLMProvider llm) => _llm = llm;

    [McpTool("analyze_code")]
    public async Task<string> AnalyzeCode(string code)
    {
        return await _llm.GenerateAsync(
            $"Analyze this code and suggest improvements:\n\n{code}",
            new LLMGenerationOptions
            {
                SystemPrompt = "You are an expert code reviewer.",
                Temperature = 0.3
            });
    }

    [McpTool("generate_tests")]
    public async Task<string> GenerateTests(string code)
    {
        return await _llm.GenerateAsync(
            $"Generate unit tests for:\n\n{code}",
            new LLMGenerationOptions
            {
                SystemPrompt = "You are a testing expert. Generate xUnit tests.",
                MaxTokens = 1000
            });
    }
}
```

## 🔗 Resources

- [Anthropic Claude API Docs](https://docs.anthropic.com/claude/reference)
- [Google Gemini API Docs](https://ai.google.dev/docs)
- [Cohere API Docs](https://docs.cohere.com/)
- [Hugging Face Inference Docs](https://huggingface.co/docs/api-inference)
- [Deepseek API Docs](https://platform.deepseek.com/api-docs)
- [OpenAI API Docs](https://platform.openai.com/docs)
- [Ollama Docs](https://ollama.ai/docs)

## 📝 Summary

The LLM integration system in DotnetFastMCP provides:

1. **8 Production-Ready Providers** - From local (Ollama) to cutting-edge cloud (Claude Opus 4.6, Gemini 3)
2. **Unified Interface** - Single API for all providers
3. **Enterprise Features** - Retry policies, connection pooling, proper resource management
4. **Latest Models** - Feb 2026 API specifications with newest capabilities
5. **Type-Safe & Testable** - Full C# integration with dependency injection

Start building AI-powered MCP servers today! 🚀
