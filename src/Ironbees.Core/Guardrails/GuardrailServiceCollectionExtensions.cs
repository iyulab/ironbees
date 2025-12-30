// Copyright (c) Ironbees. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

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
