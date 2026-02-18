using TokenMeter;

namespace Ironbees.Autonomous.Utilities;

/// <summary>
/// Token count estimator that delegates to TokenMeter for accurate tiktoken-based counting.
/// </summary>
internal static class TokenEstimator
{
    private static readonly TokenCounter _counter = TokenCounter.Default();

    /// <summary>
    /// Estimates token count using TokenMeter's tiktoken-based counting.
    /// </summary>
    public static int EstimateTokens(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        return _counter.CountTokens(text);
    }
}
