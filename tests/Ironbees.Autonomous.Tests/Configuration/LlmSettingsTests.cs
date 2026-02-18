using Ironbees.Autonomous.Configuration;
using Xunit;

namespace Ironbees.Autonomous.Tests.Configuration;

public class LlmSettingsTests
{
    // --- Defaults ---

    [Fact]
    public void Defaults_ShouldBeCorrect()
    {
        var settings = new LlmSettings();

        Assert.Null(settings.Endpoint);
        Assert.Null(settings.ApiKey);
        Assert.Null(settings.Model);
        Assert.Equal(200, settings.MaxOutputTokens);
        Assert.Equal(0.7f, settings.Temperature);
        Assert.Null(settings.TopP);
        Assert.Null(settings.FrequencyPenalty);
        Assert.Null(settings.PresencePenalty);
        Assert.Equal(60, settings.TimeoutSeconds);
        Assert.False(settings.EnableDebugOutput);
    }

    // --- ResolveApiKey ---

    [Fact]
    public void ResolveApiKey_Null_ShouldReturnEmpty()
    {
        var settings = new LlmSettings { ApiKey = null };
        Assert.Equal(string.Empty, settings.ResolveApiKey());
    }

    [Fact]
    public void ResolveApiKey_Empty_ShouldReturnEmpty()
    {
        var settings = new LlmSettings { ApiKey = "" };
        Assert.Equal(string.Empty, settings.ResolveApiKey());
    }

    [Fact]
    public void ResolveApiKey_PlainValue_ShouldReturnAsIs()
    {
        var settings = new LlmSettings { ApiKey = "sk-test-key-123" };
        Assert.Equal("sk-test-key-123", settings.ResolveApiKey());
    }

    [Fact]
    public void ResolveApiKey_EnvVarSyntax_ShouldResolveFromEnvironment()
    {
        var envName = $"TEST_API_KEY_{Guid.NewGuid():N}";
        try
        {
            Environment.SetEnvironmentVariable(envName, "resolved-key");
            var settings = new LlmSettings { ApiKey = $"${{{envName}}}" };
            Assert.Equal("resolved-key", settings.ResolveApiKey());
        }
        finally
        {
            Environment.SetEnvironmentVariable(envName, null);
        }
    }

    [Fact]
    public void ResolveApiKey_EnvVarMissing_ShouldReturnEmpty()
    {
        var settings = new LlmSettings { ApiKey = "${NONEXISTENT_VAR_ABCXYZ}" };
        Assert.Equal(string.Empty, settings.ResolveApiKey());
    }

    // --- ResolveEndpoint ---

    [Fact]
    public void ResolveEndpoint_Null_ShouldReturnNull()
    {
        var settings = new LlmSettings { Endpoint = null };
        Assert.Null(settings.ResolveEndpoint());
    }

    [Fact]
    public void ResolveEndpoint_ValidUrl_ShouldReturnUri()
    {
        var settings = new LlmSettings { Endpoint = "https://api.example.com/v1" };
        var uri = settings.ResolveEndpoint();

        Assert.NotNull(uri);
        Assert.Equal("https://api.example.com/v1", uri.ToString().TrimEnd('/'));
    }

    [Fact]
    public void ResolveEndpoint_InvalidUrl_ShouldReturnNull()
    {
        var settings = new LlmSettings { Endpoint = "not-a-url" };
        Assert.Null(settings.ResolveEndpoint());
    }

    [Fact]
    public void ResolveEndpoint_EnvVarSyntax_ShouldResolve()
    {
        var envName = $"TEST_ENDPOINT_{Guid.NewGuid():N}";
        try
        {
            Environment.SetEnvironmentVariable(envName, "https://resolved.example.com");
            var settings = new LlmSettings { Endpoint = $"${{{envName}}}" };
            var uri = settings.ResolveEndpoint();

            Assert.NotNull(uri);
            Assert.Contains("resolved.example.com", uri.ToString());
        }
        finally
        {
            Environment.SetEnvironmentVariable(envName, null);
        }
    }

    // --- ResolveModel ---

    [Fact]
    public void ResolveModel_Null_ShouldReturnEmpty()
    {
        var settings = new LlmSettings { Model = null };
        Assert.Equal(string.Empty, settings.ResolveModel());
    }

    [Fact]
    public void ResolveModel_PlainValue_ShouldReturnAsIs()
    {
        var settings = new LlmSettings { Model = "gpt-4" };
        Assert.Equal("gpt-4", settings.ResolveModel());
    }

    [Fact]
    public void ResolveModel_EnvVarSyntax_ShouldResolve()
    {
        var envName = $"TEST_MODEL_{Guid.NewGuid():N}";
        try
        {
            Environment.SetEnvironmentVariable(envName, "claude-3");
            var settings = new LlmSettings { Model = $"${{{envName}}}" };
            Assert.Equal("claude-3", settings.ResolveModel());
        }
        finally
        {
            Environment.SetEnvironmentVariable(envName, null);
        }
    }

    // --- Record with syntax ---

    [Fact]
    public void WithSyntax_ShouldCreateModifiedCopy()
    {
        var original = new LlmSettings { Model = "gpt-3.5", Temperature = 0.5f };
        var modified = original with { Model = "gpt-4" };

        Assert.Equal("gpt-4", modified.Model);
        Assert.Equal(0.5f, modified.Temperature);
        Assert.Equal("gpt-3.5", original.Model);
    }
}
