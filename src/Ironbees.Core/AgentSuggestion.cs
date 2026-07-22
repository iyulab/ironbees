namespace Ironbees.Core;

/// <summary>
/// A model-generated follow-up suggestion block returned alongside a response.
/// </summary>
/// <param name="Question">The question the model poses to the user.</param>
/// <param name="Items">Selectable suggestion items for the question.</param>
public record AgentSuggestion(string Question, IReadOnlyList<string> Items);
