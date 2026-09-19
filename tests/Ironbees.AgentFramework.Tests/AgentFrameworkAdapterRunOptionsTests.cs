using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using Ironbees.Core;
using Ironbees.Core.Streaming;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OpenAI;

namespace Ironbees.AgentFramework.Tests;

/// <summary>
/// Per-request options through the OpenAI-SDK adapter, observed on the wire.
///
/// <para>
/// <see cref="AgentRunOptions"/> reaches every adapter through <c>RunStructuredAsync</c> /
/// <c>StreamStructuredAsync</c>. This adapter did not override either, so it inherited the interface
/// default, which refuses every option — a caller who set an output cap got
/// <see cref="NotSupportedException"/> from this adapter and the cap from the IronHive one. Loud, which
/// was deliberate, but the cap is something this adapter can honour: it builds the request itself.
/// </para>
/// <para>
/// What the adapter cannot honour it still refuses. The roster test below makes that a decision per
/// option rather than a default: a new <see cref="AgentRunOptions"/> property fails it until someone
/// says whether this adapter honours it or refuses it.
/// </para>
/// </summary>
public class AgentFrameworkAdapterRunOptionsTests
{
    private const int ConfiguredCap = 1000;

    /// <summary>What this adapter puts on the wire.</summary>
    private static readonly HashSet<string> Honoured = [nameof(AgentRunOptions.MaxTokens)];

    /// <summary>
    /// What it refuses: it has no tool-execution loop, no reasoning channel on Chat Completions to
    /// surface thinking deltas from, and no suggestion pass.
    /// </summary>
    private static readonly HashSet<string> Refused =
    [
        nameof(AgentRunOptions.Suggestions),
        nameof(AgentRunOptions.ThinkingEffort),
        nameof(AgentRunOptions.Tools),
        nameof(AgentRunOptions.MaxToolTurns),
    ];

    private static AgentConfig CreateConfig() => new()
    {
        Name = "test-agent",
        Description = "Test agent",
        Version = "1.0.0",
        SystemPrompt = "You are a test assistant",
        Model = new ModelConfig { Deployment = "gpt-4", Temperature = 0.7, MaxTokens = ConfiguredCap }
    };

    private static (ILLMFrameworkAdapter Adapter, CapturingHandler Handler) CreateAdapter()
    {
        var handler = new CapturingHandler();
        var client = new OpenAIClient(new ApiKeyCredential("test-key"), new OpenAIClientOptions
        {
            Endpoint = new Uri("http://localhost/v1"),
            Transport = new HttpClientPipelineTransport(new HttpClient(handler)),
        });
        return (new AgentFrameworkAdapter(client, NullLogger<AgentFrameworkAdapter>.Instance), handler);
    }

    private static async Task DrainAsync(IAsyncEnumerable<StreamChunk> stream)
    {
        await foreach (var _ in stream)
        {
        }
    }

    private static int? SentCap(JsonNode request) => request["max_completion_tokens"]?.GetValue<int>();

