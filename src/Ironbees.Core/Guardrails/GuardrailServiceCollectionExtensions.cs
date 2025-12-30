// Copyright (c) Ironbees. All rights reserved.
// Licensed under the MIT License.

using Azure;
using Azure.AI.ContentSafety;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using OpenAI.Moderations;

namespace Ironbees.Core.Guardrails;

/// <summary>
/// Extension methods for registering guardrails with dependency injection.
/// </summary>
public static class GuardrailServiceCollectionExtensions
{
    /// <summary>
    /// Adds guardrail services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>A builder for further guardrail configuration.</returns>
    public static GuardrailBuilder AddGuardrails(
        this IServiceCollection services,
        Action<GuardrailPipelineOptions>? configure = null)
    {
        var options = new GuardrailPipelineOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(options);

        return new GuardrailBuilder(services, options);
    }
}

/// <summary>
/// Builder for configuring guardrails.
/// </summary>
public sealed class GuardrailBuilder
{
    private readonly IServiceCollection _services;
    private readonly GuardrailPipelineOptions _options;
    private readonly List<ServiceDescriptor> _inputGuardrails = [];
    private readonly List<ServiceDescriptor> _outputGuardrails = [];

    internal GuardrailBuilder(IServiceCollection services, GuardrailPipelineOptions options)
    {
        _services = services;
        _options = options;
    }

    /// <summary>
    /// Adds an input guardrail.
    /// </summary>
    /// <typeparam name="TGuardrail">The guardrail type.</typeparam>
    /// <returns>This builder for chaining.</returns>
    public GuardrailBuilder AddInputGuardrail<TGuardrail>()
        where TGuardrail : class, IContentGuardrail
    {
        _services.AddSingleton<TGuardrail>();
        _inputGuardrails.Add(ServiceDescriptor.Singleton<IContentGuardrail>(
            sp => sp.GetRequiredService<TGuardrail>()));
        return this;
    }

    /// <summary>
    /// Adds an input guardrail with a factory.
    /// </summary>
    /// <typeparam name="TGuardrail">The guardrail type.</typeparam>
    /// <param name="factory">The factory function.</param>
    /// <returns>This builder for chaining.</returns>
    public GuardrailBuilder AddInputGuardrail<TGuardrail>(Func<IServiceProvider, TGuardrail> factory)
        where TGuardrail : class, IContentGuardrail
    {
        _services.AddSingleton(factory);
        _inputGuardrails.Add(ServiceDescriptor.Singleton<IContentGuardrail>(
            sp => sp.GetRequiredService<TGuardrail>()));
        return this;
    }

    /// <summary>
    /// Adds an input guardrail instance.
    /// </summary>
    /// <param name="guardrail">The guardrail instance.</param>
    /// <returns>This builder for chaining.</returns>
    public GuardrailBuilder AddInputGuardrail(IContentGuardrail guardrail)
    {
        ArgumentNullException.ThrowIfNull(guardrail);
        _inputGuardrails.Add(ServiceDescriptor.Singleton(guardrail));
        return this;
    }

    /// <summary>
    /// Adds an output guardrail.
    /// </summary>
    /// <typeparam name="TGuardrail">The guardrail type.</typeparam>
    /// <returns>This builder for chaining.</returns>
    public GuardrailBuilder AddOutputGuardrail<TGuardrail>()
        where TGuardrail : class, IContentGuardrail
    {
        _services.AddSingleton<TGuardrail>();
        _outputGuardrails.Add(ServiceDescriptor.Singleton<IContentGuardrail>(
            sp => sp.GetRequiredService<TGuardrail>()));
        return this;
    }

    /// <summary>
    /// Adds an output guardrail with a factory.
    /// </summary>
    /// <typeparam name="TGuardrail">The guardrail type.</typeparam>
    /// <param name="factory">The factory function.</param>
    /// <returns>This builder for chaining.</returns>
    public GuardrailBuilder AddOutputGuardrail<TGuardrail>(Func<IServiceProvider, TGuardrail> factory)
        where TGuardrail : class, IContentGuardrail
    {
        _services.AddSingleton(factory);
        _outputGuardrails.Add(ServiceDescriptor.Singleton<IContentGuardrail>(
            sp => sp.GetRequiredService<TGuardrail>()));
        return this;
    }

    /// <summary>
    /// Adds an output guardrail instance.
    /// </summary>
    /// <param name="guardrail">The guardrail instance.</param>
    /// <returns>This builder for chaining.</returns>
    public GuardrailBuilder AddOutputGuardrail(IContentGuardrail guardrail)
    {
        ArgumentNullException.ThrowIfNull(guardrail);
        _outputGuardrails.Add(ServiceDescriptor.Singleton(guardrail));
        return this;
    }

