using Ironbees.Core;
using Ironbees.Core.Streaming;
using Ironbees.Ironhive.Orchestration;
using IronHive.Abstractions;
using IronHive.Abstractions.Messages;
using IronHive.Abstractions.Messages.Content;
using IronHive.Abstractions.Tools;
using IronHive.Core.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using AgentInvokeOptions = IronHive.Abstractions.Agent.AgentInvokeOptions;
using IronHiveAgent = IronHive.Abstractions.Agent.IAgent;

namespace Ironbees.Ironhive.Tests;

/// <summary>
/// Tool-calling surface tests for IronhiveAdapter: AgentConfig.Tools name resolution against
/// IronhiveOptions.Tools, fail-loud behavior for missing pool/names, and streaming
/// ToolCallStartChunk/ToolCallCompleteChunk mapping (HD-11).
/// </summary>
public class IronhiveAdapterToolTests
{
    private static AgentConfig CreateTestConfig(List<string>? tools = null) => new()
    {
        Name = "test-agent",
        Description = "Test agent",
        Version = "1.0.0",
        Model = new ModelConfig { Provider = "openai", Deployment = "gpt-test" },
        SystemPrompt = "You are a test agent.",
        Tools = tools,
    };

    private static ITool FakeTool(string name)
    {
        var tool = Substitute.For<ITool>();
        tool.UniqueName.Returns(name);
        return tool;
    }

    private static IronhiveAdapter CreateAdapter(
        IHiveService hiveService,
        IToolCollection? pool = null) =>
        new(
            hiveService,
            Substitute.For<IIronhiveOrchestratorFactory>(),
            new OrchestrationEventMapper(),
            NullLogger<IronhiveAdapter>.Instance,
            pool is null ? null : new IronhiveOptions { Tools = pool });

    [Fact]
    public async Task CreateAgentAsync_NoToolsDeclared_LeavesUnderlyingToolsNull()
    {
        var hiveService = Substitute.For<IHiveService>();
        var mockAgent = Substitute.For<IronHiveAgent>();
        hiveService.CreateAgentFrom(Arg.Any<Action<IronHive.Abstractions.Agent.AgentConfig>>()).Returns(mockAgent);
        // No pool registered at all - must not matter when the agent declares no tools.
        var adapter = CreateAdapter(hiveService, pool: null);

        await adapter.CreateAgentAsync(CreateTestConfig(tools: null), TestContext.Current.CancellationToken);

        Assert.Null(mockAgent.Tools);
    }

    [Fact]
    public async Task CreateAgentAsync_DeclaredToolsResolveFromPool_AssignsFilteredCollection()
    {
        var hiveService = Substitute.For<IHiveService>();
        var mockAgent = Substitute.For<IronHiveAgent>();
        hiveService.CreateAgentFrom(Arg.Any<Action<IronHive.Abstractions.Agent.AgentConfig>>()).Returns(mockAgent);
        var pool = new ToolCollection([FakeTool("search"), FakeTool("count-files"), FakeTool("unused")]);
        var adapter = CreateAdapter(hiveService, pool);

        await adapter.CreateAgentAsync(CreateTestConfig(tools: ["search", "count-files"]), TestContext.Current.CancellationToken);

        Assert.NotNull(mockAgent.Tools);
        Assert.Equal(2, mockAgent.Tools!.Count);
        Assert.True(mockAgent.Tools.ContainsKey("search"));
        Assert.True(mockAgent.Tools.ContainsKey("count-files"));
        Assert.False(mockAgent.Tools.ContainsKey("unused"));
    }

    [Fact]
    public async Task CreateAgentAsync_ToolsDeclaredButNoPoolRegistered_ThrowsActionable()
    {
        var hiveService = Substitute.For<IHiveService>();
        hiveService.CreateAgentFrom(Arg.Any<Action<IronHive.Abstractions.Agent.AgentConfig>>())
            .Returns(Substitute.For<IronHiveAgent>());
        var adapter = CreateAdapter(hiveService, pool: null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => adapter.CreateAgentAsync(CreateTestConfig(tools: ["search"]), TestContext.Current.CancellationToken));

        Assert.Contains("test-agent", ex.Message);
        Assert.Contains("IronhiveOptions.Tools", ex.Message);
    }

