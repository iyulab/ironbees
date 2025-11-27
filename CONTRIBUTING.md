# Contributing to Ironbees

Thank you for your interest in contributing to Ironbees! This document provides guidelines and instructions for contributing to the project.

## Table of Contents

- [Getting Started](#getting-started)
- [Development Setup](#development-setup)
- [Project Structure](#project-structure)
- [Coding Standards](#coding-standards)
- [Testing](#testing)
- [Submitting Changes](#submitting-changes)
- [Building and Running](#building-and-running)

## Getting Started

### Prerequisites

- .NET 9.0 SDK or later
- Git
- Your favorite IDE (Visual Studio 2022, VS Code, or JetBrains Rider recommended)
- OpenAI API key for running samples (optional)

### Fork and Clone

1. Fork the repository on GitHub
2. Clone your fork locally:
   ```bash
   git clone https://github.com/YOUR-USERNAME/ironbees.git
   cd ironbees
   ```
3. Add the upstream repository:
   ```bash
   git remote add upstream https://github.com/iyulab/ironbees.git
   ```

## Development Setup

### Environment Configuration

For running samples, create a `.env` file in the project root:

```env
OPENAI_API_KEY=your_api_key_here
OPENAI_MODEL=gpt-4
```

### Build the Solution

```bash
dotnet restore
dotnet build
```

### Run Tests

```bash
dotnet test
```

All tests should pass before submitting a pull request.

## Project Structure

```
ironbees/
├── src/
│   ├── Ironbees.Core/           # Core framework (agents, orchestration, pipelines)
│   └── Ironbees.AgentFramework/  # Legacy/alternative agent framework
├── samples/
│   ├── Ironbees.Samples.Shared/  # Shared code for samples (OpenAIAdapter)
│   ├── OpenAISample/             # Basic OpenAI integration example
│   ├── PipelineSample/           # Agent pipeline demonstrations
│   ├── BuiltinAgentsTest/        # Built-in agent usage examples
│   └── WebApiSample/             # REST API integration example
├── tests/
│   ├── Ironbees.Core.Tests/
│   └── Ironbees.AgentFramework.Tests/
└── docs/                         # Documentation and guides
```

## Coding Standards

### C# Style Guidelines

- Follow [Microsoft C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use meaningful names for classes, methods, and variables
- Add XML documentation comments for public APIs
- Keep methods focused and concise (Single Responsibility Principle)

### Code Quality

- **No Console.WriteLine in Core Libraries**: Use dependency injection for logging (ILogger)
- **Exception Handling**: Don't swallow exceptions silently; log or rethrow appropriately
- **Async/Await**: Use async methods consistently; don't mix blocking and async code
- **Nullability**: Enable nullable reference types and handle null cases properly
- **Dispose Pattern**: Implement IDisposable for resources requiring cleanup

### Example

```csharp
namespace Ironbees.Core;

/// <summary>
/// Manages agent lifecycle and coordination
/// </summary>
public class AgentManager : IDisposable
{
    private readonly ILogger<AgentManager> _logger;
    private readonly IAgentRegistry _registry;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentManager"/> class
    /// </summary>
    /// <param name="logger">Logger for diagnostic messages</param>
    /// <param name="registry">Agent registry for managing agents</param>
    public AgentManager(ILogger<AgentManager> logger, IAgentRegistry registry)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    /// <summary>
    /// Loads an agent asynchronously
    /// </summary>
    public async Task<AgentConfig> LoadAgentAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        _logger.LogInformation("Loading agent from {Path}", path);

        // Implementation

        return config;
    }

    public void Dispose()
    {
        // Cleanup resources
    }
}
```

## Testing

### Writing Tests

- Use xUnit for test framework
- Follow AAA pattern (Arrange, Act, Assert)
- Use descriptive test method names
- Test both success and failure scenarios
- Use mocks for external dependencies (Moq library)

### Test Example

```csharp
public class AgentLoaderTests
{
    [Fact]
    public async Task LoadConfigAsync_ValidDirectory_ReturnsConfig()
    {
        // Arrange
        var loader = new FileSystemAgentLoader();
        var testPath = "path/to/test/agent";

        // Act
        var config = await loader.LoadConfigAsync(testPath);

        // Assert
        Assert.NotNull(config);
        Assert.Equal("test-agent", config.Name);
    }

    [Fact]
    public async Task LoadConfigAsync_InvalidDirectory_ThrowsException()
    {
        // Arrange
        var loader = new FileSystemAgentLoader();
        var invalidPath = "nonexistent/path";

        // Act & Assert
        await Assert.ThrowsAsync<InvalidAgentDirectoryException>(
            () => loader.LoadConfigAsync(invalidPath));
    }
}
```

### Test Coverage

- Aim for >80% code coverage on core libraries
- All public APIs should have tests
- Run tests before submitting PR:
  ```bash
  dotnet test --collect:"XPlat Code Coverage"
  ```

## Submitting Changes

### Branch Naming

Use descriptive branch names:
- `feature/add-azure-openai-support`
- `fix/agent-loading-error`
- `docs/update-readme`
- `refactor/improve-pipeline-api`

### Commit Messages

Write clear, descriptive commit messages:

```
Add Azure OpenAI adapter implementation

- Implement AzureOpenAIAdapter class
- Add configuration options for Azure endpoints
- Include tests for Azure-specific features
- Update documentation with Azure setup guide
```

### Pull Request Process

1. **Update from upstream**:
   ```bash
   git fetch upstream
   git rebase upstream/main
   ```

2. **Ensure tests pass**:
   ```bash
   dotnet test
   ```

3. **Clean build artifacts**:
   ```bash
   dotnet clean
   ```

4. **Push to your fork**:
   ```bash
   git push origin feature/your-feature-name
   ```

5. **Create Pull Request** on GitHub with:
   - Clear description of changes
   - Link to related issues
   - Screenshots/examples if applicable
   - Checklist of completed items

### PR Checklist

- [ ] Code follows project style guidelines
- [ ] Tests added/updated and passing
- [ ] Documentation updated (README, XML comments)
- [ ] No breaking changes (or clearly documented)
- [ ] Commit messages are clear and descriptive
- [ ] Branch is up to date with main

## Building and Running

### Build Configuration

```bash
# Debug build (default)
dotnet build

# Release build
dotnet build -c Release
```

### Running Samples

```bash
# OpenAI Sample
cd samples/OpenAISample
dotnet run

# Pipeline Sample
cd samples/PipelineSample
dotnet run

# Built-in Agents Test
cd samples/BuiltinAgentsTest
dotnet run

# Web API Sample
cd samples/WebApiSample/Ironbees.WebApi
dotnet run
```

### Running Specific Tests

```bash
# All tests
dotnet test

# Specific test project
dotnet test tests/Ironbees.Core.Tests

# Specific test class
dotnet test --filter FullyQualifiedName~AgentLoaderTests

# Specific test method
dotnet test --filter FullyQualifiedName~AgentLoaderTests.LoadConfigAsync_ValidDirectory_ReturnsConfig
```

## Code Review Guidelines

When reviewing pull requests:

- **Functionality**: Does the code work as intended?
- **Tests**: Are there adequate tests covering the changes?
- **Style**: Does it follow project coding standards?
- **Documentation**: Are public APIs documented?
- **Performance**: Are there any obvious performance issues?
- **Security**: Are there security concerns or vulnerabilities?

## Getting Help

- **Issues**: Check existing issues or create a new one
- **Discussions**: Use GitHub Discussions for questions
- **Documentation**: Refer to docs/ directory for detailed guides

## License

By contributing, you agree that your contributions will be licensed under the same license as the project.

---

Thank you for contributing to Ironbees! 🐝
