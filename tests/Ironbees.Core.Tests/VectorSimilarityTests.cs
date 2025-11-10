namespace Ironbees.Core.Tests;

public class VectorSimilarityTests
{
    [Fact]
    public void CosineSimilarity_IdenticalVectors_Returns1()
    {
        var vector = new float[] { 1.0f, 2.0f, 3.0f };

        var similarity = VectorSimilarity.CosineSimilarity(vector, vector);

        Assert.Equal(1.0, similarity, precision: 5);
    }

    [Fact]
    public void CosineSimilarity_OrthogonalVectors_Returns0()
    {
        var vector1 = new float[] { 1.0f, 0.0f };
        var vector2 = new float[] { 0.0f, 1.0f };

        var similarity = VectorSimilarity.CosineSimilarity(vector1, vector2);

        Assert.Equal(0.0, similarity, precision: 5);
    }

    [Fact]
    public void CosineSimilarity_OppositeVectors_ReturnsNegative1()
    {
        var vector1 = new float[] { 1.0f, 0.0f };
        var vector2 = new float[] { -1.0f, 0.0f };

        var similarity = VectorSimilarity.CosineSimilarity(vector1, vector2);

        Assert.Equal(-1.0, similarity, precision: 5);
    }

    [Fact]
    public void CosineSimilarity_SimilarVectors_ReturnsHighScore()
    {
        var vector1 = new float[] { 1.0f, 2.0f, 3.0f };
        var vector2 = new float[] { 1.1f, 2.1f, 2.9f };

        var similarity = VectorSimilarity.CosineSimilarity(vector1, vector2);

        Assert.True(similarity > 0.99);
    }

    [Fact]
    public void CosineSimilarity_DifferentDimensions_ThrowsArgumentException()
    {
        var vector1 = new float[] { 1.0f, 2.0f };
        var vector2 = new float[] { 1.0f, 2.0f, 3.0f };

        Assert.Throws<ArgumentException>(() =>
            VectorSimilarity.CosineSimilarity(vector1, vector2));
    }

    [Fact]
    public void CosineSimilarity_EmptyVectors_ThrowsArgumentException()
    {
        var vector1 = Array.Empty<float>();
        var vector2 = Array.Empty<float>();

        Assert.Throws<ArgumentException>(() =>
            VectorSimilarity.CosineSimilarity(vector1, vector2));
    }

    [Fact]
    public void Normalize_RegularVector_ReturnsUnitVector()
    {
        var vector = new float[] { 3.0f, 4.0f }; // Magnitude = 5

        var normalized = VectorSimilarity.Normalize(vector);

        Assert.Equal(0.6f, normalized[0], precision: 5);
        Assert.Equal(0.8f, normalized[1], precision: 5);

        // Check magnitude is 1.0
        var magnitude = Math.Sqrt(normalized[0] * normalized[0] + normalized[1] * normalized[1]);
        Assert.Equal(1.0, magnitude, precision: 5);
    }

    [Fact]
    public void Normalize_ZeroVector_ReturnsZeroVector()
    {
        var vector = new float[] { 0.0f, 0.0f, 0.0f };

        var normalized = VectorSimilarity.Normalize(vector);

        Assert.All(normalized, value => Assert.Equal(0.0f, value));
    }

    [Fact]
    public void Normalize_AlreadyNormalized_ReturnsSameVector()
    {
        var vector = new float[] { 0.6f, 0.8f }; // Already unit vector

        var normalized = VectorSimilarity.Normalize(vector);

        Assert.Equal(0.6f, normalized[0], precision: 5);
        Assert.Equal(0.8f, normalized[1], precision: 5);
    }

    [Fact]
    public void EuclideanDistance_IdenticalVectors_Returns0()
    {
        var vector = new float[] { 1.0f, 2.0f, 3.0f };

        var distance = VectorSimilarity.EuclideanDistance(vector, vector);

        Assert.Equal(0.0, distance, precision: 5);
    }

    [Fact]
    public void EuclideanDistance_SimpleVectors_ReturnsCorrectDistance()
    {
        var vector1 = new float[] { 0.0f, 0.0f };
        var vector2 = new float[] { 3.0f, 4.0f };

        var distance = VectorSimilarity.EuclideanDistance(vector1, vector2);

        Assert.Equal(5.0, distance, precision: 5);
    }

    [Fact]
    public void EuclideanDistance_DifferentDimensions_ThrowsArgumentException()
    {
        var vector1 = new float[] { 1.0f, 2.0f };
        var vector2 = new float[] { 1.0f, 2.0f, 3.0f };

        Assert.Throws<ArgumentException>(() =>
            VectorSimilarity.EuclideanDistance(vector1, vector2));
    }

    [Fact]
    public void DotProduct_OrthogonalVectors_Returns0()
    {
        var vector1 = new float[] { 1.0f, 0.0f };
        var vector2 = new float[] { 0.0f, 1.0f };

        var dotProduct = VectorSimilarity.DotProduct(vector1, vector2);

        Assert.Equal(0.0, dotProduct, precision: 5);
    }

    [Fact]
    public void DotProduct_SimpleVectors_ReturnsCorrectValue()
    {
        var vector1 = new float[] { 1.0f, 2.0f, 3.0f };
        var vector2 = new float[] { 4.0f, 5.0f, 6.0f };

        var dotProduct = VectorSimilarity.DotProduct(vector1, vector2);

        // 1*4 + 2*5 + 3*6 = 4 + 10 + 18 = 32
        Assert.Equal(32.0, dotProduct, precision: 5);
    }

    [Fact]
    public void DotProduct_NormalizedVectors_EqualsCosineSimilarity()
    {
        var vector1 = new float[] { 1.0f, 2.0f, 3.0f };
        var vector2 = new float[] { 4.0f, 5.0f, 6.0f };

        var normalized1 = VectorSimilarity.Normalize(vector1);
        var normalized2 = VectorSimilarity.Normalize(vector2);

        var dotProduct = VectorSimilarity.DotProduct(normalized1, normalized2);
        var cosineSimilarity = VectorSimilarity.CosineSimilarity(vector1, vector2);

        Assert.Equal(cosineSimilarity, dotProduct, precision: 5);
    }
}