    [Fact]
    public async Task AnOutputCap_ReachesTheWire_FromBothHalves()
    {
        var (adapter, handler) = CreateAdapter();
        var agent = await adapter.CreateAgentAsync(CreateConfig(), TestContext.Current.CancellationToken);
        var options = new AgentRunOptions { MaxTokens = 256 };

        var result = await adapter.RunStructuredAsync(agent, "hi", null, options, TestContext.Current.CancellationToken);
        await DrainAsync(adapter.StreamStructuredAsync(agent, "hi", null, options, TestContext.Current.CancellationToken));

        Assert.Equal("ok", result.Text);
        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, r => Assert.Equal(256, SentCap(r)));
    }

    [Fact]
    public async Task WithoutAnOverride_TheAgentsConfiguredCapIsSent_FromBothHalves()
    {
        var (adapter, handler) = CreateAdapter();
        var agent = await adapter.CreateAgentAsync(CreateConfig(), TestContext.Current.CancellationToken);

        // Null options and an options object with nothing set must mean the same thing.
        foreach (var options in new AgentRunOptions?[] { null, new AgentRunOptions() })
        {
            await adapter.RunStructuredAsync(agent, "hi", null, options, TestContext.Current.CancellationToken);
            await DrainAsync(adapter.StreamStructuredAsync(agent, "hi", null, options, TestContext.Current.CancellationToken));
        }

        Assert.Equal(4, handler.Requests.Count);
        Assert.All(handler.Requests, r => Assert.Equal(ConfiguredCap, SentCap(r)));
    }

    [Fact]
    public async Task TheOrchestratorsPerRequestCap_ReachesTheWire_ThroughThisAdapter()
    {
        // The consumer-visible path: a cap set on ProcessOptions, the adapter chosen by registration.
        // Before this adapter overrode the structured surface, this threw NotSupportedException here
        // while the IronHive adapter applied the cap - one setting, two behaviours by adapter.
        var (adapter, handler) = CreateAdapter();
        var agent = await adapter.CreateAgentAsync(CreateConfig(), TestContext.Current.CancellationToken);
        var registry = Substitute.For<IAgentRegistry>();
        registry.ListAgents().Returns(new List<string> { agent.Name });
        registry.GetAgent(agent.Name).Returns(agent);
        var orchestrator = new AgentOrchestrator(
            Substitute.For<IAgentLoader>(), registry, adapter, Substitute.For<IAgentSelector>(), agentsDirectory: null);
        var options = new ProcessOptions { AgentName = agent.Name, MaxTokens = 128 };

        await orchestrator.ProcessStructuredAsync("hi", options, TestContext.Current.CancellationToken);
        await DrainAsync(orchestrator.StreamStructuredAsync("hi", options, TestContext.Current.CancellationToken));

        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, r => Assert.Equal(128, SentCap(r)));
    }

    [Fact]
    public void EveryRunOption_IsEitherHonouredOrRefused_ByThisAdapter()
    {
        var properties = typeof(AgentRunOptions)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet();

        Assert.Empty(Honoured.Intersect(Refused));
        Assert.True(properties.SetEquals(Honoured.Union(Refused)),
            "AgentRunOptions gained or lost a property. Decide what AgentFrameworkAdapter does with it - " +
            "apply it in BuildChatOptions, or refuse it in ThrowIfUnsupported - and record the decision " +
            $"in this test. Unclassified: [{string.Join(", ", properties.Except(Honoured.Union(Refused)))}]; " +
            $"stale: [{string.Join(", ", Honoured.Union(Refused).Except(properties))}].");
    }

    public static TheoryData<string> RefusedOptions() => [.. Refused];

    [Theory]
    [MemberData(nameof(RefusedOptions))]
    public async Task ARefusedOption_FailsLoud_FromBothHalves_BeforeAnythingIsSent(string option)
    {
        var (adapter, handler) = CreateAdapter();
        var agent = await adapter.CreateAgentAsync(CreateConfig(), TestContext.Current.CancellationToken);
        var options = option switch
        {
            nameof(AgentRunOptions.Suggestions) => new AgentRunOptions { Suggestions = new SuggestionRequest() },
            nameof(AgentRunOptions.ThinkingEffort) => new AgentRunOptions { ThinkingEffort = ThinkingEffort.Low },
            nameof(AgentRunOptions.Tools) => new AgentRunOptions { Tools = [AIFunctionFactory.Create(() => 1, "noop")] },
            nameof(AgentRunOptions.MaxToolTurns) => new AgentRunOptions { MaxToolTurns = 2 },
            _ => throw new ArgumentOutOfRangeException(nameof(option), option, "no sample value for this option"),
        };

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            adapter.RunStructuredAsync(agent, "hi", null, options, TestContext.Current.CancellationToken));
        // Eager: the interface contract says the refusal happens at call time, not on first MoveNext.
        Assert.Throws<NotSupportedException>(() =>
            adapter.StreamStructuredAsync(agent, "hi", null, options, TestContext.Current.CancellationToken));

        Assert.Empty(handler.Requests);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public List<JsonNode> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var json = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!;
            Requests.Add(json);

            var streaming = json["stream"]?.GetValue<bool>() == true;
            var body = streaming
                ? "data: {\"id\":\"c\",\"object\":\"chat.completion.chunk\",\"created\":1,\"model\":\"gpt-4\",\"choices\":[{\"index\":0,\"delta\":{\"role\":\"assistant\",\"content\":\"ok\"},\"finish_reason\":null}]}\n\n" +
                  "data: {\"id\":\"c\",\"object\":\"chat.completion.chunk\",\"created\":1,\"model\":\"gpt-4\",\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}]}\n\n" +
                  "data: [DONE]\n\n"
                : "{\"id\":\"c\",\"object\":\"chat.completion\",\"created\":1,\"model\":\"gpt-4\",\"choices\":[{\"index\":0,\"message\":{\"role\":\"assistant\",\"content\":\"ok\"},\"finish_reason\":\"stop\"}]}";

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, streaming ? "text/event-stream" : "application/json"),
            };
        }
    }
}
