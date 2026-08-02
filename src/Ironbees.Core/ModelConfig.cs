namespace Ironbees.Core;

/// <summary>
/// LLM model configuration
/// </summary>
/// <remarks>
/// <para>
/// Not every sampling parameter is supported by every backend. The table below is the
/// authoritative support matrix; a parameter marked "ignored" is accepted by configuration
/// binding but has no effect on the request that reaches the provider.
/// </para>
/// <list type="table">
///   <listheader>
///     <term>Parameter</term>
///     <description>Agent Framework backend / IronHive backend</description>
///   </listheader>
///   <item><term>Temperature</term><description>applied / applied</description></item>
///   <item><term>MaxTokens</term><description>applied / applied</description></item>
///   <item><term>TopP</term><description>applied / applied</description></item>
///   <item><term>FrequencyPenalty</term><description>applied / <b>ignored</b> (logged as a warning)</description></item>
///   <item><term>PresencePenalty</term><description>applied / <b>ignored</b> (logged as a warning)</description></item>
/// </list>
/// <para>
/// The two ignored parameters have no field on the IronHive agent parameter contract, so the
/// adapter has nothing to forward them to. It emits a warning at agent creation instead of
/// dropping them silently. Provider support differs on top of this: penalties are native to
/// OpenAI-compatible APIs but absent from some others, so a backend reporting "applied" still
/// depends on the selected provider honouring it.
/// </para>
/// </remarks>
public record ModelConfig
{
    /// <summary>
    /// Provider name (e.g., "azure-openai", "openai")
    /// </summary>
    public string Provider { get; init; } = "azure-openai";

    /// <summary>
    /// Model deployment name (e.g., "gpt-4o", "gpt-4o-mini").
    /// Optional: when omitted, the model is resolved at invoke time from
    /// <see cref="ProcessOptions.ModelOverride"/>, or from the orchestrator's
    /// configured default model (IronbeesCoreOptions.DefaultModelDeployment).
    /// This supports environments where the active model is decided at runtime
    /// (e.g. selected from a database setting) rather than pinned in agent.yaml.
    /// </summary>
    public string? Deployment { get; init; }

    /// <summary>
    /// Temperature for response randomness (0.0 - 2.0)
    /// </summary>
    public double Temperature { get; init; } = 0.7;

    /// <summary>
    /// Maximum tokens in response
    /// </summary>
    public int MaxTokens { get; init; } = 4000;

    /// <summary>
    /// Top-p sampling parameter (0.0 - 1.0)
    /// </summary>
    public double? TopP { get; init; }

    /// <summary>
    /// Frequency penalty (-2.0 - 2.0).
    /// Ignored by the IronHive backend - see the support matrix on <see cref="ModelConfig"/>.
    /// </summary>
    public double? FrequencyPenalty { get; init; }

    /// <summary>
    /// Presence penalty (-2.0 - 2.0).
    /// Ignored by the IronHive backend - see the support matrix on <see cref="ModelConfig"/>.
    /// </summary>
    public double? PresencePenalty { get; init; }
}
