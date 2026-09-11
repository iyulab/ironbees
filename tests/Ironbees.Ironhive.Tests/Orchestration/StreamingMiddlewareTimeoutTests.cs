// Copyright (c) IYULab. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Ironbees.Core;
using Ironbees.Core.Orchestration;
using Ironbees.Ironhive.Orchestration;
using IronHive.Abstractions.Agent;
using IronHive.Abstractions.Messages;
using IronHive.Abstractions.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using IronbeesAgentConfig = Ironbees.Core.AgentConfig;
using IronHiveAgent = IronHive.Abstractions.Agent.IAgent;

namespace Ironbees.Ironhive.Tests.Orchestration;

/// <summary>
/// The consumer path behind IronHive's HD-91: a timeout configured in Ironbees' declarative middleware
/// settings reaches a streaming orchestration run. Until IronHive 0.26.0 the built-in middleware had no
/// streaming half, so `IronhiveOrchestratorWrapper.RunStreamingAsync` - which is what Ironbees runs -
/// silently dropped it, and an agent that hung was never cut by the timeout the operator had set.
///
/// <para>
/// Attribution matters here: <see cref="OrchestratorSettings"/> carries its own Timeout (5 min) and
/// AgentTimeout (2 min) defaults, which the factory always passes to the orchestrator. Both are set far
/// above the middleware's duration, and the control run - identical but with no middleware - shows the
/// stalled agent is still running when the middleware run has long finished. So the deadline observed in
/// the first case can only come from the middleware.
/// </para>
/// </summary>
public class StreamingMiddlewareTimeoutTests
{
    private static readonly TimeSpan MiddlewareTimeout = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan ObservationWindow = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ControlWindow = TimeSpan.FromSeconds(3);

    [Fact]
    public async Task StreamingOrchestration_CutsAStalledAgent_AtTheConfiguredTimeout()
    {
        var orchestrator = CreateOrchestrator(new MiddlewareSettings
        {
            Timeout = new TimeoutSettings { Duration = MiddlewareTimeout }
        });

        using var cts = new CancellationTokenSource(ObservationWindow);
        var stopwatch = Stopwatch.StartNew();
        var events = 0;

        try
        {
            await foreach (var _ in orchestrator.RunStreamingAsync("go", cts.Token))
            {
                events++;
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            Assert.Fail(
                $"the stream was still running after {ObservationWindow.TotalSeconds:0} s, so the configured " +
                $"{MiddlewareTimeout.TotalMilliseconds:0} ms timeout never reached the streaming path");
        }

        stopwatch.Stop();
        Assert.True(
            stopwatch.Elapsed < ControlWindow,
            $"a {MiddlewareTimeout.TotalMilliseconds:0} ms timeout must end the stream promptly, but it took {stopwatch.Elapsed}");
        Assert.True(events > 0, "the orchestration must report what happened rather than ending silently");
    }

    [Fact]
    public async Task WithoutTheMiddleware_TheStalledAgentIsStillRunning()
    {
        // The control. Without it, "the stream ended" could be the orchestrator's own timeout, a failing
        // fixture, or an agent that never really stalled.
        var orchestrator = CreateOrchestrator(middleware: null);

        using var cts = new CancellationTokenSource(ControlWindow);
        var completed = false;

        try
        {
            await foreach (var _ in orchestrator.RunStreamingAsync("go", cts.Token))
            {
            }

            completed = true;
        }
        catch (OperationCanceledException)
        {
            // Expected: nothing cut the stalled agent, so the observation window expired.
        }

        Assert.False(
            completed,
            $"with no timeout middleware the stalled agent must still be running after {ControlWindow.TotalSeconds:0} s; " +
            "if it ended, this fixture is not measuring a stall");
    }

    private static IMultiAgentOrchestrator CreateOrchestrator(MiddlewareSettings? middleware)
    {
        var factory = new IronhiveOrchestratorFactory(
            NullLogger<IronhiveOrchestratorFactory>.Instance,
            new IronhiveMiddlewareFactory());

        var settings = new OrchestratorSettings
        {
            Type = OrchestratorType.Sequential,
            // Far above the middleware's duration, so a cut cannot be attributed to these.
            Timeout = TimeSpan.FromMinutes(5),
            AgentTimeout = TimeSpan.FromMinutes(5),
            Middleware = middleware
        };

        var config = new IronbeesAgentConfig
        {
            Name = "staller",
            Description = "An agent that never answers",
            Version = "1.0.0",
            SystemPrompt = "Test system prompt",
            Model = new ModelConfig { Provider = "test", Deployment = "test-model" }
        };

        return factory.CreateOrchestrator(settings, [new IronhiveAgentWrapper(new StallingAgent(), config)]);
    }

    /// <summary>An IronHive agent that never produces anything until it is cancelled.</summary>
    private sealed class StallingAgent : IronHiveAgent
    {
        public string Provider { get; set; } = "test";

        public string Model { get; set; } = "test-model";

        public string Name { get; set; } = "staller";

        public string Description { get; set; } = "An agent that never answers";

        public string? Instructions { get; set; }

        public IToolCollection? Tools { get; set; }

        public int? MaxTokens { get; set; }

        public async Task<MessageResponse> InvokeAsync(
            IEnumerable<Message> messages,
            AgentInvokeOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new UnreachableException();
        }

        public async IAsyncEnumerable<StreamingMessageResponse> InvokeStreamingAsync(
            IEnumerable<Message> messages,
            AgentInvokeOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            yield break;
        }
    }
}
