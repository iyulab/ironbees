using IronHive.Abstractions.Messages;
using IronHive.Abstractions.Messages.Content;
using IronHive.Abstractions.Tools;

namespace Ironbees.Ironhive.Tests;

/// <summary>
/// IronHive 0.23.0+ carries tool results as <see cref="MessageContent"/> blocks; the streaming
/// <c>ToolCallCompleteChunk</c> still exposes text. These pin how the adapter flattens.
/// </summary>
public class IronhiveAdapterToolOutputTests
{
    [Fact]
    public void FlattenToolOutput_TextBlocks_JoinedWithNewline()
    {
        var output = ToolOutput.Success(new MessageContent[]
        {
            new TextMessageContent { Value = "first" },
            new TextMessageContent { Value = "second" }
        });

        Assert.Equal("first\nsecond", IronhiveAdapter.FlattenToolOutput(output));
    }

    [Fact]
    public void FlattenToolOutput_SingleTextResult_IsUnchanged()
    {
        Assert.Equal("3 results", IronhiveAdapter.FlattenToolOutput(ToolOutput.Success("3 results")));
        Assert.Equal("directory not found", IronhiveAdapter.FlattenToolOutput(ToolOutput.Failure("directory not found")));
    }

    [Fact]
    public void FlattenToolOutput_NonTextBlock_BecomesPlaceholderNamingTheType()
    {
        var output = ToolOutput.Success(new MessageContent[]
        {
            new TextMessageContent { Value = "caption" },
            new ImageMessageContent { Format = ImageFormat.Png, Base64 = "AAAA" }
        });

        var text = IronhiveAdapter.FlattenToolOutput(output);

        Assert.StartsWith("caption", text);
        Assert.Contains("ImageMessageContent omitted", text);
        Assert.DoesNotContain("AAAA", text);
    }

    [Fact]
    public void FlattenToolOutput_Empty_IsEmptyString()
    {
        Assert.Equal(string.Empty, IronhiveAdapter.FlattenToolOutput(new ToolOutput()));
    }
}
