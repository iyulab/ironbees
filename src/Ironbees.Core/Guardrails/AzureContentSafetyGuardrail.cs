// Copyright (c) Ironbees. All rights reserved.
// Licensed under the MIT License.

using Azure;
using Azure.AI.ContentSafety;

namespace Ironbees.Core.Guardrails;

/// <summary>
/// A guardrail that validates content using Azure AI Content Safety service.
/// </summary>
/// <remarks>
/// <para>
/// This guardrail integrates with Azure AI Content Safety to detect harmful content
/// across four categories: Hate, Self-Harm, Sexual, and Violence. Each category
/// returns a severity level (0-6) that can be compared against configurable thresholds.
/// </para>
/// <para>
/// Following the Thin Wrapper philosophy, this adapter delegates the actual content
/// analysis to Azure AI Content Safety. Ironbees only provides the integration layer.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var client = new ContentSafetyClient(
///     new Uri("https://your-resource.cognitiveservices.azure.com/"),
///     new AzureKeyCredential("your-api-key"));
///
/// var guardrail = new AzureContentSafetyGuardrail(client, new AzureContentSafetyGuardrailOptions
/// {
///     Name = "Azure-Content-Safety",
///     MaxAllowedSeverity = 2
/// });
/// </code>
/// </example>
public sealed class AzureContentSafetyGuardrail : IContentGuardrail
{
    private readonly ContentSafetyClient _client;
    private readonly AzureContentSafetyGuardrailOptions _options;

    /// <inheritdoc />
    public string Name => _options.Name;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureContentSafetyGuardrail"/> class.
    /// </summary>
    /// <param name="client">The Azure Content Safety client.</param>
    /// <param name="options">The configuration options.</param>
    public AzureContentSafetyGuardrail(ContentSafetyClient client, AzureContentSafetyGuardrailOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
        _options = options ?? new AzureContentSafetyGuardrailOptions();
    }

    /// <inheritdoc />
    public async Task<GuardrailResult> ValidateInputAsync(string input, CancellationToken cancellationToken = default)
    {
        if (!_options.ValidateInput)
        {
            return GuardrailResult.Allowed(Name);
        }

        return await AnalyzeContentAsync(input, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<GuardrailResult> ValidateOutputAsync(string output, CancellationToken cancellationToken = default)
    {
        if (!_options.ValidateOutput)
        {
            return GuardrailResult.Allowed(Name);
        }

        return await AnalyzeContentAsync(output, cancellationToken).ConfigureAwait(false);
    }

    private async Task<GuardrailResult> AnalyzeContentAsync(string content, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(content))
        {
            return GuardrailResult.Allowed(Name);
        }

        try
        {
            var request = new AnalyzeTextOptions(content);

            // Apply blocklists if configured
            foreach (var blocklist in _options.BlocklistNames)
            {
                request.BlocklistNames.Add(blocklist);
            }

            if (_options.HaltOnBlocklistHit.HasValue)
            {
                request.HaltOnBlocklistHit = _options.HaltOnBlocklistHit.Value;
            }

            var response = await _client.AnalyzeTextAsync(request, cancellationToken).ConfigureAwait(false);
            return MapResponseToResult(response.Value);
        }
        catch (RequestFailedException ex)
        {
            if (_options.FailOpen)
            {
                return CreateAllowedWithWarning($"Azure Content Safety request failed: {ex.Message}");
            }

            return GuardrailResult.Blocked(
                Name,
                $"Azure Content Safety request failed: {ex.Message}",
                GuardrailViolation.Create("ServiceError", ex.Message));
        }
    }

    private GuardrailResult MapResponseToResult(AnalyzeTextResult response)
    {
        var violations = new List<GuardrailViolation>();
        var metadata = new Dictionary<string, object>();

        // Check blocklist matches first
        if (response.BlocklistsMatch is { Count: > 0 })
        {
            foreach (var match in response.BlocklistsMatch)
            {
                violations.Add(new GuardrailViolation
                {
                    ViolationType = "BlocklistMatch",
                    Description = $"Matched blocklist: {match.BlocklistName}",
                    Severity = ViolationSeverity.High,
                    MatchedContent = _options.IncludeMatchedContent ? match.BlocklistItemText : null,
                    Context = new Dictionary<string, object>
                    {
                        ["BlocklistName"] = match.BlocklistName,
                        ["BlocklistItemId"] = match.BlocklistItemId
                    }
                });
            }
        }

        // Check category analysis
        foreach (var category in response.CategoriesAnalysis)
        {
            var severity = category.Severity ?? 0;
            var categoryName = category.Category.ToString();
            metadata[$"{categoryName}Severity"] = severity;

            // Check if this category should be blocked
            var threshold = GetThresholdForCategory(category.Category);
            if (severity > threshold)
            {
                violations.Add(new GuardrailViolation
                {
                    ViolationType = $"{categoryName}Content",
                    Description = $"Content flagged for {categoryName} (severity: {severity}, threshold: {threshold})",
                    Severity = MapSeverity(severity),
                    Context = new Dictionary<string, object>
                    {
                        ["Category"] = categoryName,
                        ["Severity"] = severity,
                        ["Threshold"] = threshold
                    }
                });
            }
        }

        if (violations.Count > 0)
        {
            return new GuardrailResult
            {
                IsAllowed = false,
                GuardrailName = Name,
                Reason = $"Content flagged by Azure AI Content Safety: {violations.Count} violation(s)",
                Violations = violations,
                Metadata = metadata
            };
        }

        return new GuardrailResult
        {
            IsAllowed = true,
            GuardrailName = Name,
            Reason = "Content passed Azure AI Content Safety validation",
            Metadata = metadata
        };
    }

    private int GetThresholdForCategory(TextCategory category)
    {
        // Using if-else instead of switch expression because TextCategory
        // is an extensible enum that doesn't support constant pattern matching
        if (category == TextCategory.Hate)
        {
            return _options.HateSeverityThreshold ?? _options.MaxAllowedSeverity;
        }

        if (category == TextCategory.SelfHarm)
        {
            return _options.SelfHarmSeverityThreshold ?? _options.MaxAllowedSeverity;
        }

        if (category == TextCategory.Sexual)
        {
            return _options.SexualSeverityThreshold ?? _options.MaxAllowedSeverity;
        }

        if (category == TextCategory.Violence)
        {
            return _options.ViolenceSeverityThreshold ?? _options.MaxAllowedSeverity;
        }

        return _options.MaxAllowedSeverity;
    }

    private static ViolationSeverity MapSeverity(int azureSeverity)
    {
        return azureSeverity switch
        {
            <= 1 => ViolationSeverity.Low,
            <= 3 => ViolationSeverity.Medium,
            <= 5 => ViolationSeverity.High,
            _ => ViolationSeverity.Critical
        };
    }

    private GuardrailResult CreateAllowedWithWarning(string warning)
    {
        return new GuardrailResult
        {
            IsAllowed = true,
            GuardrailName = Name,
            Reason = warning,
            Metadata = new Dictionary<string, object>
            {
                ["Warning"] = warning,
                ["FailOpen"] = true
            }
        };
    }
}

/// <summary>
/// Configuration options for <see cref="AzureContentSafetyGuardrail"/>.
/// </summary>
public sealed class AzureContentSafetyGuardrailOptions
{
    /// <summary>
    /// Gets or sets the name of this guardrail.
    /// </summary>
    public string Name { get; set; } = "AzureContentSafety";

