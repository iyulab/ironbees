namespace Ironbees.Autonomous.Utilities;

internal static class TokenEstimator
{
    public static int EstimateTokens(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        // Rough estimation: ~4 characters per token (GPT-style)
        return (text.Length + 3) / 4;
    }
}
