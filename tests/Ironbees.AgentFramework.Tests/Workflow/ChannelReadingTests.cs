using System.Threading.Channels;
using Ironbees.AgentFramework.Workflow;
using Xunit;

namespace Ironbees.AgentFramework.Tests.Workflow;

/// <summary>
/// A streaming method's caller that cancels gets no event afterwards. <c>ChannelReader.ReadAllAsync</c> drains every item a
/// fast producer already buffered, ignoring the token, and throws only when it next waits.
/// </summary>
public sealed class ChannelReadingTests
{
    [Fact]
    public async Task ReadUntilCancelled_StopsAtTheCancellation_EvenWithItemsBuffered()
    {
        var channel = await BufferedAsync(100);
        using var cts = new CancellationTokenSource();
        var read = new List<int>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var item in channel.Reader.ReadUntilCancelledAsync(cts.Token))
            {
                read.Add(item);
                if (read.Count == 5)
                    await cts.CancelAsync();
            }
        });

        Assert.Equal([0, 1, 2, 3, 4], read);
    }

    [Fact]
    public async Task ReadAllAsync_DrainsTheBuffer_BeforeTheCancellationSurfaces()
    {
        // The behaviour the helper replaces, pinned: if this changes, the BCL fixed it and the helper may be redundant.
        var channel = await BufferedAsync(100);
        using var cts = new CancellationTokenSource();
        var read = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var item in channel.Reader.ReadAllAsync(cts.Token))
            {
                if (++read == 5)
                    await cts.CancelAsync();
            }
        });

        Assert.Equal(100, read);
    }

    [Fact]
    public async Task ReadUntilCancelled_ReadsEverything_WhenNotCancelled()
    {
        var channel = await BufferedAsync(10);
        var read = new List<int>();

        await foreach (var item in channel.Reader.ReadUntilCancelledAsync(TestContext.Current.CancellationToken))
            read.Add(item);

        Assert.Equal(Enumerable.Range(0, 10), read);
    }

    private static async Task<Channel<int>> BufferedAsync(int count)
    {
        var channel = Channel.CreateUnbounded<int>();
        for (var i = 0; i < count; i++)
            await channel.Writer.WriteAsync(i, TestContext.Current.CancellationToken);
        channel.Writer.Complete();
        return channel;
    }
}
