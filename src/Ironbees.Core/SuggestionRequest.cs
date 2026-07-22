namespace Ironbees.Core;

/// <summary>
/// How suggestion blocks are included in a response.
/// </summary>
public enum SuggestionMode
{
    /// <summary>Include suggestions only when the model judges them helpful.</summary>
    Auto,

    /// <summary>Always include suggestions.</summary>
    Always,
}

/// <summary>
/// Framework-neutral request for model-generated follow-up suggestions.
/// Setting a non-null value on <see cref="AgentRunOptions.Suggestions"/> enables
/// the feature; adapters map it to their framework's native option
/// (e.g. IronHive SuggestionOptions).
/// </summary>
public record SuggestionRequest
{
    /// <summary>
    /// Inclusion mode. Auto lets the model omit suggestions; Always forces them.
    /// </summary>
    public SuggestionMode Mode { get; init; } = SuggestionMode.Auto;

    /// <summary>
    /// Maximum number of suggestion blocks per response.
    /// </summary>
    public int MaxCount { get; init; } = 1;

    /// <summary>
    /// Minimum number of items per suggestion block.
    /// </summary>
    public int MinItems { get; init; } = 2;

    /// <summary>
    /// Maximum number of items per suggestion block.
    /// </summary>
    public int MaxItems { get; init; } = 4;
}
