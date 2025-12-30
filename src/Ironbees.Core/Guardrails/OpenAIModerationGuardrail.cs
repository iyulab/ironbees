// Copyright (c) Ironbees. All rights reserved.
// Licensed under the MIT License.

using System.ClientModel;
using OpenAI.Moderations;

namespace Ironbees.Core.Guardrails;

/// <summary>
/// A guardrail that validates content using OpenAI's Moderation API.
/// </summary>
/// <remarks>
/// <para>
/// This guardrail integrates with OpenAI's Moderation API to detect harmful content
/// across multiple categories including: sexual, hate, harassment, self-harm, violence,
/// and their subcategories. Each category returns a score (0-1) and a boolean flag.
/// </para>
/// <para>
/// Following the Thin Wrapper philosophy, this adapter delegates the actual content
/// analysis to OpenAI's Moderation API. Ironbees only provides the integration layer.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var client = new ModerationClient(
///     model: "omni-moderation-latest",
///     apiKey: Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
///
/// var guardrail = new OpenAIModerationGuardrail(client, new OpenAIModerationGuardrailOptions
/// {
///     Name = "OpenAI-Moderation",
///     ScoreThreshold = 0.7
/// });
/// </code>
/// </example>
public sealed class OpenAIModerationGuardrail : IContentGuardrail
{
    private readonly ModerationClient _client;
    private readonly OpenAIModerationGuardrailOptions _options;

    /// <inheritdoc />
    public string Name => _options.Name;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAIModerationGuardrail"/> class.
    /// </summary>
    /// <param name="client">The OpenAI Moderation client.</param>
    /// <param name="options">The configuration options.</param>
    public OpenAIModerationGuardrail(ModerationClient client, OpenAIModerationGuardrailOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
        _options = options ?? new OpenAIModerationGuardrailOptions();
    }