    /// <summary>
    /// Adds a guardrail for both input and output validation.
    /// </summary>
    /// <typeparam name="TGuardrail">The guardrail type.</typeparam>
    /// <returns>This builder for chaining.</returns>
    public GuardrailBuilder AddGuardrail<TGuardrail>()
        where TGuardrail : class, IContentGuardrail
    {
        _services.AddSingleton<TGuardrail>();
        var descriptor = ServiceDescriptor.Singleton<IContentGuardrail>(
            sp => sp.GetRequiredService<TGuardrail>());
        _inputGuardrails.Add(descriptor);
        _outputGuardrails.Add(descriptor);
        return this;
    }

    /// <summary>
    /// Adds a guardrail instance for both input and output validation.
    /// </summary>
    /// <param name="guardrail">The guardrail instance.</param>
    /// <returns>This builder for chaining.</returns>
    public GuardrailBuilder AddGuardrail(IContentGuardrail guardrail)
    {
        ArgumentNullException.ThrowIfNull(guardrail);
        _inputGuardrails.Add(ServiceDescriptor.Singleton(guardrail));
        _outputGuardrails.Add(ServiceDescriptor.Singleton(guardrail));
        return this;
    }

    /// <summary>
    /// Adds a length guardrail with the specified limits.
    /// </summary>
    /// <param name="maxInputLength">Maximum input length.</param>
    /// <param name="maxOutputLength">Maximum output length.</param>
    /// <returns>This builder for chaining.</returns>
    public GuardrailBuilder AddLengthGuardrail(int? maxInputLength = null, int? maxOutputLength = null)
    {
        var guardrail = new LengthGuardrail(new LengthGuardrailOptions
        {
            MaxInputLength = maxInputLength,
            MaxOutputLength = maxOutputLength
        });

        if (maxInputLength.HasValue)
        {
            _inputGuardrails.Add(ServiceDescriptor.Singleton<IContentGuardrail>(guardrail));
        }

        if (maxOutputLength.HasValue)
        {
            _outputGuardrails.Add(ServiceDescriptor.Singleton<IContentGuardrail>(guardrail));
        }

        return this;
    }

    /// <summary>
    /// Adds a keyword guardrail with the specified blocked keywords.
    /// </summary>
    /// <param name="blockedKeywords">Keywords to block.</param>
    /// <returns>This builder for chaining.</returns>
    public GuardrailBuilder AddKeywordGuardrail(params string[] blockedKeywords)
    {
        var guardrail = new KeywordGuardrail(new KeywordGuardrailOptions
        {
            BlockedKeywords = blockedKeywords
        });

        _inputGuardrails.Add(ServiceDescriptor.Singleton<IContentGuardrail>(guardrail));
        _outputGuardrails.Add(ServiceDescriptor.Singleton<IContentGuardrail>(guardrail));
        return this;
    }

    /// <summary>
    /// Adds a regex guardrail with the specified patterns.
    /// </summary>
    /// <param name="patterns">Patterns to block.</param>
    /// <returns>This builder for chaining.</returns>
    public GuardrailBuilder AddRegexGuardrail(params PatternDefinition[] patterns)
    {
        var guardrail = new RegexGuardrail(new RegexGuardrailOptions
        {
            BlockedPatterns = patterns
        });

        _inputGuardrails.Add(ServiceDescriptor.Singleton<IContentGuardrail>(guardrail));
        _outputGuardrails.Add(ServiceDescriptor.Singleton<IContentGuardrail>(guardrail));
        return this;
    }

    /// <summary>
    /// Adds an Azure AI Content Safety guardrail.
    /// </summary>
    /// <param name="endpoint">The Azure Content Safety endpoint URL.</param>
    /// <param name="apiKey">The API key for authentication.</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>This builder for chaining.</returns>
    public GuardrailBuilder AddAzureContentSafety(
        string endpoint,
        string apiKey,
        Action<AzureContentSafetyGuardrailOptions>? configure = null)
    {
        return AddAzureContentSafety(
            new Uri(endpoint),
            new AzureKeyCredential(apiKey),
            configure);
    }

    /// <summary>
    /// Adds an Azure AI Content Safety guardrail.
    /// </summary>
    /// <param name="endpoint">The Azure Content Safety endpoint URI.</param>
    /// <param name="credential">The Azure credential for authentication.</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>This builder for chaining.</returns>
    public GuardrailBuilder AddAzureContentSafety(
        Uri endpoint,
        AzureKeyCredential credential,
        Action<AzureContentSafetyGuardrailOptions>? configure = null)
    {
        var options = new AzureContentSafetyGuardrailOptions();
        configure?.Invoke(options);

        var client = new ContentSafetyClient(endpoint, credential);
        var guardrail = new AzureContentSafetyGuardrail(client, options);

        if (options.ValidateInput)
        {
            _inputGuardrails.Add(ServiceDescriptor.Singleton<IContentGuardrail>(guardrail));
        }

        if (options.ValidateOutput)
        {
            _outputGuardrails.Add(ServiceDescriptor.Singleton<IContentGuardrail>(guardrail));
        }

        return this;
    }

