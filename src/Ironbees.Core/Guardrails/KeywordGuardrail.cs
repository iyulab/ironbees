// Copyright (c) Ironbees. All rights reserved.
// Licensed under the MIT License.

namespace Ironbees.Core.Guardrails;

/// <summary>
/// A guardrail that validates content against a list of blocked keywords.
/// </summary>
/// <remarks>
/// <para>
/// This guardrail blocks content containing any of the configured keywords.
/// Common use cases include:
/// - Profanity filtering
/// - Hate speech detection
/// - Brand name protection
/// - Competitor mention blocking
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var guardrail = new KeywordGuardrail(new KeywordGuardrailOptions
/// {
///     Name = "Profanity-Filter",
///     BlockedKeywords = ["badword1", "badword2"],
///     CaseSensitive = false
/// });
/// </code>
/// </example>
public sealed class KeywordGuardrail : IContentGuardrail
{
    private readonly KeywordGuardrailOptions _options;
    private readonly HashSet<string> _blockedKeywords;
    private readonly StringComparison _comparison;

    /// <inheritdoc />
    public string Name => _options.Name;

    /// <summary>
    /// Initializes a new instance of the <see cref="KeywordGuardrail"/> class.
    /// </summary>
    /// <param name="options">The configuration options.</param>
    public KeywordGuardrail(KeywordGuardrailOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;

        _comparison = options.CaseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        var comparer = options.CaseSensitive
            ? StringComparer.Ordinal
            : StringComparer.OrdinalIgnoreCase;

        _blockedKeywords = new HashSet<string>(options.BlockedKeywords, comparer);
    }

    /// <inheritdoc />
    public Task<GuardrailResult> ValidateInputAsync(string input, CancellationToken cancellationToken = default)
    {
        if (!_options.ValidateInput)
        {
            return Task.FromResult(GuardrailResult.Allowed(Name));
        }

        return Task.FromResult(Validate(input));
    }

    /// <inheritdoc />
    public Task<GuardrailResult> ValidateOutputAsync(string output, CancellationToken cancellationToken = default)
    {
        if (!_options.ValidateOutput)
        {
            return Task.FromResult(GuardrailResult.Allowed(Name));
        }

        return Task.FromResult(Validate(output));
    }

    private GuardrailResult Validate(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return GuardrailResult.Allowed(Name);
        }

        var violations = new List<GuardrailViolation>();

        if (_options.WholeWordOnly)
        {
            ValidateWholeWords(content, violations);
        }
        else
        {
            ValidateSubstrings(content, violations);
        }

        if (violations.Count > 0)
        {
            return GuardrailResult.Blocked(
                Name,
                $"Content contains {violations.Count} blocked keyword(s)",
                violations);
        }

        return GuardrailResult.Allowed(Name);
    }

    private void ValidateWholeWords(string content, List<GuardrailViolation> violations)
    {
        var words = ExtractWords(content);

        foreach (var (word, position) in words)
        {
            if (_blockedKeywords.Contains(word))
            {
                var matchedContent = _options.IncludeMatchedContent ? word : null;

                violations.Add(GuardrailViolation.CreateWithPosition(
                    "BlockedKeyword",
                    $"Content contains blocked keyword",
                    position,
                    word.Length,
                    matchedContent));

                if (!_options.FindAllViolations)
                {
                    return;
                }
            }
        }
    }

    private void ValidateSubstrings(string content, List<GuardrailViolation> violations)
    {
        foreach (var keyword in _blockedKeywords)
        {
            var index = 0;
            while ((index = content.IndexOf(keyword, index, _comparison)) >= 0)
            {
                var matchedContent = _options.IncludeMatchedContent
                    ? content.Substring(index, keyword.Length)
                    : null;

                violations.Add(GuardrailViolation.CreateWithPosition(
                    "BlockedKeyword",
                    "Content contains blocked keyword",
                    index,
                    keyword.Length,
                    matchedContent));

                if (!_options.FindAllViolations)
                {
                    return;
                }

                index += keyword.Length;
            }
        }
    }

    private static IEnumerable<(string Word, int Position)> ExtractWords(string content)
    {
        var wordStart = -1;

        for (var i = 0; i <= content.Length; i++)
        {
            var isWordChar = i < content.Length && (char.IsLetterOrDigit(content[i]) || content[i] == '_');

            if (isWordChar)
            {
                if (wordStart < 0)
                {
                    wordStart = i;
                }
            }
            else if (wordStart >= 0)
            {
                yield return (content[wordStart..i], wordStart);
                wordStart = -1;
            }
        }
    }
}

/// <summary>
/// Configuration options for <see cref="KeywordGuardrail"/>.
/// </summary>
public sealed class KeywordGuardrailOptions
{
    /// <summary>
    /// Gets or sets the name of this guardrail.
    /// </summary>
    public string Name { get; set; } = "KeywordGuardrail";

    /// <summary>
    /// Gets or sets the keywords to block.
    /// </summary>
    public IReadOnlyList<string> BlockedKeywords { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether keyword matching is case-sensitive.
    /// </summary>
    public bool CaseSensitive { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to match whole words only.
    /// </summary>
    public bool WholeWordOnly { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to validate input content.
    /// </summary>
    public bool ValidateInput { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to validate output content.
    /// </summary>
    public bool ValidateOutput { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to find all violations or stop at first.
    /// </summary>
    public bool FindAllViolations { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to include matched content in violations.
    /// </summary>
    public bool IncludeMatchedContent { get; set; } = true;
}
