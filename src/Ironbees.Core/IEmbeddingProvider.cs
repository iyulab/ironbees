namespace Ironbees.Core;

/// <summary>
/// Provides text embedding generation capabilities for semantic similarity comparison.
/// </summary>
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
    /// Gets the dimensionality of the embedding vectors produced by this provider.
    /// </summary>
    int Dimensions { get; }

    /// <summary>
    /// Gets the name of the embedding model used by this provider.
    /// </summary>
    string ModelName { get; }
}
