# Changelog

All notable changes to the Ironbees project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- **Microsoft Agent Framework Integration** (v1.1.0)
  - `MicrosoftAgentFrameworkAdapter` for Microsoft Agent Framework execution
  - `MicrosoftAgentWrapper` for AIAgent integration
  - `UseMicrosoftAgentFramework` configuration option
  - Full streaming support via `RunStreamingAsync`
  - Comprehensive documentation at `docs/MICROSOFT_AGENT_FRAMEWORK.md`

### Changed
- `ServiceCollectionExtensions` now supports adapter selection via `UseMicrosoftAgentFramework` option
- Default behavior unchanged (uses Azure.AI.OpenAI ChatClient)

### Dependencies
- Added `Microsoft.Agents.AI.OpenAI` (1.0.0-preview.251028.1)
- Added `Azure.Identity` (1.17.0)
- Added `Microsoft.Extensions.AI.OpenAI` (9.10.1-preview.1.25521.4)

### Planned
- NuGet package publication
- Embedding-based agent selector
- CLI tools for agent management
- Pipeline preprocessors and postprocessors
- Conversation history management
- MCP native tool integration
- Workflow support with Microsoft Agent Framework

## [1.0.0] - 2025-01-29

### Added
- **Core Framework**
  - `IAgent` interface for agent abstraction
  - `IAgentLoader` for loading agent configurations
  - `IAgentRegistry` for thread-safe agent storage
  - `IAgentSelector` for intelligent agent selection
  - `IAgentOrchestrator` for coordinating agent operations
  - `FileSystemAgentLoader` for YAML-based agent loading
  - `AgentRegistry` with thread-safe ConcurrentDictionary storage
  - `KeywordAgentSelector` with weighted multi-factor scoring (capabilities: 40%, tags: 30%, description: 20%, name: 10%)
  - `AgentOrchestrator` for complete orchestration workflow

- **Azure OpenAI Integration**
  - `AgentFrameworkAdapter` using Azure.AI.OpenAI ChatClient
  - `AgentWrapper` for wrapping agent configurations
  - `ServiceCollectionExtensions` for ASP.NET Core DI integration
  - Support for synchronous and streaming responses
  - Configurable model parameters (temperature, max tokens, etc.)

- **Agent System**
  - Convention-over-configuration approach with file structure
  - YAML-based agent configuration (agent.yaml)
  - Markdown system prompts (system-prompt.md)
  - Support for capabilities, tags, and metadata
  - Automatic agent loading from directory structure

- **Example Agents**
  - `coding-agent`: Software development, code generation, and review
  - `writing-agent`: Content writing, editing, and proofreading
  - `analysis-agent`: Data analysis, reporting, and insights
  - `review-agent`: Comprehensive quality review and assessment

- **Examples**
  - BasicUsage console application demonstrating all features
  - Agent selection with confidence scoring
  - Streaming response handling
  - Multi-agent interaction patterns

- **Testing**
  - 67 comprehensive unit tests (36 Core + 31 AgentFramework)
  - Mock-based testing for Azure OpenAI integration
  - Coverage of all major components and scenarios
  - Integration test examples (requires Azure credentials)

- **Documentation**
  - Comprehensive README with Korean and English content
  - Detailed usage guide (USAGE.md) with patterns and examples
  - Agent configuration examples and best practices
  - Architecture diagrams and component descriptions
  - Troubleshooting guide for common issues

### Technical Details

#### Dependencies
- .NET 9.0 target framework
- Azure.AI.OpenAI (2.1.0)
- Microsoft.Extensions.DependencyInjection (9.0.0)
- YamlDotNet (16.2.1) for YAML parsing
- xUnit (2.9.2) for testing
- Moq (4.20.72) for test mocking

#### Architecture
- Clean architecture with clear separation of concerns
- Interface-based design for extensibility
- Dependency injection throughout
- Thread-safe implementations
- Async/await patterns for I/O operations

#### Agent Selection Algorithm
- Keyword extraction with stopword filtering
- Weighted scoring across multiple factors
- Configurable confidence threshold
- Detailed scoring reasons for transparency
- Fallback agent support

### Design Decisions

1. **Convention over Configuration**: Reduced boilerplate by using file structure to define agents
2. **Thin Abstraction Layer**: Framework doesn't hide Azure OpenAI features, just orchestrates them
3. **Extensibility First**: All core components (Loader, Selector, Adapter) are replaceable
4. **Type Safety**: Leveraged C# type system for compile-time safety

### Breaking Changes
- None (initial release)

### Deprecated
- None (initial release)

### Security
- Environment variable-based credential management
- No hardcoded secrets in code or configuration
- Secure Azure OpenAI client integration

### Performance
- Thread-safe concurrent agent registry
- Efficient keyword-based selection algorithm
- Streaming support for large responses
- Minimal overhead orchestration layer

## Version History

### Phase 0: Project Setup (Completed)
- Project structure and organization
- Solution file and project references
- Git repository initialization
- Development environment setup

### Phase 1: Core Abstractions (Completed)
- Interface definitions for all components
- FileSystemAgentLoader implementation
- AgentRegistry with thread-safety
- Basic error handling and exceptions

### Phase 2: Azure OpenAI Integration (Completed)
- ChatClient-based adapter implementation
- Agent wrapper for configuration management
- Service collection extensions for DI
- Streaming and synchronous execution modes

### Phase 3: Intelligent Agent Selection (Completed)
- KeywordAgentSelector with multi-factor scoring
- Confidence threshold configuration
- Detailed selection reasoning
- Fallback agent support
- AgentOrchestrator coordination logic

### Phase 4: Documentation & Production Preparation (In Progress)
- ✅ Comprehensive README
- ✅ Usage guide with examples
- ✅ Example agent configurations
- ✅ CHANGELOG documentation
- 🔄 Preparing for NuGet publication

## Credits

### Contributors
- Ironbees Team - Initial implementation and design

### Inspirations
- Semantic Kernel for multi-agent patterns
- LangChain for agent orchestration concepts
- Azure OpenAI best practices

### Tools and Libraries
- Azure.AI.OpenAI for LLM integration
- YamlDotNet for configuration parsing
- xUnit and Moq for testing infrastructure

## License

MIT License - See [LICENSE](LICENSE) file for details.

## Links

- [GitHub Repository](https://github.com/yourusername/ironbees)
- [Documentation](README.md)
- [Usage Guide](docs/USAGE.md)
- [Issue Tracker](https://github.com/yourusername/ironbees/issues)

---

**Ironbees** - Convention-based multi-agent orchestration for .NET 🐝