    /// <summary>
    /// Adds an Azure AI Content Safety guardrail with a pre-configured client.
    /// </summary>
    /// <param name="client">The Content Safety client.</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>This builder for chaining.</returns>
    public GuardrailBuilder AddAzureContentSafety(
        ContentSafetyClient client,
        Action<AzureContentSafetyGuardrailOptions>? configure = null)
    {
        var options = new AzureContentSafetyGuardrailOptions();
        configure?.Invoke(options);

        var guardrail = new AzureContentSafetyGuardrail(client, options);

        if (options.ValidateInput)
        {
            _inputGuardrails.Add(ServiceDescriptor.Singleton<IContentGuardrail>(guardrail));
        }

        if (options.ValidateOutput)
        {
            _outputGuardrails.Add(ServiceDescriptor.Singleton<IContentGuardrail>(guardrail));
        }

        return this;
    }

    /// <summary>
    /// Adds an OpenAI Moderation guardrail.
    /// </summary>
    /// <param name="apiKey">The OpenAI API key.</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <param name="model">The moderation model to use. Defaults to "omni-moderation-latest".</param>
    /// <returns>This builder for chaining.</returns>
    public GuardrailBuilder AddOpenAIModeration(
        string apiKey,
        Action<OpenAIModerationGuardrailOptions>? configure = null,
        string model = "omni-moderation-latest")
    {
        var options = new OpenAIModerationGuardrailOptions();
        configure?.Invoke(options);

        var client = new ModerationClient(model, apiKey);
        var guardrail = new OpenAIModerationGuardrail(client, options);

        if (options.ValidateInput)
        {
            _inputGuardrails.Add(ServiceDescriptor.Singleton<IContentGuardrail>(guardrail));
        }

        if (options.ValidateOutput)
        {
            _outputGuardrails.Add(ServiceDescriptor.Singleton<IContentGuardrail>(guardrail));
        }

        return this;
    }

    /// <summary>
    /// Adds an OpenAI Moderation guardrail with a pre-configured client.
    /// </summary>
    /// <param name="client">The Moderation client.</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>This builder for chaining.</returns>
    public GuardrailBuilder AddOpenAIModeration(
        ModerationClient client,
        Action<OpenAIModerationGuardrailOptions>? configure = null)
    {
        var options = new OpenAIModerationGuardrailOptions();
        configure?.Invoke(options);

        var guardrail = new OpenAIModerationGuardrail(client, options);

        if (options.ValidateInput)
        {
            _inputGuardrails.Add(ServiceDescriptor.Singleton<IContentGuardrail>(guardrail));
        }

        if (options.ValidateOutput)
        {
            _outputGuardrails.Add(ServiceDescriptor.Singleton<IContentGuardrail>(guardrail));
        }

        return this;
    }

    /// <summary>
    /// Adds an audit logger for tracking guardrail decisions.
    /// </summary>
    /// <typeparam name="TLogger">The audit logger type.</typeparam>
    /// <returns>This builder for chaining.</returns>
    public GuardrailBuilder AddAuditLogger<TLogger>()
        where TLogger : class, IAuditLogger
    {
        _services.AddSingleton<IAuditLogger, TLogger>();
        return this;
    }

    /// <summary>
    /// Adds an audit logger instance for tracking guardrail decisions.
    /// </summary>
    /// <param name="logger">The audit logger instance.</param>
    /// <returns>This builder for chaining.</returns>
    public GuardrailBuilder AddAuditLogger(IAuditLogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _services.AddSingleton(logger);
        return this;
    }

    /// <summary>
    /// Builds and registers the guardrail pipeline.
    /// </summary>
    /// <returns>The service collection for chaining.</returns>
    public IServiceCollection Build()
    {
        _services.AddSingleton<GuardrailPipeline>(sp =>
        {
            var inputGuardrails = _inputGuardrails
                .Select(d => d.ImplementationInstance as IContentGuardrail
                    ?? (d.ImplementationFactory?.Invoke(sp) as IContentGuardrail)
                    ?? sp.GetRequiredService(d.ServiceType) as IContentGuardrail)
                .Where(g => g != null)
                .Cast<IContentGuardrail>()
                .ToList();

            var outputGuardrails = _outputGuardrails
                .Select(d => d.ImplementationInstance as IContentGuardrail
                    ?? (d.ImplementationFactory?.Invoke(sp) as IContentGuardrail)
                    ?? sp.GetRequiredService(d.ServiceType) as IContentGuardrail)
                .Where(g => g != null)
                .Cast<IContentGuardrail>()
                .ToList();

            var logger = sp.GetService<ILogger<GuardrailPipeline>>();

            return new GuardrailPipeline(inputGuardrails, outputGuardrails, _options, logger);
        });

        return _services;
    }
}
