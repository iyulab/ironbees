namespace Ironbees.Core;

/// <summary>
/// Provides vector similarity computation methods for embedding-based comparisons.
/// </summary>
public static class VectorSimilarity
{
    /// <summary>
    /// Computes the cosine similarity between two vectors.
    /// </summary>
    /// <param name="vector1">The first vector.</param>
    /// <param name="vector2">The second vector.</param>
    /// <returns>
    /// Cosine similarity score between -1.0 and 1.0, where:
    /// 1.0 = identical direction,
    /// 0.0 = orthogonal (no similarity),
    /// -1.0 = opposite direction.
    /// For normalized vectors, this is equivalent to dot product.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when vectors have different dimensions.</exception>
    public static double CosineSimilarity(float[] vector1, float[] vector2)
    {
        ArgumentNullException.ThrowIfNull(vector1);
        ArgumentNullException.ThrowIfNull(vector2);

        if (vector1.Length != vector2.Length)
        {
            throw new ArgumentException(
                $"Vectors must have the same dimensions. Got {vector1.Length} and {vector2.Length}");
        }

        if (vector1.Length == 0)
        {
            throw new ArgumentException("Vectors must not be empty");
        }

        double dotProduct = 0.0;
        double magnitude1 = 0.0;
        double magnitude2 = 0.0;

        for (int i = 0; i < vector1.Length; i++)
        {
            dotProduct += vector1[i] * vector2[i];
            magnitude1 += vector1[i] * vector1[i];
            magnitude2 += vector2[i] * vector2[i];
        }

        magnitude1 = Math.Sqrt(magnitude1);
        magnitude2 = Math.Sqrt(magnitude2);

        if (magnitude1 == 0.0 || magnitude2 == 0.0)
        {
            return 0.0;
        }

        return dotProduct / (magnitude1 * magnitude2);
    }

    /// <summary>
    /// Normalizes a vector to unit length (magnitude = 1.0).
    /// </summary>
    /// <param name="vector">The vector to normalize.</param>
    /// <returns>A new normalized vector.</returns>
    public static float[] Normalize(float[] vector)
    {
        ArgumentNullException.ThrowIfNull(vector);

        if (vector.Length == 0)
        {
            throw new ArgumentException("Vector must not be empty");
        }

        double magnitude = 0.0;
        for (int i = 0; i < vector.Length; i++)
        {
            magnitude += vector[i] * vector[i];
        }

        magnitude = Math.Sqrt(magnitude);

        if (magnitude == 0.0)
        {
            // Return zero vector if input is zero vector
            return new float[vector.Length];
        }

        var normalized = new float[vector.Length];
        for (int i = 0; i < vector.Length; i++)
        {
            normalized[i] = (float)(vector[i] / magnitude);
        }

        return normalized;
    }

    /// <summary>
    /// Computes the Euclidean distance between two vectors.
    /// </summary>
    /// <param name="vector1">The first vector.</param>
    /// <param name="vector2">The second vector.</param>
    /// <returns>Euclidean distance (lower is more similar).</returns>
    /// <exception cref="ArgumentException">Thrown when vectors have different dimensions.</exception>
    public static double EuclideanDistance(float[] vector1, float[] vector2)
    {
        ArgumentNullException.ThrowIfNull(vector1);
        ArgumentNullException.ThrowIfNull(vector2);

        if (vector1.Length != vector2.Length)
        {
            throw new ArgumentException(
                $"Vectors must have the same dimensions. Got {vector1.Length} and {vector2.Length}");
        }

        double sumSquaredDifferences = 0.0;
        for (int i = 0; i < vector1.Length; i++)
        {
            double difference = vector1[i] - vector2[i];
            sumSquaredDifferences += difference * difference;
        }

        return Math.Sqrt(sumSquaredDifferences);
    }

    /// <summary>
    /// Computes the dot product of two vectors.
    /// For normalized vectors, this is equivalent to cosine similarity.
    /// </summary>
    /// <param name="vector1">The first vector.</param>
    /// <param name="vector2">The second vector.</param>
    /// <returns>Dot product.</returns>
    /// <exception cref="ArgumentException">Thrown when vectors have different dimensions.</exception>
    public static double DotProduct(float[] vector1, float[] vector2)
    {
        ArgumentNullException.ThrowIfNull(vector1);
        ArgumentNullException.ThrowIfNull(vector2);

        if (vector1.Length != vector2.Length)
        {
            throw new ArgumentException(
                $"Vectors must have the same dimensions. Got {vector1.Length} and {vector2.Length}");
        }

        double dotProduct = 0.0;
        for (int i = 0; i < vector1.Length; i++)
        {
            dotProduct += vector1[i] * vector2[i];
        }

        return dotProduct;
    }
}
