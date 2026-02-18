using Ironbees.Core.Middleware;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;

namespace Ironbees.Core.Tests.Middleware;

public class CachingMiddlewareTests : IDisposable
{
    private readonly IChatClient _innerClient;
    private readonly MemoryCache _cache;
    private readonly ChatResponse _defaultResponse;

    public CachingMiddlewareTests()
    {
        _innerClient = Substitute.For<IChatClient>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _defaultResponse = new ChatResponse(
            new ChatMessage(ChatRole.Assistant, "This is a test response long enough to cache"));

        _innerClient.GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(_defaultResponse);
    }

    public void Dispose()
    {
        _cache.Dispose();
        GC.SuppressFinalize(this);
    }

    // --- Constructor ---

    [Fact]
    public void Constructor_NullCache_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CachingMiddleware(_innerClient, null!));
    }

    // --- Cache miss / hit ---

    [Fact]
    public async Task GetResponse_CacheMiss_ShouldCallInnerClient()
    {
        var middleware = CreateMiddleware();

        await middleware.GetResponseAsync(Messages("Hello"));

        await _innerClient.Received(1).GetResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(),
            Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetResponse_CacheHit_ShouldReturnCachedAndCallInnerOnce()
    {
        var middleware = CreateMiddleware();
        var messages = Messages("Hello");

        var first = await middleware.GetResponseAsync(messages);
        var second = await middleware.GetResponseAsync(messages);

        Assert.Same(first, second);
        await _innerClient.Received(1).GetResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(),
            Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetResponse_DifferentMessages_ShouldCallInnerAgain()
    {
        var middleware = CreateMiddleware();

        await middleware.GetResponseAsync(Messages("Hello"));
        await middleware.GetResponseAsync(Messages("World"));

        await _innerClient.Received(2).GetResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(),
            Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>());
    }

    // --- ShouldSkipCache ---

    [Fact]
    public async Task GetResponse_SkipCacheBoolTrue_ShouldBypassCache()
    {
        var middleware = CreateMiddleware();
        var messages = Messages("Hello");
        var opts = new ChatOptions
        {
            AdditionalProperties = new AdditionalPropertiesDictionary { ["SkipCache"] = true }
        };

        await middleware.GetResponseAsync(messages, opts);
        await middleware.GetResponseAsync(messages, opts);

        await _innerClient.Received(2).GetResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(),
            Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetResponse_SkipCacheStringTrue_ShouldBypassCache()
    {
        var middleware = CreateMiddleware();
        var messages = Messages("Hello");
        var opts = new ChatOptions
        {
            AdditionalProperties = new AdditionalPropertiesDictionary { ["SkipCache"] = "true" }
        };

        await middleware.GetResponseAsync(messages, opts);
        await middleware.GetResponseAsync(messages, opts);

        await _innerClient.Received(2).GetResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(),
            Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetResponse_OnlyCacheDeterministic_WithTemperature_ShouldBypass()
    {
        var middleware = CreateMiddleware(new CachingOptions { OnlyCacheDeterministic = true });
        var messages = Messages("Hello");
        var opts = new ChatOptions { Temperature = 0.7f };

        await middleware.GetResponseAsync(messages, opts);
        await middleware.GetResponseAsync(messages, opts);

        await _innerClient.Received(2).GetResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(),
            Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetResponse_OnlyCacheDeterministic_ZeroTemperature_ShouldCache()
    {
        var middleware = CreateMiddleware(new CachingOptions { OnlyCacheDeterministic = true });
        var messages = Messages("Hello");
        var opts = new ChatOptions { Temperature = 0 };

        await middleware.GetResponseAsync(messages, opts);
        await middleware.GetResponseAsync(messages, opts);

        await _innerClient.Received(1).GetResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(),
            Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetResponse_OnlyCacheDeterministicFalse_WithTemperature_ShouldCache()
    {
        var middleware = CreateMiddleware(new CachingOptions { OnlyCacheDeterministic = false });
        var messages = Messages("Hello");
        var opts = new ChatOptions { Temperature = 0.7f };

        await middleware.GetResponseAsync(messages, opts);
        await middleware.GetResponseAsync(messages, opts);

        await _innerClient.Received(1).GetResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(),
            Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>());
    }

    // --- ShouldCacheResponse ---

    [Fact]
    public async Task GetResponse_ErrorResponse_ShouldNotCache()
    {
        var errorResponse = new ChatResponse(
            new ChatMessage(ChatRole.Assistant, "Error occurred and this text is long enough"))
        {
            AdditionalProperties = new AdditionalPropertiesDictionary { ["Error"] = "fail" }
        };
        _innerClient.GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(errorResponse);

        var middleware = CreateMiddleware();
        var messages = Messages("Hello");

        await middleware.GetResponseAsync(messages);
        await middleware.GetResponseAsync(messages);

        await _innerClient.Received(2).GetResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(),
            Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetResponse_EmptyMessages_ShouldNotCache()
    {
        var emptyResponse = new ChatResponse([]);
        _innerClient.GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(emptyResponse);

        var middleware = CreateMiddleware();
        var messages = Messages("Hello");

        await middleware.GetResponseAsync(messages);
        await middleware.GetResponseAsync(messages);

        await _innerClient.Received(2).GetResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(),
            Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetResponse_ShortResponse_ShouldNotCache()
    {
        // Default MinimumResponseLengthToCache = 10, "Hi" is only 2 chars
        var shortResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Hi"));
        _innerClient.GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(shortResponse);

        var middleware = CreateMiddleware();
        var messages = Messages("Hello");

        await middleware.GetResponseAsync(messages);
        await middleware.GetResponseAsync(messages);

        await _innerClient.Received(2).GetResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(),
            Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>());
    }

    // --- Disabled ---

    [Fact]
    public async Task GetResponse_Disabled_ShouldAlwaysCallInner()
    {
        var middleware = CreateMiddleware(CachingOptions.Disabled);
        var messages = Messages("Hello");

        await middleware.GetResponseAsync(messages);
        await middleware.GetResponseAsync(messages);

        await _innerClient.Received(2).GetResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(),
            Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>());
    }

    // --- Cache key differentiation ---

    [Fact]
    public async Task GetResponse_DifferentModel_ShouldNotHitCache()
    {
        var middleware = CreateMiddleware(new CachingOptions { OnlyCacheDeterministic = false });
        var messages = Messages("Hello");

        await middleware.GetResponseAsync(messages, new ChatOptions { ModelId = "gpt-4" });
        await middleware.GetResponseAsync(messages, new ChatOptions { ModelId = "gpt-3.5" });

        await _innerClient.Received(2).GetResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(),
            Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetResponse_SameModelSameMessages_ShouldHitCache()
    {
        var middleware = CreateMiddleware(new CachingOptions { OnlyCacheDeterministic = false });
        var messages = Messages("Hello");
        var opts = new ChatOptions { ModelId = "gpt-4" };

        await middleware.GetResponseAsync(messages, opts);
        await middleware.GetResponseAsync(messages, opts);

        await _innerClient.Received(1).GetResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(),
            Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetResponse_DifferentMaxTokens_ShouldNotHitCache()
    {
        var middleware = CreateMiddleware(new CachingOptions { OnlyCacheDeterministic = false });
        var messages = Messages("Hello");

        await middleware.GetResponseAsync(messages, new ChatOptions { MaxOutputTokens = 100 });
        await middleware.GetResponseAsync(messages, new ChatOptions { MaxOutputTokens = 500 });

        await _innerClient.Received(2).GetResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(),
            Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>());
    }

    // --- CachingOptions presets ---

    [Fact]
    public void CachingOptions_Defaults_ShouldBeCorrect()
    {
        var options = new CachingOptions();

        Assert.True(options.Enabled);
        Assert.Equal(3600, options.ExpirationSeconds);
        Assert.Equal(0, options.SlidingExpirationSeconds);
        Assert.True(options.OnlyCacheDeterministic);
        Assert.Equal(10, options.MinimumResponseLengthToCache);
    }

    [Fact]
    public void CachingOptions_Development_ShouldHaveCorrectValues()
    {
        var options = CachingOptions.Development;

        Assert.True(options.Enabled);
        Assert.Equal(300, options.ExpirationSeconds);
        Assert.False(options.OnlyCacheDeterministic);
    }

    [Fact]
    public void CachingOptions_Production_ShouldHaveCorrectValues()
    {
        var options = CachingOptions.Production;

        Assert.True(options.Enabled);
        Assert.Equal(3600, options.ExpirationSeconds);
        Assert.Equal(1800, options.SlidingExpirationSeconds);
        Assert.True(options.OnlyCacheDeterministic);
    }

    [Fact]
    public void CachingOptions_Disabled_ShouldBeDisabled()
    {
        var options = CachingOptions.Disabled;

        Assert.False(options.Enabled);
    }

    // --- Helpers ---

    private CachingMiddleware CreateMiddleware(CachingOptions? options = null)
    {
        return new CachingMiddleware(_innerClient, _cache, options);
    }

    private static List<ChatMessage> Messages(string text)
    {
        return [new ChatMessage(ChatRole.User, text)];
    }
}
