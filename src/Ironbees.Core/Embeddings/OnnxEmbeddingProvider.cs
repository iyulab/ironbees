using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Ironbees.Core.Embeddings;

/// <summary>
/// Provides embeddings using local ONNX models with automatic model download.
/// Supports all-MiniLM-L6-v2 (384dim, fast) and all-MiniLM-L12-v2 (384dim, accurate).
/// </summary>
public class OnnxEmbeddingProvider : IEmbeddingProvider, IDisposable
{
    private readonly InferenceSession _session;
    private readonly BertTokenizer _tokenizer;
    private readonly int _dimensions;
    private readonly string _modelName;
    private readonly ModelDownloader _downloader;

    /// <summary>
    /// Supported ONNX embedding models.
    /// </summary>
    public enum ModelType
    {
        /// <summary>
        /// all-MiniLM-L6-v2: Fast and efficient (6 layers, ~23MB, ~14K sentences/sec)
        /// Best for: Real-time applications, resource-constrained environments
        /// </summary>
        MiniLML6V2,

        /// <summary>
        /// all-MiniLM-L12-v2: Higher accuracy (12 layers, ~45MB, ~4K sentences/sec)
        /// Best for: Legal documents, academic papers, quality-critical tasks
        /// </summary>
        MiniLML12V2
    }

    /// <summary>
    /// Creates a new ONNX embedding provider with automatic model download.
    /// </summary>
    /// <param name="modelType">Model type to use (default: MiniLML6V2 for speed)</param>
    /// <param name="cacheDirectory">Custom cache directory for models</param>
    /// <param name="cancellationToken">Cancellation token for model download</param>
    public static async Task<OnnxEmbeddingProvider> CreateAsync(
        ModelType modelType = ModelType.MiniLML6V2,
        string? cacheDirectory = null,
        CancellationToken cancellationToken = default)
    {
        var downloader = new ModelDownloader(cacheDirectory);
        var modelName = GetModelName(modelType);

        // Ensure model is downloaded
        var modelPath = await downloader.EnsureModelAsync(modelName, cancellationToken);

        return new OnnxEmbeddingProvider(modelPath, modelName, downloader);
    }

    /// <summary>
    /// Creates a provider from an already downloaded model path (for advanced scenarios).
    /// </summary>
    /// <param name="modelPath">Path to local model directory</param>
    /// <param name="modelName">Model name for identification</param>
    /// <param name="downloader">Model downloader instance (optional)</param>
    public OnnxEmbeddingProvider(string modelPath, string modelName, ModelDownloader? downloader = null)
    {
        _modelName = modelName;
        _downloader = downloader ?? new ModelDownloader();
        _dimensions = 384; // Both MiniLM models use 384 dimensions

        // Load ONNX model
        var onnxPath = Path.Combine(modelPath, "model.onnx");
        var tokenizerPath = Path.Combine(modelPath, "tokenizer.json");

        if (!File.Exists(onnxPath))
        {
            throw new FileNotFoundException($"ONNX model not found at: {onnxPath}");
        }

        if (!File.Exists(tokenizerPath))
        {
            throw new FileNotFoundException($"Tokenizer not found at: {tokenizerPath}");
        }

        _session = new InferenceSession(onnxPath);
        _tokenizer = new BertTokenizer(tokenizerPath);
    }

    /// <inheritdoc />
    public int Dimensions => _dimensions;

    /// <inheritdoc />
    public string ModelName => _modelName;

    /// <inheritdoc />
    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            // Tokenize input
            var (inputIds, attentionMask) = _tokenizer.Encode(text);

            // Create input tensors
            var inputIdsTensor = new DenseTensor<long>(inputIds, new[] { 1, inputIds.Length });
            var attentionMaskTensor = new DenseTensor<long>(attentionMask, new[] { 1, attentionMask.Length });

            // Run inference
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
                NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor)
            };

            using var results = _session.Run(inputs);
            var output = results.First().AsEnumerable<float>().ToArray();

            // Extract embedding (first token [CLS] representation)
            var embedding = new float[_dimensions];
            Array.Copy(output, 0, embedding, 0, _dimensions);

            // Normalize to unit vector
            return VectorSimilarity.Normalize(embedding);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var embeddings = new List<float[]>();

            // Process each text individually (ONNX batching would require dynamic input shapes)
            foreach (var text in texts)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var (inputIds, attentionMask) = _tokenizer.Encode(text);

                var inputIdsTensor = new DenseTensor<long>(inputIds, new[] { 1, inputIds.Length });
                var attentionMaskTensor = new DenseTensor<long>(attentionMask, new[] { 1, attentionMask.Length });

                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
                    NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor)
                };

                using var results = _session.Run(inputs);
                var output = results.First().AsEnumerable<float>().ToArray();

                var embedding = new float[_dimensions];
                Array.Copy(output, 0, embedding, 0, _dimensions);

                embeddings.Add(VectorSimilarity.Normalize(embedding));
            }

            return embeddings;
        }, cancellationToken);
    }

    /// <summary>
    /// Gets the model name string for a model type.
    /// </summary>
    private static string GetModelName(ModelType modelType)
    {
        return modelType switch
        {
            ModelType.MiniLML6V2 => "all-MiniLM-L6-v2",
            ModelType.MiniLML12V2 => "all-MiniLM-L12-v2",
            _ => throw new ArgumentException($"Unknown model type: {modelType}")
        };
    }

    /// <summary>
    /// Clears the model cache for this provider's model.
    /// </summary>
    public void ClearCache()
    {
        _downloader.ClearModelCache(_modelName);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _session?.Dispose();
    }
}
