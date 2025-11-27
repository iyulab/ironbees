using Ironbees.AgentMode.Agents;
using Ironbees.AgentMode.Agents.Samples;
using Microsoft.Extensions.AI;
using Moq;

namespace Ironbees.AgentMode.Tests;

/// <summary>
/// Unit tests for ConversationalAgent base class and sample implementations.
/// </summary>
public class ConversationalAgentTests
{
    private class TestConversationalAgent : ConversationalAgent
    {
        public TestConversationalAgent(IChatClient chatClient, string systemPrompt)
            : base(chatClient, systemPrompt)
        {
        }
    }

    [Fact]
    public void Constructor_WithNullChatClient_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new TestConversationalAgent(null!, "test prompt"));
    }

    [Fact]
    public void Constructor_WithNullSystemPrompt_ThrowsArgumentNullException()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new TestConversationalAgent(mockChatClient.Object, null!));
    }

    [Fact]
    public async Task RespondAsync_WithValidInput_ReturnsResponse()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var expectedResponse = "This is a test response.";

        mockChatClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, expectedResponse)));

        var agent = new TestConversationalAgent(mockChatClient.Object, "Test system prompt");

        // Act
        var response = await agent.RespondAsync("Hello, agent!");

        // Assert
        Assert.Contains(expectedResponse, response);
    }

    [Fact]
    public async Task RespondAsync_WithEmptyMessage_ThrowsArgumentException()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new TestConversationalAgent(mockChatClient.Object, "Test system prompt");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await agent.RespondAsync(""));
    }

    [Fact]
    public async Task RespondAsync_WithWhitespaceMessage_ThrowsArgumentException()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new TestConversationalAgent(mockChatClient.Object, "Test system prompt");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await agent.RespondAsync("   "));
    }

    [Fact]
    public async Task RespondAsync_WithCustomOptions_PassesOptionsToClient()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var customOptions = new ChatOptions
        {
            Temperature = 0.8f,
            MaxOutputTokens = 500
        };

        mockChatClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Response")));

        var agent = new TestConversationalAgent(mockChatClient.Object, "Test system prompt");

        // Act
        var response = await agent.RespondAsync("Test message", customOptions);

        // Assert
        Assert.NotNull(response);
        mockChatClient.Verify(c => c.GetResponseAsync(
            It.IsAny<IList<ChatMessage>>(),
            It.Is<ChatOptions?>(opts => opts != null && opts.Temperature == 0.8f && opts.MaxOutputTokens == 500),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // TODO: Add streaming tests after confirming correct Microsoft.Extensions.AI streaming types
    // [Fact]
    // public async Task StreamResponseAsync_WithValidInput_YieldsChunks()
    // {
    //     // Streaming test implementation pending Microsoft.Extensions.AI type verification
    // }

    [Fact]
    public void CustomerSupportAgent_WithDefaultPrompt_CreatesSuccessfully()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();

        // Act
        var agent = new CustomerSupportAgent(mockChatClient.Object);

        // Assert
        Assert.NotNull(agent);
    }

    [Fact]
    public void CustomerSupportAgent_WithCustomPrompt_CreatesSuccessfully()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var customPrompt = "Custom support agent prompt";

        // Act
        var agent = new CustomerSupportAgent(mockChatClient.Object, customPrompt);

        // Assert
        Assert.NotNull(agent);
    }

    [Fact]
    public async Task CustomerSupportAgent_RespondAsync_WorksCorrectly()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var expectedResponse = "To reset your password, please follow these steps...";

        mockChatClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, expectedResponse)));

        var agent = new CustomerSupportAgent(mockChatClient.Object);

        // Act
        var response = await agent.RespondAsync("How do I reset my password?");

        // Assert
        Assert.Contains(expectedResponse, response);
    }

    [Fact]
    public void DataAnalystAgent_WithDefaultPrompt_CreatesSuccessfully()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();

        // Act
        var agent = new DataAnalystAgent(mockChatClient.Object);

        // Assert
        Assert.NotNull(agent);
    }

    [Fact]
    public void DataAnalystAgent_WithCustomPrompt_CreatesSuccessfully()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var customPrompt = "Custom data analyst prompt";

        // Act
        var agent = new DataAnalystAgent(mockChatClient.Object, customPrompt);

        // Assert
        Assert.NotNull(agent);
    }

    [Fact]
    public async Task DataAnalystAgent_RespondAsync_WorksCorrectly()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var expectedResponse = "SELECT correlation(activity, revenue) FROM user_data...";

        mockChatClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, expectedResponse)));

        var agent = new DataAnalystAgent(mockChatClient.Object);

        // Act
        var response = await agent.RespondAsync("How do I calculate correlation?");

        // Assert
        Assert.Contains(expectedResponse, response);
    }

}
