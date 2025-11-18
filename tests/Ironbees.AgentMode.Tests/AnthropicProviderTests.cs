using Ironbees.AgentMode.Configuration;
using Ironbees.AgentMode.Providers;
using Microsoft.Extensions.AI;

namespace Ironbees.AgentMode.Tests;

/// <summary>
/// Unit tests for Anthropic provider (AnthropicProviderFactory and AnthropicChatClientAdapter).
/// Note: These are unit tests that don't require an API key. Integration tests are in samples/AnthropicSample.
/// </summary>
public class AnthropicProviderTests
{
    [Fact]
    public void AnthropicProviderFactory_Provider_ReturnsAnthropicEnum()
    {
        // Arrange
        var factory = new AnthropicProviderFactory();

        // Act
        var provider = factory.Provider;

        // Assert
        Assert.Equal(LLMProvider.Anthropic, provider);
    }

    [Fact]
    public void AnthropicProviderFactory_CreateChatClient_WithInvalidProvider_ThrowsArgumentException()
    {
        // Arrange
        var factory = new AnthropicProviderFactory();
        var config = new LLMConfiguration
        {
            Provider = LLMProvider.OpenAI, // Wrong provider
            ApiKey = "test-key",
            Model = "claude-sonnet-4-20250514"
        };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => factory.CreateChatClient(config));
        Assert.Contains("Invalid provider", exception.Message);
        Assert.Contains("Anthropic", exception.Message);
    }

    [Fact]
    public void AnthropicProviderFactory_CreateChatClient_WithNullApiKey_ThrowsArgumentException()
    {
        // Arrange
        var factory = new AnthropicProviderFactory();
        var config = new LLMConfiguration
        {
            Provider = LLMProvider.Anthropic,
            ApiKey = null!,
            Model = "claude-sonnet-4-20250514"
        };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => factory.CreateChatClient(config));
        Assert.Contains("API key is required", exception.Message);
    }

    [Fact]
    public void AnthropicProviderFactory_CreateChatClient_WithEmptyApiKey_ThrowsArgumentException()
    {
        // Arrange
        var factory = new AnthropicProviderFactory();
        var config = new LLMConfiguration
        {
            Provider = LLMProvider.Anthropic,
            ApiKey = "",
            Model = "claude-sonnet-4-20250514"
        };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => factory.CreateChatClient(config));
        Assert.Contains("API key is required", exception.Message);
    }

    [Fact]
    public void AnthropicProviderFactory_CreateChatClient_WithWhitespaceApiKey_ThrowsArgumentException()
    {
        // Arrange
        var factory = new AnthropicProviderFactory();
        var config = new LLMConfiguration
        {
            Provider = LLMProvider.Anthropic,
            ApiKey = "   ",
            Model = "claude-sonnet-4-20250514"
        };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => factory.CreateChatClient(config));
        Assert.Contains("API key is required", exception.Message);
    }

    [Fact]
    public void AnthropicProviderFactory_CreateChatClient_WithNullModel_ThrowsArgumentException()
    {
        // Arrange
        var factory = new AnthropicProviderFactory();
        var config = new LLMConfiguration
        {
            Provider = LLMProvider.Anthropic,
            ApiKey = "sk-ant-test-key",
            Model = null!
        };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => factory.CreateChatClient(config));
        Assert.Contains("Model name is required", exception.Message);
    }

    [Fact]
    public void AnthropicProviderFactory_CreateChatClient_WithEmptyModel_ThrowsArgumentException()
    {
        // Arrange
        var factory = new AnthropicProviderFactory();
        var config = new LLMConfiguration
        {
            Provider = LLMProvider.Anthropic,
            ApiKey = "sk-ant-test-key",
            Model = ""
        };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => factory.CreateChatClient(config));
        Assert.Contains("Model name is required", exception.Message);
    }

    [Fact]
    public void AnthropicProviderFactory_CreateChatClient_WithValidConfig_ReturnsIChatClient()
    {
        // Arrange
        var factory = new AnthropicProviderFactory();
        var config = new LLMConfiguration
        {
            Provider = LLMProvider.Anthropic,
            ApiKey = "sk-ant-test-key-for-unit-test",
            Model = "claude-sonnet-4-20250514"
        };

        // Act
        var chatClient = factory.CreateChatClient(config);

        // Assert
        Assert.NotNull(chatClient);
        Assert.IsAssignableFrom<IChatClient>(chatClient);
    }

    [Fact]
    public void AnthropicProviderFactory_CreateChatClient_WithSonnet35Model_ReturnsIChatClient()
    {
        // Arrange
        var factory = new AnthropicProviderFactory();
        var config = new LLMConfiguration
        {
            Provider = LLMProvider.Anthropic,
            ApiKey = "sk-ant-test-key",
            Model = "claude-3-5-sonnet-20241022"
        };

        // Act
        var chatClient = factory.CreateChatClient(config);

        // Assert
        Assert.NotNull(chatClient);
        Assert.IsAssignableFrom<IChatClient>(chatClient);
    }

    [Fact]
    public void AnthropicProviderFactory_CreateChatClient_WithOpusModel_ReturnsIChatClient()
    {
        // Arrange
        var factory = new AnthropicProviderFactory();
        var config = new LLMConfiguration
        {
            Provider = LLMProvider.Anthropic,
            ApiKey = "sk-ant-test-key",
            Model = "claude-3-opus-20240229"
        };

        // Act
        var chatClient = factory.CreateChatClient(config);

        // Assert
        Assert.NotNull(chatClient);
        Assert.IsAssignableFrom<IChatClient>(chatClient);
    }

    [Fact]
    public void AnthropicProviderFactory_CreateChatClient_WithHaikuModel_ReturnsIChatClient()
    {
        // Arrange
        var factory = new AnthropicProviderFactory();
        var config = new LLMConfiguration
        {
            Provider = LLMProvider.Anthropic,
            ApiKey = "sk-ant-test-key",
            Model = "claude-3-haiku-20240307"
        };

        // Act
        var chatClient = factory.CreateChatClient(config);

        // Assert
        Assert.NotNull(chatClient);
        Assert.IsAssignableFrom<IChatClient>(chatClient);
    }

    [Fact]
    public void AnthropicProviderFactory_CreateChatClient_WithCustomTemperature_ReturnsIChatClient()
    {
        // Arrange
        var factory = new AnthropicProviderFactory();
        var config = new LLMConfiguration
        {
            Provider = LLMProvider.Anthropic,
            ApiKey = "sk-ant-test-key",
            Model = "claude-sonnet-4-20250514",
            Temperature = 0.9f
        };

        // Act
        var chatClient = factory.CreateChatClient(config);

        // Assert
        Assert.NotNull(chatClient);
        Assert.IsAssignableFrom<IChatClient>(chatClient);
    }

    [Fact]
    public void AnthropicProviderFactory_CreateChatClient_WithCustomMaxTokens_ReturnsIChatClient()
    {
        // Arrange
        var factory = new AnthropicProviderFactory();
        var config = new LLMConfiguration
        {
            Provider = LLMProvider.Anthropic,
            ApiKey = "sk-ant-test-key",
            Model = "claude-sonnet-4-20250514",
            MaxOutputTokens = 8192
        };

        // Act
        var chatClient = factory.CreateChatClient(config);

        // Assert
        Assert.NotNull(chatClient);
        Assert.IsAssignableFrom<IChatClient>(chatClient);
    }

    [Fact]
    public void AnthropicProviderFactory_CreateChatClient_Metadata_HasCorrectProviderName()
    {
        // Arrange
        var factory = new AnthropicProviderFactory();
        var config = new LLMConfiguration
        {
            Provider = LLMProvider.Anthropic,
            ApiKey = "sk-ant-test-key",
            Model = "claude-sonnet-4-20250514"
        };

        // Act
        var chatClient = factory.CreateChatClient(config);
        var adapter = chatClient as AnthropicChatClientAdapter;

        // Assert
        Assert.NotNull(adapter);
        Assert.Equal("Anthropic", adapter!.Metadata.ProviderName);
    }

    [Fact]
    public void AnthropicProviderFactory_CreateChatClient_Metadata_HasCorrectProviderUri()
    {
        // Arrange
        var factory = new AnthropicProviderFactory();
        var config = new LLMConfiguration
        {
            Provider = LLMProvider.Anthropic,
            ApiKey = "sk-ant-test-key",
            Model = "claude-sonnet-4-20250514"
        };

        // Act
        var chatClient = factory.CreateChatClient(config);
        var adapter = chatClient as AnthropicChatClientAdapter;

        // Assert
        Assert.NotNull(adapter);
        Assert.NotNull(adapter!.Metadata.ProviderUri);
        Assert.Equal("https://api.anthropic.com/", adapter.Metadata.ProviderUri.ToString());
    }

    [Fact]
    public void AnthropicProviderFactory_Dispose_DoesNotThrow()
    {
        // Arrange
        var factory = new AnthropicProviderFactory();
        var config = new LLMConfiguration
        {
            Provider = LLMProvider.Anthropic,
            ApiKey = "sk-ant-test-key",
            Model = "claude-sonnet-4-20250514"
        };

        var chatClient = factory.CreateChatClient(config);

        // Act & Assert (should not throw)
        chatClient.Dispose();
    }

    [Fact]
    public void AnthropicProviderFactory_GetService_WithMatchingType_ReturnsClient()
    {
        // Arrange
        var factory = new AnthropicProviderFactory();
        var config = new LLMConfiguration
        {
            Provider = LLMProvider.Anthropic,
            ApiKey = "sk-ant-test-key",
            Model = "claude-sonnet-4-20250514"
        };

        var chatClient = factory.CreateChatClient(config);

        // Act
        var service = chatClient.GetService(chatClient.GetType());

        // Assert
        Assert.NotNull(service);
        Assert.Same(chatClient, service);
    }

    [Fact]
    public void AnthropicProviderFactory_GetService_WithNonMatchingType_ReturnsNull()
    {
        // Arrange
        var factory = new AnthropicProviderFactory();
        var config = new LLMConfiguration
        {
            Provider = LLMProvider.Anthropic,
            ApiKey = "sk-ant-test-key",
            Model = "claude-sonnet-4-20250514"
        };

        var chatClient = factory.CreateChatClient(config);

        // Act
        var service = chatClient.GetService(typeof(string));

        // Assert
        Assert.Null(service);
    }
}