    /// <inheritdoc />
    public async Task<GuardrailResult> ValidateInputAsync(string input, CancellationToken cancellationToken = default)
    {
        if (!_options.ValidateInput)
        {
            return GuardrailResult.Allowed(Name);
        }

        return await ClassifyContentAsync(input, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<GuardrailResult> ValidateOutputAsync(string output, CancellationToken cancellationToken = default)
    {
        if (!_options.ValidateOutput)
        {
            return GuardrailResult.Allowed(Name);
        }

        return await ClassifyContentAsync(output, cancellationToken).ConfigureAwait(false);
    }

    private async Task<GuardrailResult> ClassifyContentAsync(string content, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(content))
        {
            return GuardrailResult.Allowed(Name);
        }

        try
        {
            var result = await _client.ClassifyTextAsync(content, cancellationToken).ConfigureAwait(false);
            return MapResultToGuardrailResult(result.Value);
        }
        catch (ClientResultException ex)
        {
            if (_options.FailOpen)
            {
                return CreateAllowedWithWarning($"OpenAI Moderation request failed: {ex.Message}");
            }

            return GuardrailResult.Blocked(
                Name,
                $"OpenAI Moderation request failed: {ex.Message}",
                GuardrailViolation.Create("ServiceError", ex.Message));
        }
    }

    private GuardrailResult MapResultToGuardrailResult(ModerationResult result)
    {
        var violations = new List<GuardrailViolation>();
        var metadata = new Dictionary<string, object>();

        // Check each category using the flat ModerationResult structure
        // Each property is a ModerationCategory with Flagged and Score
        CheckCategory("Sexual", result.Sexual, violations, metadata);
        CheckCategory("SexualMinors", result.SexualMinors, violations, metadata);
        CheckCategory("Hate", result.Hate, violations, metadata);
        CheckCategory("HateThreatening", result.HateThreatening, violations, metadata);
        CheckCategory("Harassment", result.Harassment, violations, metadata);
        CheckCategory("HarassmentThreatening", result.HarassmentThreatening, violations, metadata);
        CheckCategory("SelfHarm", result.SelfHarm, violations, metadata);
        CheckCategory("SelfHarmIntent", result.SelfHarmIntent, violations, metadata);
        CheckCategory("SelfHarmInstructions", result.SelfHarmInstructions, violations, metadata);
        CheckCategory("Violence", result.Violence, violations, metadata);
        CheckCategory("ViolenceGraphic", result.ViolenceGraphic, violations, metadata);

        // Add overall flagged status
        metadata["Flagged"] = result.Flagged;

        if (violations.Count > 0 || (result.Flagged && _options.BlockOnFlagged))
        {
            var reason = violations.Count > 0
                ? $"Content flagged by OpenAI Moderation: {violations.Count} violation(s)"
                : "Content flagged by OpenAI Moderation";

            return new GuardrailResult
            {
                IsAllowed = false,
                GuardrailName = Name,
                Reason = reason,
                Violations = violations,
                Metadata = metadata
            };
        }

        return new GuardrailResult
        {
            IsAllowed = true,
            GuardrailName = Name,
            Reason = "Content passed OpenAI Moderation validation",
            Metadata = metadata
        };
    }

    private void CheckCategory(
        string categoryName,
        ModerationCategory category,
        List<GuardrailViolation> violations,
        Dictionary<string, object> metadata)
    {
        var score = (float)category.Score;
        var flagged = category.Flagged;

        metadata[$"{categoryName}Score"] = score;
        metadata[$"{categoryName}Flagged"] = flagged;

        var threshold = GetThresholdForCategory(categoryName);
        var shouldBlock = _options.UseScoreThreshold
            ? score >= threshold
            : flagged;

        // Skip disabled categories
        if (!IsCategoryEnabled(categoryName))
        {
            return;
        }

        if (shouldBlock)
        {
            violations.Add(new GuardrailViolation
            {
                ViolationType = $"{categoryName}Content",
                Description = $"Content flagged for {categoryName} (score: {score:F4}, threshold: {threshold:F4})",
                Severity = MapScoreToSeverity(score),
                Context = new Dictionary<string, object>
                {
                    ["Category"] = categoryName,
                    ["Score"] = score,
                    ["Threshold"] = threshold,
                    ["Flagged"] = flagged
                }
            });
        }
    }

    private float GetThresholdForCategory(string categoryName)
    {
        return categoryName switch
        {
            "Sexual" => _options.SexualThreshold ?? _options.ScoreThreshold,
            "SexualMinors" => _options.SexualMinorsThreshold ?? _options.ScoreThreshold,
            "Hate" => _options.HateThreshold ?? _options.ScoreThreshold,
            "HateThreatening" => _options.HateThreateningThreshold ?? _options.ScoreThreshold,
            "Harassment" => _options.HarassmentThreshold ?? _options.ScoreThreshold,
            "HarassmentThreatening" => _options.HarassmentThreateningThreshold ?? _options.ScoreThreshold,
            "SelfHarm" => _options.SelfHarmThreshold ?? _options.ScoreThreshold,
            "SelfHarmIntent" => _options.SelfHarmIntentThreshold ?? _options.ScoreThreshold,
            "SelfHarmInstructions" => _options.SelfHarmInstructionsThreshold ?? _options.ScoreThreshold,
            "Violence" => _options.ViolenceThreshold ?? _options.ScoreThreshold,
            "ViolenceGraphic" => _options.ViolenceGraphicThreshold ?? _options.ScoreThreshold,
            _ => _options.ScoreThreshold
        };
    }

    private bool IsCategoryEnabled(string categoryName)
    {
        if (_options.EnabledCategories is { Count: > 0 })
        {
            return _options.EnabledCategories.Contains(categoryName);
        }

        if (_options.DisabledCategories is { Count: > 0 })
        {
            return !_options.DisabledCategories.Contains(categoryName);
        }

        return true;
    }

    private static ViolationSeverity MapScoreToSeverity(float score)
    {
        return score switch
        {
            < 0.3f => ViolationSeverity.Low,
            < 0.6f => ViolationSeverity.Medium,
            < 0.85f => ViolationSeverity.High,
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
/// Configuration options for <see cref="OpenAIModerationGuardrail"/>.
/// </summary>
public sealed class OpenAIModerationGuardrailOptions
{
    /// <summary>
    /// Gets or sets the name of this guardrail.
    /// </summary>
    public string Name { get; set; } = "OpenAIModeration";

    /// <summary>
    /// Gets or sets the default score threshold (0-1) for all categories.
    /// Content with scores at or above this threshold will be blocked.
    /// </summary>
    /// <remarks>
    /// OpenAI Moderation returns scores from 0 (safe) to 1 (definitely harmful).
    /// Default is 0.7 to block content with high confidence of being harmful.
    /// </remarks>
    public float ScoreThreshold { get; set; } = 0.7f;

    /// <summary>
    /// Gets or sets whether to use score thresholds or rely on the API's flagged status.
    /// </summary>
    /// <remarks>
    /// When true, uses <see cref="ScoreThreshold"/> and category-specific thresholds.
    /// When false, uses the boolean 'flagged' status from the API.
    /// Default is true for more granular control.
    /// </remarks>
    public bool UseScoreThreshold { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to block content when the API's overall 'flagged' status is true,
    /// even if no individual category exceeds the threshold.
    /// </summary>
    public bool BlockOnFlagged { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to validate input content.
    /// </summary>
    public bool ValidateInput { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to validate output content.
    /// </summary>
    public bool ValidateOutput { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to allow content when the service fails.
    /// </summary>
    /// <remarks>
    /// When true, content is allowed if the OpenAI service is unavailable or returns an error.
    /// When false, content is blocked on service failure. Default is false for safety.
    /// </remarks>
    public bool FailOpen { get; set; }

    /// <summary>
    /// Gets or sets the list of category names to enable. If empty, all categories are enabled.
    /// </summary>
    public ISet<string>? EnabledCategories { get; set; }

    /// <summary>
    /// Gets or sets the list of category names to disable.
    /// </summary>
    public ISet<string>? DisabledCategories { get; set; }

    #region Category-Specific Thresholds

    /// <summary>
    /// Gets or sets the threshold for sexual content.
    /// If null, uses <see cref="ScoreThreshold"/>.
    /// </summary>
    public float? SexualThreshold { get; set; }

    /// <summary>
    /// Gets or sets the threshold for sexual content involving minors.
    /// If null, uses <see cref="ScoreThreshold"/>.
    /// </summary>
    public float? SexualMinorsThreshold { get; set; }

    /// <summary>
    /// Gets or sets the threshold for hate content.
    /// If null, uses <see cref="ScoreThreshold"/>.
    /// </summary>
    public float? HateThreshold { get; set; }

    /// <summary>
    /// Gets or sets the threshold for hate/threatening content.
    /// If null, uses <see cref="ScoreThreshold"/>.
    /// </summary>
    public float? HateThreateningThreshold { get; set; }

    /// <summary>
    /// Gets or sets the threshold for harassment content.
    /// If null, uses <see cref="ScoreThreshold"/>.
    /// </summary>
    public float? HarassmentThreshold { get; set; }

    /// <summary>
    /// Gets or sets the threshold for harassment/threatening content.
    /// If null, uses <see cref="ScoreThreshold"/>.
    /// </summary>
    public float? HarassmentThreateningThreshold { get; set; }

    /// <summary>
    /// Gets or sets the threshold for self-harm content.
    /// If null, uses <see cref="ScoreThreshold"/>.
    /// </summary>
    public float? SelfHarmThreshold { get; set; }

    /// <summary>
    /// Gets or sets the threshold for self-harm/intent content.
    /// If null, uses <see cref="ScoreThreshold"/>.
    /// </summary>
    public float? SelfHarmIntentThreshold { get; set; }

    /// <summary>
    /// Gets or sets the threshold for self-harm/instructions content.
    /// If null, uses <see cref="ScoreThreshold"/>.
    /// </summary>
    public float? SelfHarmInstructionsThreshold { get; set; }

    /// <summary>
    /// Gets or sets the threshold for violence content.
    /// If null, uses <see cref="ScoreThreshold"/>.
    /// </summary>
    public float? ViolenceThreshold { get; set; }

    /// <summary>
    /// Gets or sets the threshold for violence/graphic content.
    /// If null, uses <see cref="ScoreThreshold"/>.
    /// </summary>
    public float? ViolenceGraphicThreshold { get; set; }

    #endregion
}