    /// <summary>
    /// Gets or sets the maximum allowed severity level (0-6) for all categories.
    /// Content exceeding this threshold will be blocked.
    /// </summary>
    /// <remarks>
    /// Azure Content Safety returns severity levels from 0 (safe) to 6 (most severe).
    /// Default is 2, which blocks medium severity and above.
    /// </remarks>
    public int MaxAllowedSeverity { get; set; } = 2;

    /// <summary>
    /// Gets or sets the severity threshold for hate content.
    /// If null, uses <see cref="MaxAllowedSeverity"/>.
    /// </summary>
    public int? HateSeverityThreshold { get; set; }

    /// <summary>
    /// Gets or sets the severity threshold for self-harm content.
    /// If null, uses <see cref="MaxAllowedSeverity"/>.
    /// </summary>
    public int? SelfHarmSeverityThreshold { get; set; }

    /// <summary>
    /// Gets or sets the severity threshold for sexual content.
    /// If null, uses <see cref="MaxAllowedSeverity"/>.
    /// </summary>
    public int? SexualSeverityThreshold { get; set; }

    /// <summary>
    /// Gets or sets the severity threshold for violence content.
    /// If null, uses <see cref="MaxAllowedSeverity"/>.
    /// </summary>
    public int? ViolenceSeverityThreshold { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to validate input content.
    /// </summary>
    public bool ValidateInput { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to validate output content.
    /// </summary>
    public bool ValidateOutput { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to include matched content in violations.
    /// </summary>
    public bool IncludeMatchedContent { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to allow content when the service fails.
    /// </summary>
    /// <remarks>
    /// When true, content is allowed if the Azure service is unavailable or returns an error.
    /// When false, content is blocked on service failure. Default is false for safety.
    /// </remarks>
    public bool FailOpen { get; set; }

    /// <summary>
    /// Gets or sets the blocklist names to check against.
    /// </summary>
    public IList<string> BlocklistNames { get; set; } = [];

    /// <summary>
    /// Gets or sets whether to halt on blocklist hit.
    /// </summary>
    public bool? HaltOnBlocklistHit { get; set; }
}
