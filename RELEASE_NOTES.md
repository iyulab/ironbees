# Ironbees Release Notes

## Version 1.1.0 (2025-10-30)

### 🎉 Microsoft Agent Framework Integration

Ironbees now supports [Microsoft Agent Framework](https://aka.ms/agent-framework) as an alternative execution engine while maintaining 100% backward compatibility.

#### Key Features

**1. Dual Execution Mode**
- Default: Azure.AI.OpenAI ChatClient (existing behavior)
- Optional: Microsoft Agent Framework AIAgent
- Simple flag to switch: `UseMicrosoftAgentFramework = true`

**2. New Components**
- `MicrosoftAgentFrameworkAdapter` - AIAgent-based execution adapter
- `MicrosoftAgentWrapper` - Seamless AIAgent integration
- Full streaming support via `RunStreamingAsync`

**3. Benefits**
- ✅ Official Microsoft support and long-term maintenance
- ✅ Native MCP (Model Context Protocol) tool integration (future)
- ✅ Advanced workflow capabilities (future)
- ✅ Standards-based approach via Microsoft.Extensions.AI

#### Usage

**Basic (unchanged)**:
```csharp
services.AddIronbees(options =>
{
    options.AzureOpenAIEndpoint = "https://your-resource.openai.azure.com";
    options.AzureOpenAIKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY");
    options.AgentsDirectory = "./agents";
});
```

**With Microsoft Agent Framework**:
```csharp
services.AddIronbees(options =>
{
    options.AzureOpenAIEndpoint = "https://your-resource.openai.azure.com";
    options.AzureOpenAIKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY");
    options.AgentsDirectory = "./agents";
    options.UseMicrosoftAgentFramework = true; // 👈 New!
});
```

### 📦 New Dependencies

- `Microsoft.Agents.AI.OpenAI` 1.0.0-preview.251028.1
- `Azure.Identity` 1.17.0
- `Microsoft.Extensions.AI.OpenAI` 9.10.1-preview.1.25521.4

### 📚 Documentation

- New guide: [docs/MICROSOFT_AGENT_FRAMEWORK.md](docs/MICROSOFT_AGENT_FRAMEWORK.md)
- Updated: README.md with usage examples
- Updated: CHANGELOG.md with detailed changes

### ✅ Quality Assurance

- All 67 tests pass (36 Core + 31 AgentFramework)
- Build: 0 warnings, 0 errors
- 100% backward compatible - no breaking changes

### 🔄 Migration Guide

**No migration needed!** Existing code continues to work without any changes. The new Microsoft Agent Framework support is opt-in.

To try the new feature:
1. Update to v1.1.0
2. Set `UseMicrosoftAgentFramework = true` in configuration
3. Test with your existing agents
4. Enjoy enhanced capabilities!

---

## Version 1.0.0 (2025-01-29)

### Initial Release

Complete multi-agent orchestration framework for .NET with:

#### Core Features
- Convention-based agent loading from filesystem
- Intelligent agent selection via KeywordSelector
- Agent pipeline with sequential/parallel/conditional execution
- Collaboration patterns: Voting, BestOfN, Ensemble, FirstSuccess
- Azure OpenAI integration via ChatClient

#### Built-in Agents
- 5 specialized agents: RAG, Function Calling, Router, Memory, Summarization
- 4 example agents: coding, writing, analysis, review

#### Developer Experience
- ASP.NET Core integration via dependency injection
- Comprehensive documentation and examples
- 67 unit tests with >90% coverage
- Clean architecture with extensible abstractions

#### Technical Highlights
- .NET 9.0 target framework
- Thread-safe concurrent operations
- Async/await throughout
- Nullable reference types enabled

For full details, see [CHANGELOG.md](CHANGELOG.md).

---

## Support

- **Documentation**: [README.md](README.md)
- **Issues**: [GitHub Issues](https://github.com/iyulab-rnd/ironbees/issues)
- **License**: MIT License

---

**Ironbees** - Convention-based multi-agent orchestration for .NET 🐝
