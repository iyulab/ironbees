using Ironbees.Core;
using Ironbees.Core.Embeddings;

namespace EmbeddingSample;

/// <summary>
/// Demonstrates Ironbees embedding features including local ONNX models,
/// EmbeddingAgentSelector, and HybridAgentSelector.
/// </summary>
sealed class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║           Ironbees Embedding Features Demo                   ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        try
        {
            // Example 1: Using all-MiniLM-L6-v2 (fast model)
            await Example1_FastModel();

            Console.WriteLine();
            Console.WriteLine("─────────────────────────────────────────────────────────────────");
            Console.WriteLine();

            // Example 2: Using all-MiniLM-L12-v2 (accurate model)
            await Example2_AccurateModel();

            Console.WriteLine();
            Console.WriteLine("─────────────────────────────────────────────────────────────────");
            Console.WriteLine();

            // Example 3: Model Comparison (L6 vs L12)
            await Example3_ModelComparison();

            Console.WriteLine();
            Console.WriteLine("─────────────────────────────────────────────────────────────────");
            Console.WriteLine();

            // Example 4: EmbeddingAgentSelector
            await Example4_EmbeddingAgentSelector();

            Console.WriteLine();
            Console.WriteLine("─────────────────────────────────────────────────────────────────");
            Console.WriteLine();

            // Example 5: HybridAgentSelector (Keyword + Embedding)
            await Example5_HybridAgentSelector();

            Console.WriteLine();
            Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                    Demo Complete!                             ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine($"Stack: {ex.StackTrace}");
        }
    }

    /// <summary>
    /// Example 1: Using all-MiniLM-L6-v2 (fast model, default)
    /// - 6 layers, ~23MB download, ~14K sentences/sec
    /// - 384 dimensions, 84-85% accuracy
    /// - Best for: Real-time applications, resource-constrained environments
    /// </summary>
    static async Task Example1_FastModel()
    {
        Console.WriteLine("Example 1: all-MiniLM-L6-v2 (Fast Model)");
        Console.WriteLine("=========================================");
        Console.WriteLine();

        // Create provider (downloads model on first run)
        Console.WriteLine("Creating ONNX embedding provider (L6-v2)...");
        var provider = await OnnxEmbeddingProvider.CreateAsync(
            OnnxEmbeddingProvider.ModelType.MiniLML6V2);

        Console.WriteLine($"Model: {provider.ModelName}");
        Console.WriteLine($"Dimensions: {provider.Dimensions}");
        Console.WriteLine();

        // Generate embeddings
        var texts = new[]
        {
            "The quick brown fox jumps over the lazy dog",
            "A fast auburn fox leaps above a sleepy canine",
            "Machine learning is transforming technology",
            "How to make chocolate chip cookies"
        };

        Console.WriteLine("Generating embeddings...");
        var embeddings = await provider.GenerateEmbeddingsAsync(texts);

        // Calculate similarities
        Console.WriteLine();
        Console.WriteLine("Similarity Matrix:");
        Console.WriteLine("──────────────────────────────────────────────────");

        for (int i = 0; i < texts.Length; i++)
        {
            Console.WriteLine($"\n[{i}] \"{texts[i]}\"");
            for (int j = 0; j < texts.Length; j++)
            {
                if (i == j) continue;
                var similarity = VectorSimilarity.CosineSimilarity(embeddings[i], embeddings[j]);
                Console.WriteLine($"  → [{j}]: {similarity:F4}");
            }
        }

        provider.Dispose();
    }

    /// <summary>
    /// Example 2: Using all-MiniLM-L12-v2 (accurate model)
    /// - 12 layers, ~45MB download, ~4K sentences/sec
    /// - 384 dimensions, 87-88% accuracy
    /// - Best for: Legal documents, academic papers, quality-critical tasks
    /// </summary>
    static async Task Example2_AccurateModel()
    {
        Console.WriteLine("Example 2: all-MiniLM-L12-v2 (Accurate Model)");
        Console.WriteLine("==============================================");
        Console.WriteLine();

        // Create provider (downloads model on first run)
        Console.WriteLine("Creating ONNX embedding provider (L12-v2)...");
        var provider = await OnnxEmbeddingProvider.CreateAsync(
            OnnxEmbeddingProvider.ModelType.MiniLML12V2);

        Console.WriteLine($"Model: {provider.ModelName}");
        Console.WriteLine($"Dimensions: {provider.Dimensions}");
        Console.WriteLine();

        // Generate embeddings for technical content
        var texts = new[]
        {
            "Neural networks learn patterns from data",
            "Deep learning models require large datasets",
            "Supervised learning uses labeled training data",
            "The weather forecast predicts rain tomorrow"
        };

        Console.WriteLine("Generating embeddings for technical content...");
        var embeddings = await provider.GenerateEmbeddingsAsync(texts);

        // Calculate similarities
        Console.WriteLine();
        Console.WriteLine("Similarity Matrix (Technical Content):");
        Console.WriteLine("──────────────────────────────────────────────────");

        for (int i = 0; i < 3; i++)  // First 3 are ML-related
        {
            Console.WriteLine($"\n[{i}] \"{texts[i]}\"");
            for (int j = 0; j < texts.Length; j++)
            {
                if (i == j) continue;
                var similarity = VectorSimilarity.CosineSimilarity(embeddings[i], embeddings[j]);
                var label = j < 3 ? "[ML-related]" : "[unrelated]";
                Console.WriteLine($"  → [{j}]: {similarity:F4} {label}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("L12-v2 provides superior accuracy for distinguishing");
        Console.WriteLine("semantically similar technical content.");

        provider.Dispose();
    }

    /// <summary>
    /// Example 3: Direct comparison between L6-v2 and L12-v2 models
    /// Demonstrates the accuracy difference on the same content
    /// </summary>
    static async Task Example3_ModelComparison()
    {
        Console.WriteLine("Example 3: Model Comparison (L6-v2 vs L12-v2)");
        Console.WriteLine("==============================================");
        Console.WriteLine();

        // Create both providers
        Console.WriteLine("Loading both models for comparison...");
        var providerL6 = await OnnxEmbeddingProvider.CreateAsync(
            OnnxEmbeddingProvider.ModelType.MiniLML6V2);
        var providerL12 = await OnnxEmbeddingProvider.CreateAsync(
            OnnxEmbeddingProvider.ModelType.MiniLML12V2);

        Console.WriteLine($"L6-v2: {providerL6.ModelName} ({providerL6.Dimensions}D)");
        Console.WriteLine($"L12-v2: {providerL12.ModelName} ({providerL12.Dimensions}D)");
        Console.WriteLine();

        // Test texts with subtle differences
        var text1 = "The stock market crashed yesterday";
        var text2 = "Financial markets experienced a significant decline";
        var text3 = "I love eating pizza on Fridays";

        Console.WriteLine("Test Texts:");
        Console.WriteLine($"Text 1: \"{text1}\"");
        Console.WriteLine($"Text 2: \"{text2}\" [semantically similar to 1]");
        Console.WriteLine($"Text 3: \"{text3}\" [completely unrelated]");
        Console.WriteLine();

        // Generate embeddings with both models
        var emb1_L6 = await providerL6.GenerateEmbeddingAsync(text1);
        var emb2_L6 = await providerL6.GenerateEmbeddingAsync(text2);
        var emb3_L6 = await providerL6.GenerateEmbeddingAsync(text3);

        var emb1_L12 = await providerL12.GenerateEmbeddingAsync(text1);
        var emb2_L12 = await providerL12.GenerateEmbeddingAsync(text2);
        var emb3_L12 = await providerL12.GenerateEmbeddingAsync(text3);

        // Calculate similarities
        var sim12_L6 = VectorSimilarity.CosineSimilarity(emb1_L6, emb2_L6);
        var sim13_L6 = VectorSimilarity.CosineSimilarity(emb1_L6, emb3_L6);
        var sim12_L12 = VectorSimilarity.CosineSimilarity(emb1_L12, emb2_L12);
        var sim13_L12 = VectorSimilarity.CosineSimilarity(emb1_L12, emb3_L12);

        Console.WriteLine("Similarity Results:");
        Console.WriteLine("──────────────────────────────────────────────────");
        Console.WriteLine($"Text 1 ↔ Text 2 (related):");
        Console.WriteLine($"  L6-v2:  {sim12_L6:F4}");
        Console.WriteLine($"  L12-v2: {sim12_L12:F4}");
        Console.WriteLine();
        Console.WriteLine($"Text 1 ↔ Text 3 (unrelated):");
        Console.WriteLine($"  L6-v2:  {sim13_L6:F4}");
        Console.WriteLine($"  L12-v2: {sim13_L12:F4}");
        Console.WriteLine();

        var separation_L6 = sim12_L6 - sim13_L6;
        var separation_L12 = sim12_L12 - sim13_L12;

        Console.WriteLine($"Separation (related - unrelated):");
        Console.WriteLine($"  L6-v2:  {separation_L6:F4}");
        Console.WriteLine($"  L12-v2: {separation_L12:F4}");
        Console.WriteLine();
        Console.WriteLine("Higher separation = better distinction between related/unrelated content");
        Console.WriteLine($"L12-v2 improvement: {((separation_L12 / separation_L6 - 1) * 100):F1}%");

        providerL6.Dispose();
        providerL12.Dispose();
    }

    /// <summary>
    /// Example 4: EmbeddingAgentSelector
    /// - Pure semantic similarity-based agent selection
    /// - No keyword matching, only embedding comparison
    /// - Best for: Finding semantically similar agents regardless of exact keywords
    /// </summary>
    static async Task Example4_EmbeddingAgentSelector()
    {
        Console.WriteLine("Example 4: EmbeddingAgentSelector");
        Console.WriteLine("==================================");
        Console.WriteLine();

        // Create embedding provider
        var provider = await OnnxEmbeddingProvider.CreateAsync(
            OnnxEmbeddingProvider.ModelType.MiniLML6V2);

        // Create agents with descriptions
        var agents = CreateMockAgents();

        // Create selector
        var selector = new EmbeddingAgentSelector(provider);

        // Test queries
        var queries = new[]
        {
            "I need to write some Python code to process data",
            "How do I secure my web application against attacks?",
            "My React component is not rendering properly",
            "The database is running very slow"
        };

        Console.WriteLine("Testing semantic agent selection:");
        Console.WriteLine("──────────────────────────────────────────────────");
        Console.WriteLine();

        foreach (var query in queries)
        {
            Console.WriteLine($"Query: \"{query}\"");

            var result = await selector.SelectAgentAsync(query, agents);

            if (result.SelectedAgent != null)
            {
                Console.WriteLine($"Selected: {result.SelectedAgent.Name}");
                Console.WriteLine($"Confidence: {result.ConfidenceScore:F4}");
                Console.WriteLine($"Reason: {result.SelectionReason}");
            }
            else
            {
                Console.WriteLine("No suitable agent found");
            }
            Console.WriteLine();
        }

        provider.Dispose();
    }

    /// <summary>
    /// Example 5: HybridAgentSelector (Keyword + Embedding)
    /// - Combines keyword matching with semantic similarity
    /// - Balances exact term matches with contextual understanding
    /// - Best for: Production use cases requiring both precision and recall
    /// </summary>
    static async Task Example5_HybridAgentSelector()
    {
        Console.WriteLine("Example 5: HybridAgentSelector");
        Console.WriteLine("===============================");
        Console.WriteLine();

        // Create embedding provider
        var provider = await OnnxEmbeddingProvider.CreateAsync(
            OnnxEmbeddingProvider.ModelType.MiniLML6V2);

        // Create agents with descriptions
        var agents = CreateMockAgents();

        // Create keyword selector for hybrid
        var keywordSelector = new KeywordAgentSelector();

        // Create embedding selector
        var embeddingSelector = new EmbeddingAgentSelector(provider);

        // Create hybrid selector (default weights: Keyword 40% + Embedding 60%)
        var hybridSelector = new HybridAgentSelector(
            keywordSelector,
            embeddingSelector);

        // Test queries that benefit from hybrid approach
        var queries = new[]
        {
            "python security best practices",  // Contains both keywords
            "protect my application from SQL injection",  // Security context without keyword
            "optimize slow queries",  // Performance without exact "database" keyword
            "react hooks performance optimization"  // Multiple domains
        };

        Console.WriteLine("Testing hybrid agent selection (Keyword + Embedding):");
        Console.WriteLine("──────────────────────────────────────────────────────────────");
        Console.WriteLine();

        foreach (var query in queries)
        {
            Console.WriteLine($"Query: \"{query}\"");

            var result = await hybridSelector.SelectAgentAsync(query, agents);

            if (result.SelectedAgent != null)
            {
                Console.WriteLine($"Selected: {result.SelectedAgent.Name}");
                Console.WriteLine($"Confidence: {result.ConfidenceScore:F4}");
                Console.WriteLine($"Reason: {result.SelectionReason}");
            }
            else
            {
                Console.WriteLine("No suitable agent found");
            }
            Console.WriteLine();
        }

        provider.Dispose();
    }

    /// <summary>
    /// Creates mock agents for demonstration purposes.
    /// In production, these would be real agent implementations.
    /// </summary>
    static List<IAgent> CreateMockAgents()
    {
        return new List<IAgent>
        {
            new MockAgent
            {
                Name = "Python Expert",
                Description = "Expert in Python programming, data processing, scripting, and automation. " +
                             "Helps with writing clean Python code, debugging, and best practices.",
                Config = new AgentConfig
                {
                    Name = "python-expert",
                    Description = "Expert in Python programming",
                    Version = "1.0.0",
                    SystemPrompt = "You are a Python expert.",
                    Model = new ModelConfig { Provider = "openai", Deployment = "gpt-4", MaxTokens = 1000 },
                    Capabilities = new() { "python", "coding", "scripting", "automation" }
                }
            },
            new MockAgent
            {
                Name = "Security Specialist",
                Description = "Security expert specializing in application security, vulnerability assessment, " +
                             "threat modeling, and secure coding practices. Helps protect applications from attacks.",
                Config = new AgentConfig
                {
                    Name = "security-specialist",
                    Description = "Security expert",
                    Version = "1.0.0",
                    SystemPrompt = "You are a security expert.",
                    Model = new ModelConfig { Provider = "openai", Deployment = "gpt-4", MaxTokens = 1000 },
                    Capabilities = new() { "security", "vulnerability", "authentication", "encryption" }
                }
            },
            new MockAgent
            {
                Name = "Frontend Developer",
                Description = "Frontend development expert with React, Vue, Angular experience. " +
                             "Helps with UI components, state management, rendering issues, and performance.",
                Config = new AgentConfig
                {
                    Name = "frontend-developer",
                    Description = "Frontend development expert",
                    Version = "1.0.0",
                    SystemPrompt = "You are a frontend development expert.",
                    Model = new ModelConfig { Provider = "openai", Deployment = "gpt-4", MaxTokens = 1000 },
                    Capabilities = new() { "react", "frontend", "ui", "components", "javascript" }
                }
            },
            new MockAgent
            {
                Name = "Database Optimizer",
                Description = "Database performance expert specializing in query optimization, indexing, " +
                             "schema design, and database tuning. Helps resolve slow queries and bottlenecks.",
                Config = new AgentConfig
                {
                    Name = "database-optimizer",
                    Description = "Database performance expert",
                    Version = "1.0.0",
                    SystemPrompt = "You are a database performance expert.",
                    Model = new ModelConfig { Provider = "openai", Deployment = "gpt-4", MaxTokens = 1000 },
                    Capabilities = new() { "database", "sql", "performance", "optimization", "indexing" }
                }
            }
        };
    }

    /// <summary>
    /// Mock agent implementation for demonstration.
    /// </summary>
    sealed class MockAgent : IAgent
    {
        public required string Name { get; init; }
        public required string Description { get; init; }
        public required AgentConfig Config { get; init; }
    }
}
