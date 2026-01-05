namespace Ironbees.AgentMode.Configuration;

/// <summary>
/// Supported LLM providers.
/// </summary>
public enum LLMProvider
{
    /// <summary>
    /// Azure OpenAI Service (enterprise).
    /// </summary>
    AzureOpenAI,

    /// <summary>
    /// OpenAI official API.
    /// </summary>
    OpenAI,

    /// <summary>
    /// Anthropic Claude API.
    /// </summary>
    Anthropic,

    /// <summary>
    /// OpenAI-compatible APIs (GPUStack, local LLMs, etc).
    /// </summary>
    OpenAICompatible
}
