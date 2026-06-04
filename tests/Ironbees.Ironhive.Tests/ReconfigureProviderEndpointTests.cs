using Ironbees.Ironhive.Orchestration;
using IronHive.Abstractions;
using IronHive.Abstractions.Messages;
using IronHive.Abstractions.Registries;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Ironbees.Ironhive.Tests;

/// <summary>
/// Tests for <see cref="IronhiveAdapter.ReconfigureProviderEndpoint"/> — the administrative,
/// endpoint-reconfiguration path that replaced the per-request EndpointOverride.
/// </summary>
public class ReconfigureProviderEndpointTests
{
    private const string Provider = "gpustack";

    private readonly IHiveService _hiveServiceMock;
    private readonly IProviderRegistry _providersMock;

    public ReconfigureProviderEndpointTests()
    {
        _hiveServiceMock = Substitute.For<IHiveService>();
        _providersMock = Substitute.For<IProviderRegistry>();
        _hiveServiceMock.Providers.Returns(_providersMock);
    }

    private IronhiveAdapter CreateAdapter(IronhiveOptions options) =>
        new(
            _hiveServiceMock,
            Substitute.For<IIronhiveOrchestratorFactory>(),
            new OrchestrationEventMapper(),
            options,
            NullLogger<IronhiveAdapter>.Instance);

    private static IronhiveOptions OptionsWithUpdater(out List<string> capturedEndpoints)
    {
        var captured = new List<string>();
        capturedEndpoints = captured;
        var options = new IronhiveOptions();
        options.ProviderEndpointUpdaters[Provider] = endpoint =>
        {
            captured.Add(endpoint);
            return Substitute.For<IMessageGenerator>();
        };
        return options;
    }

    [Fact]
    public void ReconfigureProviderEndpoint_ValidEndpoint_RegistersGeneratorAndReturnsTrue()
    {
        var options = OptionsWithUpdater(out var captured);
        var adapter = CreateAdapter(options);

        var changed = adapter.ReconfigureProviderEndpoint(Provider, "https://gpu.example.com:8080/v1");

        Assert.True(changed);
        Assert.Equal(["https://gpu.example.com:8080/v1"], captured);
        _providersMock.Received(1).SetMessageGenerator(Provider, Arg.Any<IMessageGenerator>());
    }

    [Fact]
    public void ReconfigureProviderEndpoint_SameEndpointTwice_SkipsSecondReregistration()
    {
        var options = OptionsWithUpdater(out _);
        var adapter = CreateAdapter(options);

        var first = adapter.ReconfigureProviderEndpoint(Provider, "https://gpu.example.com/v1");
        var second = adapter.ReconfigureProviderEndpoint(Provider, "https://gpu.example.com/v1");

        Assert.True(first);
        Assert.False(second);
        _providersMock.Received(1).SetMessageGenerator(Provider, Arg.Any<IMessageGenerator>());
    }

    [Fact]
    public void ReconfigureProviderEndpoint_DifferentEndpoint_ReregistersAgain()
    {
        var options = OptionsWithUpdater(out var captured);
        var adapter = CreateAdapter(options);

        adapter.ReconfigureProviderEndpoint(Provider, "https://gpu-a.example.com/v1");
        var changed = adapter.ReconfigureProviderEndpoint(Provider, "https://gpu-b.example.com/v1");

        Assert.True(changed);
        Assert.Equal(["https://gpu-a.example.com/v1", "https://gpu-b.example.com/v1"], captured);
        _providersMock.Received(2).SetMessageGenerator(Provider, Arg.Any<IMessageGenerator>());
    }

    [Fact]
    public void ReconfigureProviderEndpoint_UnregisteredProvider_Throws()
    {
        var adapter = CreateAdapter(new IronhiveOptions());

        Assert.Throws<InvalidOperationException>(() =>
            adapter.ReconfigureProviderEndpoint("unknown", "https://gpu.example.com/v1"));

        _providersMock.DidNotReceive().SetMessageGenerator(Arg.Any<string>(), Arg.Any<IMessageGenerator>());
    }

    [Theory]
    [InlineData("ftp://gpu.example.com")]        // non-http(s) scheme
    [InlineData("file:///etc/passwd")]            // non-http(s) scheme
    [InlineData("/relative/path")]                // not absolute
    [InlineData("gpu.example.com:8080")]          // missing scheme
    [InlineData("not a url")]                      // garbage
    public void ReconfigureProviderEndpoint_InvalidUrl_ThrowsArgumentException(string endpoint)
    {
        var options = OptionsWithUpdater(out _);
        var adapter = CreateAdapter(options);

        Assert.Throws<ArgumentException>(() => adapter.ReconfigureProviderEndpoint(Provider, endpoint));
        _providersMock.DidNotReceive().SetMessageGenerator(Arg.Any<string>(), Arg.Any<IMessageGenerator>());
    }

    [Theory]
    [InlineData("http://localhost:8080")]         // loopback hostname
    [InlineData("http://127.0.0.1/v1")]           // loopback IPv4
    [InlineData("https://[::1]/v1")]              // loopback IPv6
    [InlineData("http://10.0.0.5/v1")]            // private 10/8
    [InlineData("http://172.16.4.2/v1")]          // private 172.16/12
    [InlineData("http://192.168.1.10/v1")]        // private 192.168/16
    [InlineData("http://169.254.1.1/v1")]         // link-local 169.254/16
    [InlineData("http://[fe80::1]/v1")]           // IPv6 link-local
    [InlineData("http://[fc00::1]/v1")]           // IPv6 unique-local
    public void ReconfigureProviderEndpoint_LoopbackOrPrivateHost_ThrowsArgumentException(string endpoint)
    {
        var options = OptionsWithUpdater(out _);
        var adapter = CreateAdapter(options);

        Assert.Throws<ArgumentException>(() => adapter.ReconfigureProviderEndpoint(Provider, endpoint));
        _providersMock.DidNotReceive().SetMessageGenerator(Arg.Any<string>(), Arg.Any<IMessageGenerator>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ReconfigureProviderEndpoint_NullOrBlankArguments_Throws(string? value)
    {
        var options = OptionsWithUpdater(out _);
        var adapter = CreateAdapter(options);

        // null throws ArgumentNullException, blank throws ArgumentException — both derive from ArgumentException.
        Assert.ThrowsAny<ArgumentException>(() => adapter.ReconfigureProviderEndpoint(Provider, value!));
        Assert.ThrowsAny<ArgumentException>(() => adapter.ReconfigureProviderEndpoint(value!, "https://gpu.example.com/v1"));
    }

    [Fact]
    public void ReconfigureProviderEndpoint_PublicHostname_IsAllowed()
    {
        var options = OptionsWithUpdater(out var captured);
        var adapter = CreateAdapter(options);

        var changed = adapter.ReconfigureProviderEndpoint(Provider, "https://gpu.public-cloud.example.com/v1");

        Assert.True(changed);
        Assert.Single(captured);
    }
}
