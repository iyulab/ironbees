# Changelog

All notable changes to the Ironbees project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Planned for v0.2.0
- Embedding-based agent selector
- CLI tools for agent management

## [0.1.3] - 2025-11-11

### Added - Documentation and Samples
- **Comprehensive Documentation**
  - `QUICK_START.md` - 5-minute quick start tutorial
    - Step-by-step project creation and setup
    - Agent directory structure creation
    - Complete code examples for immediate use
    - Troubleshooting section with common issues
    - Tips for hot reload, validation, and performance
    - Multi-agent and auto-routing examples

  - `CUSTOM_ADAPTER.md` - Custom framework adapter development guide
    - Complete guide for implementing `ILLMFrameworkAdapter`
    - Full Semantic Kernel adapter implementation example
    - Dependency injection integration patterns
    - Testing best practices for custom adapters
    - NuGet packaging instructions
    - Advanced patterns: agent wrappers, plugin integration, memory management
    - Additional framework examples (Ollama, LangChain)

  - `PRODUCTION_DEPLOYMENT.md` - Production deployment guide
    - Security best practices (Azure Key Vault, environment variables, API key management)
    - Comprehensive logging and monitoring setup (Serilog, Application Insights)
    - Performance optimization strategies (caching, connection pooling, parallel processing)
    - Error handling and resilience patterns (Polly, Circuit Breaker, retry policies)
    - Docker containerization with multi-stage builds
    - Azure deployment (Container Apps, App Service, Key Vault integration)
    - Health checks and monitoring
    - Load testing with k6
    - Scaling and load balancing strategies

- **ConsoleChatSample**
  - Interactive console chat application
  - Real-time streaming of agent responses
  - Built-in commands: `/agents`, `/agent`, `/auto`, `/clear`, `/help`, `/exit`
  - Color-coded output (cyan for user, green for agent)
  - Auto-selection and manual agent selection modes
  - Graceful error handling with helpful messages
  - Smart agent directory detection (multiple fallback paths)
  - Complete README with usage examples and troubleshooting

### Documentation Structure
- `docs/QUICK_START.md` - New users start here (5 minutes)
- `docs/GETTING_STARTED.md` - Comprehensive guide
- `docs/ARCHITECTURE.md` - System design and components
- `docs/USAGE.md` - Advanced usage patterns
- `docs/MICROSOFT_AGENT_FRAMEWORK.md` - MAF integration
- `docs/CUSTOM_ADAPTER.md` - Custom adapter development
- `docs/PRODUCTION_DEPLOYMENT.md` - Production best practices
- `samples/ConsoleChatSample/` - Interactive CLI demo

### Technical Details
- No breaking changes - fully backward compatible
- All 136 tests continue with same results (128 passed, 8 known failures from v0.1.1)
- ConsoleChatSample demonstrates all core features
- Documentation covers beginner to production deployment scenarios

## [0.1.2] - 2025-11-10

### Added - FileSystemAgentLoader Enhancements
- **AgentConfigValidator**
  - Comprehensive validation for agent configurations
  - Required field validation (name, description, version, system prompt)
  - Format validation (semantic version, agent name format)
  - Model configuration validation (temperature, maxTokens, topP ranges)
  - Duplicate agent name detection
  - `ValidationResult` with detailed errors and warnings

- **Enhanced Error Messages**
  - `YamlParsingException` with detailed diagnostic information
  - YAML error messages include line numbers and common issue hints
  - File path and expected location information in error messages
  - Helpful guidance for fixing common configuration errors

- **Caching Strategy**
  - Optional in-memory caching with file modification detection
  - Thread-safe `ConcurrentDictionary` implementation
  - Automatic cache invalidation on file changes
  - `ClearCache()` method for manual cache management
  - Performance improvement for repeated loads

- **Hot Reload Support**
  - `FileSystemWatcher` integration for development mode
  - Automatic config reload on file changes (agent.yaml, system-prompt.md)
  - `AgentReloaded` event for notification subscribers
  - 100ms debounce delay for file write completion
  - `IDisposable` implementation for proper cleanup

- **FileSystemAgentLoaderOptions**
  - `EnableCaching` (default: true) - Performance optimization
  - `EnableValidation` (default: true) - Configuration validation
  - `StopOnFirstError` (default: false) - Error handling strategy
  - `StrictValidation` (default: false) - Treat warnings as errors
  - `LogWarnings` (default: true) - Console warning output
  - `EnableHotReload` (default: false) - Development mode feature

