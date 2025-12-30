// Copyright (c) Ironbees. All rights reserved.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace Ironbees.Core.Guardrails;

/// <summary>
/// A guardrail that validates content against regular expression patterns.
/// </summary>
/// <remarks>
/// <para>
/// This guardrail blocks content matching any of the configured patterns.
/// Common use cases include:
/// - PII detection (email, phone, SSN, credit card patterns)
/// - URL/link filtering
/// - Code injection prevention
/// - Custom pattern matching
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var guardrail = new RegexGuardrail(new RegexGuardrailOptions
/// {
///     Name = "PII-Detector",
///     BlockedPatterns =
///     [
///         new PatternDefinition(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", "Email"),
///         new PatternDefinition(@"\b\d{3}-\d{2}-\d{4}\b", "SSN"),
///     ]
/// });
/// </code>
/// </example>
public sealed class RegexGuardrail : IContentGuardrail
{
    private readonly RegexGuardrailOptions _options;
    private readonly List<CompiledPattern> _compiledPatterns;

    /// <inheritdoc />
    public string Name => _options.Name;

    /// <summary>
    /// Initializes a new instance of the <see cref="RegexGuardrail"/> class.
    /// </summary>
    /// <param name="options">The configuration options.</param>
    public RegexGuardrail(RegexGuardrailOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;

        _compiledPatterns = options.BlockedPatterns
            .Select(p => new CompiledPattern
            {
                Pattern = new Regex(p.Pattern, options.RegexOptions | RegexOptions.Compiled),
                Definition = p
            })
            .ToList();
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

        foreach (var compiled in _compiledPatterns)
        {
            var matches = compiled.Pattern.Matches(content);

            foreach (Match match in matches)
            {
                var matchedContent = _options.IncludeMatchedContent
                    ? (match.Value.Length > _options.MaxMatchedContentLength
                        ? match.Value[.._options.MaxMatchedContentLength] + "..."
                        : match.Value)
                    : null;

                violations.Add(GuardrailViolation.CreateWithPosition(
                    compiled.Definition.ViolationType ?? "PatternMatch",
                    compiled.Definition.Description ?? $"Content matches blocked pattern: {compiled.Definition.Name}",
                    match.Index,
                    match.Length,
                    matchedContent));

                // Stop after first match if not finding all violations
                if (!_options.FindAllViolations)
                {
                    break;
                }
            }

            if (violations.Count > 0 && !_options.FindAllViolations)
            {
                break;
            }
        }

        if (violations.Count > 0)
        {
            return GuardrailResult.Blocked(
                Name,
                $"Content matched {violations.Count} blocked pattern(s)",
                violations);
        }

        return GuardrailResult.Allowed(Name);
    }

    private sealed class CompiledPattern
    {
        public required Regex Pattern { get; init; }
        public required PatternDefinition Definition { get; init; }
    }
}

/// <summary>
/// Configuration options for <see cref="RegexGuardrail"/>.
/// </summary>
public sealed class RegexGuardrailOptions
{
    /// <summary>
    /// Gets or sets the name of this guardrail.
    /// </summary>
    public string Name { get; set; } = "RegexGuardrail";

    /// <summary>
    /// Gets or sets the patterns to block.
    /// </summary>
    public IReadOnlyList<PatternDefinition> BlockedPatterns { get; set; } = [];

    /// <summary>
    /// Gets or sets the regex options to use for pattern matching.
    /// </summary>
    public RegexOptions RegexOptions { get; set; } = RegexOptions.IgnoreCase | RegexOptions.Multiline;

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

    /// <summary>
    /// Gets or sets the maximum length of matched content to include in violations.
    /// </summary>
    public int MaxMatchedContentLength { get; set; } = 50;
}

/// <summary>
/// Defines a pattern for the regex guardrail.
/// </summary>
public sealed class PatternDefinition
{
    /// <summary>
    /// Gets or sets the regular expression pattern.
    /// </summary>
    public required string Pattern { get; init; }

    /// <summary>
    /// Gets or sets a friendly name for this pattern.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets or sets a description of what this pattern detects.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets or sets the violation type to use when this pattern matches.
    /// </summary>
    public string? ViolationType { get; init; }

    /// <summary>
    /// Creates a new pattern definition.
    /// </summary>
    /// <param name="pattern">The regex pattern.</param>
    /// <param name="name">The pattern name.</param>
    /// <param name="description">The pattern description.</param>
    /// <returns>A new pattern definition.</returns>
    public static PatternDefinition Create(string pattern, string? name = null, string? description = null)
    {
        return new PatternDefinition
        {
            Pattern = pattern,
            Name = name,
            Description = description,
            ViolationType = name
        };
    }
}
