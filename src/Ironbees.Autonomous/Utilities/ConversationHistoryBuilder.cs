using System.Text;

namespace Ironbees.Autonomous.Utilities;

/// <summary>
/// Builds formatted conversation history for LLM context.
/// Configurable format and deduction extraction.
/// </summary>
public class ConversationHistoryBuilder
{
    private readonly HistoryFormatOptions _options;
    private readonly List<ConversationTurn> _turns = [];

    public ConversationHistoryBuilder(HistoryFormatOptions? options = null)
    {
        _options = options ?? HistoryFormatOptions.Default;
    }

    /// <summary>
    /// Add a conversation turn
    /// </summary>
    public ConversationHistoryBuilder AddTurn(int number, string input, string output, string? metadata = null)
    {
        _turns.Add(new ConversationTurn
        {
            Number = number,
            Input = input,
            Output = output,
            Metadata = metadata
        });
        return this;
    }

    /// <summary>
    /// Add multiple turns from a collection
    /// </summary>
    public ConversationHistoryBuilder AddTurns<T>(
        IEnumerable<T> items,
        Func<T, int> numberSelector,
        Func<T, string> inputSelector,
        Func<T, string> outputSelector,
        Func<T, string?>? metadataSelector = null)
    {
        foreach (var item in items)
        {
            _turns.Add(new ConversationTurn
            {
                Number = numberSelector(item),
                Input = inputSelector(item),
                Output = outputSelector(item),
                Metadata = metadataSelector?.Invoke(item)
            });
        }
        return this;
    }

    /// <summary>
    /// Build the formatted history string
    /// </summary>
    public string Build()
    {
        if (_turns.Count == 0)
        {
            return _options.EmptyHistoryMessage;
        }

        var sb = new StringBuilder();

        // Header
        if (!string.IsNullOrEmpty(_options.HeaderText))
        {
            sb.AppendLine(_options.HeaderText);
            sb.AppendLine();
        }

        // Turns
        foreach (var turn in _turns)
        {
            var inputLabel = _options.InputLabel.Replace("{n}", turn.Number.ToString());
            var outputLabel = _options.OutputLabel.Replace("{n}", turn.Number.ToString());

            sb.AppendLine($"{inputLabel}: {turn.Input}");
            sb.Append($"{outputLabel}: {turn.Output}");

            if (!string.IsNullOrEmpty(turn.Metadata))
            {
                sb.Append($" ({turn.Metadata})");
            }
            sb.AppendLine();
            sb.AppendLine();
        }

        // Deductions
        if (_options.IncludeDeductions)
        {
            var deductions = BuildDeductions();
            if (!string.IsNullOrEmpty(deductions))
            {
                sb.AppendLine(_options.DeductionsHeader);
                sb.AppendLine(deductions);
            }
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Build a summary of deductions from the conversation
    /// </summary>
    public string BuildDeductions()
    {
        var sb = new StringBuilder();

        var positiveOutputs = _turns
            .Where(t => IsPositiveResponse(t.Output))
            .Select(t => t.Input.TrimEnd('?'))
            .ToList();

        var negativeOutputs = _turns
            .Where(t => IsNegativeResponse(t.Output))
            .Select(t => t.Input.TrimEnd('?'))
            .ToList();

        if (positiveOutputs.Count > 0)
        {
            sb.AppendLine($"- Confirmed YES: {string.Join(", ", positiveOutputs)}");
        }
        if (negativeOutputs.Count > 0)
        {
            sb.AppendLine($"- Confirmed NO: {string.Join(", ", negativeOutputs)}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Get turns matching a filter
    /// </summary>
    public IReadOnlyList<ConversationTurn> GetTurns(Func<ConversationTurn, bool>? filter = null)
    {
        return filter == null
            ? _turns.AsReadOnly()
            : _turns.Where(filter).ToList().AsReadOnly();
    }

    /// <summary>
    /// Clear all turns
    /// </summary>
    public void Clear() => _turns.Clear();

    /// <summary>
    /// Check if response is positive (yes, true, correct, etc.)
    /// </summary>
    protected virtual bool IsPositiveResponse(string output)
    {
        var lower = output.ToLowerInvariant();
        return lower.StartsWith("yes") ||
               lower.Contains("\"yes\"") ||
               lower.Contains("\"answer\":\"yes\"") ||
               lower.Contains("\"answer\": \"yes\"");
    }

    /// <summary>
    /// Check if response is negative (no, false, incorrect, etc.)
    /// </summary>
    protected virtual bool IsNegativeResponse(string output)
    {
        var lower = output.ToLowerInvariant();
        return lower.StartsWith("no") ||
               lower.Contains("\"no\"") ||
               lower.Contains("\"answer\":\"no\"") ||
               lower.Contains("\"answer\": \"no\"");
    }
}

/// <summary>
/// A single turn in the conversation
/// </summary>
public record ConversationTurn
{
    public int Number { get; init; }
    public required string Input { get; init; }
    public required string Output { get; init; }
    public string? Metadata { get; init; }
}

/// <summary>
/// Options for formatting conversation history
/// </summary>
public record HistoryFormatOptions
{
    /// <summary>Header text before history</summary>
    public string HeaderText { get; init; } = "=== CONVERSATION HISTORY ===";

    /// <summary>Label for input (supports {n} for number)</summary>
    public string InputLabel { get; init; } = "Q{n}";

    /// <summary>Label for output (supports {n} for number)</summary>
    public string OutputLabel { get; init; } = "A";

    /// <summary>Message when history is empty</summary>
    public string EmptyHistoryMessage { get; init; } = "No previous conversation.";

    /// <summary>Whether to include deductions section</summary>
    public bool IncludeDeductions { get; init; } = true;

    /// <summary>Header for deductions section</summary>
    public string DeductionsHeader { get; init; } = "=== DEDUCTIONS ===";

    /// <summary>Default options</summary>
    public static HistoryFormatOptions Default { get; } = new();

    /// <summary>Options for Q and A games</summary>
    public static HistoryFormatOptions QnA { get; } = new()
    {
        HeaderText = "=== QUESTION HISTORY ===\nThese questions have already been asked. DO NOT repeat them:",
        InputLabel = "Q{n}",
        OutputLabel = "A",
        EmptyHistoryMessage = "No questions asked yet. Start with broad category questions.",
        IncludeDeductions = true,
        DeductionsHeader = "=== DEDUCTIONS ===\nBased on the answers above, consider what you now know:"
    };
}
