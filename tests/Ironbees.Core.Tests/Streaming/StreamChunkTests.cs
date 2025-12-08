using Ironbees.Core.Streaming;

namespace Ironbees.Core.Tests.Streaming;

public class StreamChunkTests
{
    [Fact]
    public void TextChunk_CreatesWithContent()
    {
        // Act
        var chunk = new TextChunk("Hello, world!");

        // Assert
        Assert.Equal("Hello, world!", chunk.Content);
        Assert.False(chunk.IsComplete);
        Assert.True(chunk.Timestamp <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public void TextChunk_CreatesWithIsComplete()
    {
        // Act
        var chunk = new TextChunk("Final", IsComplete: true);

        // Assert
        Assert.Equal("Final", chunk.Content);
        Assert.True(chunk.IsComplete);
    }

    [Fact]
    public void ToolCallStartChunk_CreatesWithRequiredProperties()
    {
        // Act
        var chunk = new ToolCallStartChunk("my_tool");

        // Assert
        Assert.Equal("my_tool", chunk.ToolName);
        Assert.Null(chunk.ToolCallId);
        Assert.Null(chunk.Arguments);
    }

    [Fact]
    public void ToolCallStartChunk_CreatesWithAllProperties()
    {
        // Arrange
        var args = new Dictionary<string, object> { ["param1"] = "value1", ["param2"] = 42 };

        // Act
        var chunk = new ToolCallStartChunk("my_tool", "call-123", args);

        // Assert
        Assert.Equal("my_tool", chunk.ToolName);
        Assert.Equal("call-123", chunk.ToolCallId);
        Assert.Equal(2, chunk.Arguments!.Count);
        Assert.Equal("value1", chunk.Arguments["param1"]);
    }

    [Fact]
    public void ToolCallCompleteChunk_CreatesWithSuccess()
    {
        // Act
        var chunk = new ToolCallCompleteChunk("my_tool", "call-123", Success: true, Result: "Success result");

        // Assert
        Assert.Equal("my_tool", chunk.ToolName);
        Assert.Equal("call-123", chunk.ToolCallId);
        Assert.True(chunk.Success);
        Assert.Equal("Success result", chunk.Result);
        Assert.Null(chunk.Error);
    }

    [Fact]
    public void ToolCallCompleteChunk_CreatesWithError()
    {
        // Act
        var chunk = new ToolCallCompleteChunk("my_tool", "call-123", Success: false, Error: "Something went wrong");

        // Assert
        Assert.Equal("my_tool", chunk.ToolName);
        Assert.False(chunk.Success);
        Assert.Equal("Something went wrong", chunk.Error);
    }

    [Fact]
    public void UsageChunk_CalculatesTotalTokens()
    {
        // Act
        var chunk = new UsageChunk(InputTokens: 100, OutputTokens: 50);

        // Assert
        Assert.Equal(100, chunk.InputTokens);
        Assert.Equal(50, chunk.OutputTokens);
        Assert.Equal(150, chunk.TotalTokens);
    }

    [Fact]
    public void UsageChunk_UsesProvidedTotalTokens()
    {
        // Act
        var chunk = new UsageChunk(InputTokens: 100, OutputTokens: 50, TotalTokens: 200);

        // Assert
        Assert.Equal(200, chunk.TotalTokens);
    }

    [Fact]
    public void ErrorChunk_CreatesWithNonFatalError()
    {
        // Act
        var chunk = new ErrorChunk("Minor error occurred");

        // Assert
        Assert.Equal("Minor error occurred", chunk.Error);
        Assert.False(chunk.IsFatal);
        Assert.Null(chunk.ErrorCode);
    }

    [Fact]
    public void ErrorChunk_CreatesWithFatalError()
    {
        // Act
        var chunk = new ErrorChunk("Critical error", IsFatal: true, ErrorCode: "ERR-500");

        // Assert
        Assert.Equal("Critical error", chunk.Error);
        Assert.True(chunk.IsFatal);
        Assert.Equal("ERR-500", chunk.ErrorCode);
    }

    [Fact]
    public void ProgressChunk_CreatesWithPercentage()
    {
        // Act
        var chunk = new ProgressChunk(Percentage: 50);

        // Assert
        Assert.Equal(50, chunk.Percentage);
        Assert.Null(chunk.Message);
        Assert.Null(chunk.CurrentStep);
        Assert.Null(chunk.TotalSteps);
    }

    [Fact]
    public void ProgressChunk_CreatesWithAllProperties()
    {
        // Act
        var chunk = new ProgressChunk(
            Percentage: 75,
            Message: "Processing...",
            CurrentStep: "Step 3",
            TotalSteps: 4);

        // Assert
        Assert.Equal(75, chunk.Percentage);
        Assert.Equal("Processing...", chunk.Message);
        Assert.Equal("Step 3", chunk.CurrentStep);
        Assert.Equal(4, chunk.TotalSteps);
    }

    [Fact]
    public void ThinkingChunk_CreatesWithContent()
    {
        // Act
        var chunk = new ThinkingChunk("Let me think about this...");

        // Assert
        Assert.Equal("Let me think about this...", chunk.Content);
        Assert.False(chunk.IsComplete);
    }

    [Fact]
    public void ThinkingChunk_CreatesWithIsComplete()
    {
        // Act
        var chunk = new ThinkingChunk("Final thought", IsComplete: true);

        // Assert
        Assert.True(chunk.IsComplete);
    }

    [Fact]
    public void MetadataChunk_CreatesWithKeyValue()
    {
        // Act
        var chunk = new MetadataChunk("model_id", "gpt-4");

        // Assert
        Assert.Equal("model_id", chunk.Key);
        Assert.Equal("gpt-4", chunk.Value);
    }

    [Fact]
    public void MetadataChunk_AcceptsComplexValue()
    {
        // Arrange
        var complexValue = new { Name = "Test", Count = 42 };

        // Act
        var chunk = new MetadataChunk("complex_data", complexValue);

        // Assert
        Assert.Equal("complex_data", chunk.Key);
        Assert.NotNull(chunk.Value);
    }

    [Fact]
    public void CompletionChunk_CreatesWithDefaults()
    {
        // Act
        var chunk = new CompletionChunk();

        // Assert
        Assert.True(chunk.Success);
        Assert.Null(chunk.FinishReason);
    }

    [Fact]
    public void CompletionChunk_CreatesWithFinishReason()
    {
        // Act
        var chunk = new CompletionChunk(Success: true, FinishReason: "stop");

        // Assert
        Assert.True(chunk.Success);
        Assert.Equal("stop", chunk.FinishReason);
    }

    [Fact]
    public void CompletionChunk_CreatesWithFailure()
    {
        // Act
        var chunk = new CompletionChunk(Success: false, FinishReason: "length");

        // Assert
        Assert.False(chunk.Success);
        Assert.Equal("length", chunk.FinishReason);
    }

    [Fact]
    public void AllChunks_HaveTimestamp()
    {
        // Arrange
        var beforeCreation = DateTimeOffset.UtcNow.AddMilliseconds(-1);

        // Act
        var chunks = new StreamChunk[]
        {
            new TextChunk("text"),
            new ToolCallStartChunk("tool"),
            new ToolCallCompleteChunk("tool"),
            new UsageChunk(10, 20),
            new ErrorChunk("error"),
            new ProgressChunk(50),
            new ThinkingChunk("thought"),
            new MetadataChunk("key", "value"),
            new CompletionChunk()
        };

        // Assert
        foreach (var chunk in chunks)
        {
            Assert.True(chunk.Timestamp > beforeCreation);
            Assert.True(chunk.Timestamp <= DateTimeOffset.UtcNow);
        }
    }

    [Fact]
    public void StreamChunks_CanBePatternMatched()
    {
        // Arrange
        StreamChunk[] chunks =
        [
            new TextChunk("Hello"),
            new ToolCallStartChunk("my_tool"),
            new ErrorChunk("Error"),
            new CompletionChunk()
        ];

        // Act & Assert
        foreach (var chunk in chunks)
        {
            var result = chunk switch
            {
                TextChunk tc => $"Text: {tc.Content}",
                ToolCallStartChunk tcs => $"Tool: {tcs.ToolName}",
                ErrorChunk ec => $"Error: {ec.Error}",
                CompletionChunk cc => $"Complete: {cc.Success}",
                _ => "Unknown"
            };

            Assert.DoesNotContain("Unknown", result);
        }
    }
}
