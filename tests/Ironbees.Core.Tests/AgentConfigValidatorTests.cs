using Ironbees.Core;

namespace Ironbees.Core.Tests;

/// <summary>
/// Tests for AgentConfigValidator
/// </summary>
public class AgentConfigValidatorTests
{
    [Fact]
    public void Validate_ValidConfig_ReturnsValid()
    {
        // Arrange
        var config = CreateValidConfig("test-agent");

        // Act
        var result = AgentConfigValidator.Validate(config, "/test/path");

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_MissingName_ReturnsError()
    {
        // Arrange
        var config = CreateValidConfig("test-agent") with { Name = "" };

        // Act
        var result = AgentConfigValidator.Validate(config, "/test/path");

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("name is required"));
    }

    [Fact]
    public void Validate_InvalidNameFormat_ReturnsError()
    {
        // Arrange
        var config = CreateValidConfig("Test_Agent"); // Uppercase and underscore

        // Act
        var result = AgentConfigValidator.Validate(config, "/test/path");

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("lowercase letters, numbers, and hyphens"));
    }

    [Fact]
    public void Validate_ShortDescription_ReturnsWarning()
    {
        // Arrange
        var config = CreateValidConfig("test-agent") with { Description = "Short" };

        // Act
        var result = AgentConfigValidator.Validate(config, "/test/path");

        // Assert
        Assert.True(result.IsValid); // Should still be valid
        Assert.Contains(result.Warnings, w => w.Contains("description is very short"));
    }

    [Fact]
    public void Validate_InvalidVersion_ReturnsError()
    {
        // Arrange
        var config = CreateValidConfig("test-agent") with { Version = "1.0" }; // Not semver

        // Act
        var result = AgentConfigValidator.Validate(config, "/test/path");

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("not a valid semantic version"));
    }

    [Fact]
    public void Validate_ShortSystemPrompt_ReturnsWarning()
    {
        // Arrange
        var config = CreateValidConfig("test-agent") with { SystemPrompt = "Hi" };

        // Act
        var result = AgentConfigValidator.Validate(config, "/test/path");

        // Assert
        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("System prompt is very short"));
    }

    [Fact]
    public void Validate_MissingModel_ReturnsError()
    {
        // Arrange
        var config = CreateValidConfig("test-agent") with { Model = null! };

        // Act
        var result = AgentConfigValidator.Validate(config, "/test/path");

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Model configuration is required"));
    }

    [Fact]
    public void Validate_InvalidTemperature_ReturnsError()
    {
        // Arrange
        var config = CreateValidConfig("test-agent");
        var invalidModel = config.Model with { Temperature = 3.0 }; // Out of range
        config = config with { Model = invalidModel };

        // Act
        var result = AgentConfigValidator.Validate(config, "/test/path");

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("temperature") && e.Contains("out of valid range"));
    }

    [Fact]
    public void Validate_NegativeMaxTokens_ReturnsError()
    {
        // Arrange
        var config = CreateValidConfig("test-agent");
        var invalidModel = config.Model with { MaxTokens = -100 };
        config = config with { Model = invalidModel };

        // Act
        var result = AgentConfigValidator.Validate(config, "/test/path");

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("maxTokens") && e.Contains("must be positive"));
    }

    [Fact]
    public void Validate_VeryHighMaxTokens_ReturnsWarning()
    {
        // Arrange
        var config = CreateValidConfig("test-agent");
        var model = config.Model with { MaxTokens = 150000 };
        config = config with { Model = model };

        // Act
        var result = AgentConfigValidator.Validate(config, "/test/path");

        // Assert
        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("maxTokens") && w.Contains("very high"));
    }

    [Fact]
    public void Validate_InvalidTopP_ReturnsError()
    {
        // Arrange
        var config = CreateValidConfig("test-agent");
        var invalidModel = config.Model with { TopP = 1.5 }; // Out of range
        config = config with { Model = invalidModel };

        // Act
        var result = AgentConfigValidator.Validate(config, "/test/path");

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("topP") && e.Contains("out of valid range"));
    }

    [Fact]
    public void Validate_NoCapabilities_ReturnsWarning()
    {
        // Arrange
        var config = CreateValidConfig("test-agent") with { Capabilities = new List<string>() };

        // Act
        var result = AgentConfigValidator.Validate(config, "/test/path");

        // Assert
        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("no capabilities"));
    }

    [Fact]
    public void Validate_EmptyCapability_ReturnsError()
    {
        // Arrange
        var config = CreateValidConfig("test-agent") with
        {
            Capabilities = new List<string> { "valid-capability", "" }
        };

        // Act
        var result = AgentConfigValidator.Validate(config, "/test/path");

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Capability cannot be empty"));
    }

    [Fact]
    public void Validate_NoTags_ReturnsWarning()
    {
        // Arrange
        var config = CreateValidConfig("test-agent") with { Tags = new List<string>() };

        // Act
        var result = AgentConfigValidator.Validate(config, "/test/path");

        // Assert
        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("no tags"));
    }

    [Fact]
    public void Validate_EmptyTag_ReturnsError()
    {
        // Arrange
        var config = CreateValidConfig("test-agent") with
        {
            Tags = new List<string> { "valid-tag", "" }
        };

        // Act
        var result = AgentConfigValidator.Validate(config, "/test/path");

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Tag cannot be empty"));
    }

    [Fact]
    public void GetFormattedErrors_WithErrors_ReturnsFormattedMessage()
    {
        // Arrange
        var config = CreateValidConfig("test-agent") with { Name = "" };
        var result = AgentConfigValidator.Validate(config, "/test/path");

        // Act
        var formatted = result.GetFormattedErrors();

        // Assert
        Assert.Contains("Validation failed", formatted);
        Assert.Contains("Errors:", formatted);
        Assert.Contains("1.", formatted); // Error numbering
    }

    [Fact]
    public void IsUniqueAgentName_UniqueNames_ReturnsTrue()
    {
        // Arrange
        var existingConfigs = new List<AgentConfig>
        {
            CreateValidConfig("agent-1"),
            CreateValidConfig("agent-2")
        };

        // Act
        var isUnique = AgentConfigValidator.IsUniqueAgentName("agent-3", existingConfigs);

        // Assert
        Assert.True(isUnique);
    }

    [Fact]
    public void IsUniqueAgentName_DuplicateName_ReturnsFalse()
    {
        // Arrange
        var existingConfigs = new List<AgentConfig>
        {
            CreateValidConfig("agent-1"),
            CreateValidConfig("agent-2")
        };

        // Act
        var isUnique = AgentConfigValidator.IsUniqueAgentName("agent-1", existingConfigs);

        // Assert
        Assert.False(isUnique);
    }

    [Fact]
    public void IsUniqueAgentName_CaseInsensitive_ReturnsFalse()
    {
        // Arrange
        var existingConfigs = new List<AgentConfig>
        {
            CreateValidConfig("agent-1")
        };

        // Act
        var isUnique = AgentConfigValidator.IsUniqueAgentName("Agent-1", existingConfigs);

        // Assert
        Assert.False(isUnique);
    }

    // Helper method
    private static AgentConfig CreateValidConfig(string name)
    {
        return new AgentConfig
        {
            Name = name,
            Description = "This is a valid test agent description with enough characters",
            Version = "1.0.0",
            SystemPrompt = "You are a helpful assistant with detailed instructions for handling user queries.",
            Model = new ModelConfig
            {
                Deployment = "gpt-4",
                Temperature = 0.7,
                MaxTokens = 1000,
                TopP = 1.0
            },
            Capabilities = new List<string> { "test-capability" },
            Tags = new List<string> { "test-tag" }
        };
    }
}