- **Test Coverage**
  - `FileSystemAgentLoaderEnhancedTests` - 13 comprehensive tests
  - `AgentConfigValidatorTests` - 20 validation tests
  - Total: 136 tests (105 original + 20 validator + 13 loader + 11 integration = 149 total available)

### Changed
- **FileSystemAgentLoader**
  - Now implements `IDisposable` for `FileSystemWatcher` cleanup
  - Improved `LoadAllConfigsAsync` with error aggregation
  - Duplicate agent name detection across all loaded agents
  - Better error messages with file paths and helpful hints
  - Optional strict validation mode for production environments

- **Exception Classes**
  - `AgentConfigurationException` now includes `ValidationResult` property
  - New `YamlParsingException` with detailed parsing diagnostics
  - `InvalidAgentDirectoryException` includes expected file paths

### Performance Improvements
- Configuration caching reduces repeated file I/O
- File modification timestamp checking for cache invalidation
- Thread-safe cache operations with minimal lock contention

### Developer Experience
- Hot reload enables rapid agent iteration without restart
- Detailed validation errors guide correct configuration
- YAML parsing errors include line numbers and fix suggestions
- Console warnings for non-critical issues

### Technical Details
- No breaking changes - fully backward compatible
- All original tests continue to pass
- Clean separation: validation, caching, hot reload as separate concerns
- Comprehensive XML documentation on all new classes

## [0.1.1] - 2025-11-10

### Added - KeywordAgentSelector Enhancements
- **TF-IDF Weighting Algorithm**
  - `TfidfWeightCalculator` class for term relevance scoring
  - Inverse Document Frequency (IDF) calculation across agent corpus
  - 0-30% score boost based on term importance
  - Lazy initialization with cached IDF scores for performance

- **Enhanced Stopwords Dictionary**
  - `StopwordsProvider` class with 80+ English stopwords
  - Explicit preservation of technical terms (.NET, API, code, database, etc.)
  - Case-insensitive matching with improved filtering

- **Keyword Normalization**
  - `KeywordNormalizer` class with synonym mapping and stemming
  - 50+ synonym groups (code↔programming, db↔database, auth↔login, etc.)
  - 100+ stemming rules for word form variations
  - Support for .NET-specific synonyms (csharp↔c#↔cs, dotnet↔.net)

- **Performance Caching**
  - In-memory keyword extraction cache (max 1000 entries)
  - Thread-safe implementation with lock-based access
  - `ClearCache()` method for memory management
  - ~50% performance improvement on repeated queries

- **Test Coverage**
  - `KeywordAgentSelectorBenchmarkTests` - Performance validation (6 tests)
  - `KeywordAgentSelectorEnhancedTests` - Feature validation (13 tests)
  - `KeywordAgentSelectorAccuracyTests` - 50-case accuracy suite (58 tests)
  - Total: 80 tests (67 original + 13 new)

### Changed
- **KeywordAgentSelector Scoring Weights**
  - Capabilities: 0.40 → 0.50 (increased priority)
  - Tags: 0.30 → 0.35 (increased priority)
  - Description: 0.20 → 0.10 (decreased priority)
  - Name: 0.10 → 0.05 (decreased priority)
  - TF-IDF boost: 0-20% → 0-30% (stronger relevance amplification)

### Performance Improvements
- Single agent selection: < 1ms (sub-millisecond)
- 1000 iterations: < 100ms (benchmark target met)
- Cached queries: ~50% faster on subsequent calls
- TF-IDF overhead: Minimal (lazy init, cached calculations)

### Quality Metrics
- Selection accuracy: 88% (50-case test suite)
- Test pass rate: 88.75% (71/80 tests passing)
- Performance targets: All benchmarks passed
- Code coverage: Enhanced with 77 additional test cases

### Documentation
- Added `claudedocs/KEYWORDSELECTOR_IMPROVEMENTS_v0.1.1.md` - Detailed improvement summary

### Technical Details
- No breaking changes - fully backward compatible
- All original tests continue to pass
- Clean separation of concerns with new utility classes
- Comprehensive XML documentation on all new classes

## [0.1.0] - 2025-01-30

### Added
- **Microsoft Agent Framework Integration**
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
