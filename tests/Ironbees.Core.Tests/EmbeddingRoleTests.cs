using NSubstitute;

namespace Ironbees.Core.Tests;

/// <summary>
/// The query role of <see cref="IEmbeddingProvider"/>: an asymmetric model embeds the request differently from the agent
/// texts it is matched against, so the selector must embed the request through
/// <see cref="IEmbeddingProvider.GenerateQueryEmbeddingAsync"/>.
/// </summary>
public class EmbeddingRoleTests
{
    [Fact]
    public async Task GenerateQueryEmbeddingAsync_DefaultsToGenerateEmbeddingAsync()
    {
        IEmbeddingProvider provider = new SymmetricProvider();

        var query = await provider.GenerateQueryEmbeddingAsync("write a poem", TestContext.Current.CancellationToken);

        Assert.Equal(SymmetricProvider.Vector, query);
    }

    [Fact]
    public async Task Selector_EmbedsTheRequestAsAQuery_AndAgentTextsAsDocuments()
    {
        var provider = new RecordingProvider();
        var selector = new EmbeddingAgentSelector(provider);
        var agents = new List<IAgent> { Agent("coder", "Writes code"), Agent("writer", "Writes prose") };

        await selector.SelectAgentAsync("refactor this class", agents, TestContext.Current.CancellationToken);

        Assert.Equal(["refactor this class"], provider.Queries);
        Assert.Equal(2, provider.Documents.Count);
        Assert.DoesNotContain("refactor this class", provider.Documents);
    }

    private static IAgent Agent(string name, string description)
    {
        var agent = Substitute.For<IAgent>();
        agent.Name.Returns(name);
        agent.Description.Returns(description);
        agent.Config.Returns(new AgentConfig
        {
            Name = name,
            Description = description,
            Version = "1.0.0",
            SystemPrompt = "Test prompt",
            Model = new ModelConfig { Deployment = "test" }
        });
        return agent;
    }

    private sealed class SymmetricProvider : IEmbeddingProvider
    {
        public static readonly float[] Vector = [7f];

        public int Dimensions => 1;
        public string ModelName => "symmetric";

        public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default) =>
            Task.FromResult(Vector);

        public Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<float[]>>(texts.Select(_ => Vector).ToArray());
    }

    private sealed class RecordingProvider : IEmbeddingProvider
    {
        public static readonly float[] QueryVector = [0f, 1f];
        public static readonly float[] DocumentVector = [1f, 0f];

        public List<string> Queries { get; } = [];
        public List<string> Documents { get; } = [];

        public int Dimensions => 2;
        public string ModelName => "recording";

        public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            Documents.Add(text);
            return Task.FromResult(DocumentVector);
        }

        public Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
        {
            Documents.AddRange(texts);
            return Task.FromResult<IReadOnlyList<float[]>>(texts.Select(_ => DocumentVector).ToArray());
        }

        public Task<float[]> GenerateQueryEmbeddingAsync(string query, CancellationToken cancellationToken = default)
        {
            Queries.Add(query);
            return Task.FromResult(QueryVector);
        }
    }
}
