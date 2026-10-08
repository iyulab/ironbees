namespace Ironbees.Core;

/// <summary>
/// Provides text embedding generation capabilities for semantic similarity comparison.
/// </summary>
/// <remarks>
/// Two roles reach an embedder: the text that is compared against (an agent's description and capabilities —
/// <see cref="GenerateEmbeddingAsync"/> and <see cref="GenerateEmbeddingsAsync"/>) and the request compared against it
/// (<see cref="GenerateQueryEmbeddingAsync"/>). A symmetric model needs nothing: the query method defaults to
/// <see cref="GenerateEmbeddingAsync"/>. An asymmetric model (E5 <c>query: </c>/<c>passage: </c>, Nomic, a BGE query
/// instruction) implements it with its query convention; a provider that wraps another one forwards it.
/// </remarks>
public interface IEmbeddingProvider
{
    /// <summary>
    /// Generates an embedding vector for the given text.
    /// </summary>
    /// <param name="text">The text to generate an embedding for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A normalized embedding vector (magnitude = 1.0).</returns>
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates embedding vectors for multiple texts in a single batch request.
    /// </summary>
    /// <param name="texts">The texts to generate embeddings for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of normalized embedding vectors corresponding to the input texts.</returns>
    Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates an embedding for a request to be matched against vectors from <see cref="GenerateEmbeddingAsync"/>.
    /// Defaults to <see cref="GenerateEmbeddingAsync"/> (a symmetric model); an asymmetric model overrides it.
    /// </summary>
    /// <param name="query">The request to embed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A normalized embedding vector (magnitude = 1.0).</returns>
    Task<float[]> GenerateQueryEmbeddingAsync(string query, CancellationToken cancellationToken = default) =>
        GenerateEmbeddingAsync(query, cancellationToken);

    /// <summary>
    /// Gets the dimensionality of the embedding vectors produced by this provider.
    /// </summary>
    int Dimensions { get; }

    /// <summary>
    /// Gets the name of the embedding model used by this provider.
    /// </summary>
    string ModelName { get; }
}