    [Fact]
    public async Task CreateAgentAsync_UnknownToolName_ThrowsWithAvailableNames()
    {
        var hiveService = Substitute.For<IHiveService>();
        hiveService.CreateAgentFrom(Arg.Any<Action<IronHive.Abstractions.Agent.AgentConfig>>())
            .Returns(Substitute.For<IronHiveAgent>());
        var pool = new ToolCollection([FakeTool("search")]);
        var adapter = CreateAdapter(hiveService, pool);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => adapter.CreateAgentAsync(CreateTestConfig(tools: ["search", "typo-tool"]), TestContext.Current.CancellationToken));

        Assert.Contains("typo-tool", ex.Message);
        Assert.Contains("search", ex.Message);
    }

    [Fact]
    public async Task StreamStructuredAsync_ToolCallAddedThenCompleted_YieldsStartThenComplete()
    {
        var mockAgent = Substitute.For<IronHiveAgent>();
        mockAgent
            .InvokeStreamingAsync(Arg.Any<IEnumerable<Message>>(), Arg.Any<AgentInvokeOptions?>(), Arg.Any<CancellationToken>())
            .Returns(Stream());
        var wrapper = new IronhiveAgentWrapper(mockAgent, CreateTestConfig());
        var adapter = CreateAdapter(Substitute.For<IHiveService>());

        static async IAsyncEnumerable<StreamingMessageResponse> Stream()
        {
            yield return new StreamingContentAddedResponse
            {
                Index = 0,
                Content = new ToolMessageContent { Id = "call-1", Name = "search", IsApproved = true },
            };
            yield return new StreamingContentCompletedResponse
            {
                Index = 0,
                Content = new ToolMessageContent
                {
                    Id = "call-1",
                    Name = "search",
                    IsApproved = true,
                    Output = ToolOutput.Success("3 results"),
                },
            };
            yield return new StreamingMessageDoneResponse();
            await Task.CompletedTask;
        }

        var chunks = new List<StreamChunk>();
        await foreach (var chunk in adapter.StreamStructuredAsync(wrapper, "Hi", cancellationToken: TestContext.Current.CancellationToken))
        {
            chunks.Add(chunk);
        }

        var start = Assert.IsType<ToolCallStartChunk>(chunks[0]);
        Assert.Equal("search", start.ToolName);
        Assert.Equal("call-1", start.ToolCallId);

        var complete = Assert.IsType<ToolCallCompleteChunk>(chunks[1]);
        Assert.Equal("search", complete.ToolName);
        Assert.Equal("call-1", complete.ToolCallId);
        Assert.True(complete.Success);
        Assert.Equal("3 results", complete.Result);
        Assert.Null(complete.Error);
    }

    [Fact]
    public async Task StreamStructuredAsync_FailedToolCall_YieldsCompleteChunkWithError()
    {
        var mockAgent = Substitute.For<IronHiveAgent>();
        mockAgent
            .InvokeStreamingAsync(Arg.Any<IEnumerable<Message>>(), Arg.Any<AgentInvokeOptions?>(), Arg.Any<CancellationToken>())
            .Returns(Stream());
        var wrapper = new IronhiveAgentWrapper(mockAgent, CreateTestConfig());
        var adapter = CreateAdapter(Substitute.For<IHiveService>());

        static async IAsyncEnumerable<StreamingMessageResponse> Stream()
        {
            yield return new StreamingContentCompletedResponse
            {
                Index = 0,
                Content = new ToolMessageContent
                {
                    Id = "call-2",
                    Name = "count-files",
                    IsApproved = true,
                    Output = ToolOutput.Failure("directory not found"),
                },
            };
            yield return new StreamingMessageDoneResponse();
            await Task.CompletedTask;
        }

        var chunks = new List<StreamChunk>();
        await foreach (var chunk in adapter.StreamStructuredAsync(wrapper, "Hi", cancellationToken: TestContext.Current.CancellationToken))
        {
            chunks.Add(chunk);
        }

        var complete = Assert.IsType<ToolCallCompleteChunk>(chunks[0]);
        Assert.False(complete.Success);
        Assert.Null(complete.Result);
        Assert.Equal("directory not found", complete.Error);
    }
}
