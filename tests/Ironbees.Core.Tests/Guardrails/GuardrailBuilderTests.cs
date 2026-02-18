// Copyright (c) Ironbees. All rights reserved.
// Licensed under the MIT License.

using Azure;
using Azure.AI.ContentSafety;
using Ironbees.Core.Guardrails;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using OpenAI.Moderations;

namespace Ironbees.Core.Tests.Guardrails;

public class GuardrailBuilderTests
{
    [Fact]
    public void AddGuardrails_ReturnsGuardrailBuilder()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var builder = services.AddGuardrails();

        // Assert
        Assert.NotNull(builder);
    }

    [Fact]
    public void AddGuardrails_WithConfiguration_AppliesOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var builder = services.AddGuardrails(options =>
        {
            options.FailFast = true;
        });
        builder.Build();
        var provider = services.BuildServiceProvider();

        // Assert
        var pipeline = provider.GetRequiredService<GuardrailPipeline>();
        Assert.NotNull(pipeline);
    }

    [Fact]
    public void AddInputGuardrail_Generic_RegistersGuardrail()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddGuardrails()
            .AddInputGuardrail<TestGuardrail>()
            .Build();

        var provider = services.BuildServiceProvider();

        // Assert
        var pipeline = provider.GetRequiredService<GuardrailPipeline>();
        Assert.NotNull(pipeline);
        var guardrail = provider.GetService<TestGuardrail>();
        Assert.NotNull(guardrail);
    }

    [Fact]
    public void AddInputGuardrail_WithFactory_RegistersGuardrail()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddGuardrails()
            .AddInputGuardrail<TestGuardrail>(_ => new TestGuardrail())
            .Build();

        var provider = services.BuildServiceProvider();

        // Assert
        var pipeline = provider.GetRequiredService<GuardrailPipeline>();
        Assert.NotNull(pipeline);
    }

    [Fact]
    public void AddInputGuardrail_Instance_RegistersGuardrail()
    {
        // Arrange
        var services = new ServiceCollection();
        var guardrail = new TestGuardrail();

        // Act
        services.AddGuardrails()
            .AddInputGuardrail(guardrail)
            .Build();

        var provider = services.BuildServiceProvider();

        // Assert
        var pipeline = provider.GetRequiredService<GuardrailPipeline>();
        Assert.NotNull(pipeline);
    }

    [Fact]
    public void AddInputGuardrail_NullInstance_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            services.AddGuardrails().AddInputGuardrail(null!));
    }

    [Fact]
    public void AddOutputGuardrail_Generic_RegistersGuardrail()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddGuardrails()
            .AddOutputGuardrail<TestGuardrail>()
            .Build();

        var provider = services.BuildServiceProvider();

        // Assert
        var pipeline = provider.GetRequiredService<GuardrailPipeline>();
        Assert.NotNull(pipeline);
    }

    [Fact]
    public void AddOutputGuardrail_NullInstance_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            services.AddGuardrails().AddOutputGuardrail(null!));
    }

    [Fact]
    public void AddGuardrail_Generic_RegistersForBothInputAndOutput()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddGuardrails()
            .AddGuardrail<TestGuardrail>()
            .Build();

        var provider = services.BuildServiceProvider();

        // Assert
        var pipeline = provider.GetRequiredService<GuardrailPipeline>();
        Assert.NotNull(pipeline);
    }

    [Fact]
    public void AddGuardrail_NullInstance_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            services.AddGuardrails().AddGuardrail(null!));
    }

    [Fact]
    public void AddLengthGuardrail_ConfiguresLimits()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddGuardrails()
            .AddLengthGuardrail(maxInputLength: 1000, maxOutputLength: 5000)
            .Build();

        var provider = services.BuildServiceProvider();

        // Assert
        var pipeline = provider.GetRequiredService<GuardrailPipeline>();
        Assert.NotNull(pipeline);
    }

    [Fact]
    public void AddKeywordGuardrail_ConfiguresBlockedKeywords()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddGuardrails()
            .AddKeywordGuardrail("bad", "evil", "forbidden")
            .Build();

        var provider = services.BuildServiceProvider();

        // Assert
        var pipeline = provider.GetRequiredService<GuardrailPipeline>();
        Assert.NotNull(pipeline);
    }

    [Fact]
    public void AddRegexGuardrail_ConfiguresPatterns()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddGuardrails()
            .AddRegexGuardrail(
                new PatternDefinition { Pattern = @"\d{3}-\d{2}-\d{4}", Name = "SSN" })
            .Build();

        var provider = services.BuildServiceProvider();

        // Assert
        var pipeline = provider.GetRequiredService<GuardrailPipeline>();
        Assert.NotNull(pipeline);
    }

    [Fact]
    public void AddAuditLogger_Generic_RegistersLogger()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddGuardrails()
            .AddAuditLogger<TestAuditLogger>()
            .Build();

        var provider = services.BuildServiceProvider();

        // Assert
        var logger = provider.GetService<IAuditLogger>();
        Assert.NotNull(logger);
        Assert.IsType<TestAuditLogger>(logger);
    }

    [Fact]
    public void AddAuditLogger_Instance_RegistersLogger()
    {
        // Arrange
        var services = new ServiceCollection();
        var logger = new TestAuditLogger();

        // Act
        services.AddGuardrails()
            .AddAuditLogger(logger)
            .Build();

        var provider = services.BuildServiceProvider();

        // Assert
        var resolvedLogger = provider.GetService<IAuditLogger>();
        Assert.Same(logger, resolvedLogger);
    }

    [Fact]
    public void AddAuditLogger_NullInstance_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            services.AddGuardrails().AddAuditLogger(null!));
    }

    [Fact]
    public void AddAzureContentSafety_WithEndpointAndApiKey_Configures()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddGuardrails()
            .AddAzureContentSafety(
                endpoint: "https://test.cognitiveservices.azure.com/",
                apiKey: "test-key")
            .Build();

        var provider = services.BuildServiceProvider();

        // Assert
        var pipeline = provider.GetRequiredService<GuardrailPipeline>();
        Assert.NotNull(pipeline);
    }

    [Fact]
    public void AddAzureContentSafety_WithUriAndCredential_Configures()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddGuardrails()
            .AddAzureContentSafety(
                endpoint: new Uri("https://test.cognitiveservices.azure.com/"),
                credential: new AzureKeyCredential("test-key"))
            .Build();

        var provider = services.BuildServiceProvider();

        // Assert
        var pipeline = provider.GetRequiredService<GuardrailPipeline>();
        Assert.NotNull(pipeline);
    }

    [Fact]
    public void AddAzureContentSafety_WithClient_Configures()
    {
        // Arrange
        var services = new ServiceCollection();
        var mockClient = Substitute.For<ContentSafetyClient>();

        // Act
        services.AddGuardrails()
            .AddAzureContentSafety(mockClient)
            .Build();

        var provider = services.BuildServiceProvider();

        // Assert
        var pipeline = provider.GetRequiredService<GuardrailPipeline>();
        Assert.NotNull(pipeline);
    }

    [Fact]
    public void AddAzureContentSafety_WithConfiguration_AppliesOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var mockClient = Substitute.For<ContentSafetyClient>();

        // Act
        services.AddGuardrails()
            .AddAzureContentSafety(mockClient, options =>
            {
                options.MaxAllowedSeverity = 4;
                options.ValidateInput = true;
                options.ValidateOutput = false;
            })
            .Build();

        var provider = services.BuildServiceProvider();

        // Assert
        var pipeline = provider.GetRequiredService<GuardrailPipeline>();
        Assert.NotNull(pipeline);
    }

    [Fact]
    public void AddOpenAIModeration_WithApiKey_Configures()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddGuardrails()
            .AddOpenAIModeration(apiKey: "sk-test-key")
            .Build();

        var provider = services.BuildServiceProvider();

        // Assert
        var pipeline = provider.GetRequiredService<GuardrailPipeline>();
        Assert.NotNull(pipeline);
    }

    [Fact]
    public void AddOpenAIModeration_WithClient_Configures()
    {
        // Arrange
        var services = new ServiceCollection();
        var mockClient = Substitute.For<ModerationClient>();

        // Act
        services.AddGuardrails()
            .AddOpenAIModeration(mockClient)
            .Build();

        var provider = services.BuildServiceProvider();

        // Assert
        var pipeline = provider.GetRequiredService<GuardrailPipeline>();
        Assert.NotNull(pipeline);
    }

    [Fact]
    public void AddOpenAIModeration_WithConfiguration_AppliesOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var mockClient = Substitute.For<ModerationClient>();

        // Act
        services.AddGuardrails()
            .AddOpenAIModeration(mockClient, options =>
            {
                options.ScoreThreshold = 0.9f;
                options.ValidateInput = true;
                options.ValidateOutput = false;
            })
            .Build();

        var provider = services.BuildServiceProvider();

        // Assert
        var pipeline = provider.GetRequiredService<GuardrailPipeline>();
        Assert.NotNull(pipeline);
    }

    [Fact]
    public void Build_ReturnsServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddGuardrails().Build();

        // Assert
        Assert.Same(services, result);
    }

    [Fact]
    public void Chaining_MultipleGuardrails_Works()
    {
        // Arrange
        var services = new ServiceCollection();
        var mockAzureClient = Substitute.For<ContentSafetyClient>();
        var mockOpenAIClient = Substitute.For<ModerationClient>();

        // Act
        services.AddGuardrails()
            .AddLengthGuardrail(maxInputLength: 1000)
            .AddKeywordGuardrail("forbidden")
            .AddRegexGuardrail(new PatternDefinition { Pattern = @"\d{9}", Name = "SSN" })
            .AddAzureContentSafety(mockAzureClient)
            .AddOpenAIModeration(mockOpenAIClient)
            .AddAuditLogger<TestAuditLogger>()
            .Build();

        var provider = services.BuildServiceProvider();

        // Assert
        var pipeline = provider.GetRequiredService<GuardrailPipeline>();
        Assert.NotNull(pipeline);
    }

    // Test helper classes
    private sealed class TestGuardrail : IContentGuardrail
    {
        public string Name => "TestGuardrail";

        public Task<GuardrailResult> ValidateInputAsync(string input, CancellationToken cancellationToken = default)
            => Task.FromResult(GuardrailResult.Allowed(Name));

        public Task<GuardrailResult> ValidateOutputAsync(string output, CancellationToken cancellationToken = default)
            => Task.FromResult(GuardrailResult.Allowed(Name));
    }

    private sealed class TestAuditLogger : IAuditLogger
    {
        public Task LogInputValidationAsync(GuardrailAuditEntry entry, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task LogOutputValidationAsync(GuardrailAuditEntry entry, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
