using System.Reflection;
using Ironbees.Core;
using Ironbees.Ironhive.Orchestration;
using IronHive.Abstractions;
using IronHive.Abstractions.Messages;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using IronHiveAgent = IronHive.Abstractions.Agent.IAgent;
using IronHiveAgentConfig = IronHive.Abstractions.Agent.AgentConfig;
using IronHiveAgentParametersConfig = IronHive.Abstractions.Agent.AgentParametersConfig;

namespace Ironbees.Ironhive.Tests;

/// <summary>
/// 샘플링 파라미터가 <see cref="ModelConfig"/> 에서 IronHive 요청 경로까지 실제로 도달하는지 고정한다.
///
/// 이 스위트가 존재하는 이유는 두 건의 **무음 무효(silent no-op)** 다:
/// (1) IronHive 가 요청 모델을 단순화하면서 MaxTokens 외 네 개를 떨어뜨렸는데
///     <c>AgentParametersConfig</c> 는 계속 다섯 개를 광고해, temperature 를 지정한 에이전트가
///     프로바이더 기본값으로 샘플링됐다. 4주간 아무도 몰랐다(0.15.0 에서 복원).
/// (2) 어댑터가 <see cref="ModelConfig"/> 기본값(4000 / 0.7)과 비교해 "설정 여부"를 추론해서,
///     사용자가 그 값을 **명시적으로** 지정하면 미설정으로 간주해 버렸다.
///
/// 둘 다 빌드·기존 테스트를 통과한 채로 조용히 틀렸다. 그래서 컴파일이 아니라 **값의 도달**을 본다.
/// </summary>
public class SamplingParameterWiringTests
{
    private readonly IHiveService _hiveService = Substitute.For<IHiveService>();
    private readonly IronhiveAdapter _adapter;

    public SamplingParameterWiringTests()
    {
        _adapter = new IronhiveAdapter(
            _hiveService,
            Substitute.For<IIronhiveOrchestratorFactory>(),
            new OrchestrationEventMapper(),
            NullLogger<IronhiveAdapter>.Instance);
    }

    /// <summary>어댑터가 <c>CreateAgentFrom</c> 에 넘긴 구성 델리게이트를 실제 AgentConfig 에 적용해 돌려준다.</summary>
    private async Task<IronHiveAgentConfig> CaptureConfiguredAgentAsync(ModelConfig model)
    {
        IronHiveAgentConfig? captured = null;
        _hiveService
            .CreateAgentFrom(Arg.Do<Action<IronHiveAgentConfig>>(configure =>
            {
                captured = new IronHiveAgentConfig();
                configure(captured);
            }))
            .Returns(Substitute.For<IronHiveAgent>());

        await _adapter.CreateAgentAsync(new AgentConfig
        {
            Name = "wiring-probe",
            Description = "probe",
            Version = "1.0.0",
            SystemPrompt = "probe",
            Model = model,
        });

        Assert.NotNull(captured);
        return captured!;
    }

    [Fact]
    public async Task NonDefaultSamplingValues_ReachAgentParameters()
    {
        var cfg = await CaptureConfiguredAgentAsync(new ModelConfig
        {
            Provider = "openai",
            Deployment = "gpt-4o",
            Temperature = 0.2,
            MaxTokens = 512,
            TopP = 0.9,
        });

        Assert.NotNull(cfg.Parameters);
        Assert.Equal(0.2f, cfg.Parameters!.Temperature);
        Assert.Equal(512, cfg.Parameters.MaxTokens);
        Assert.Equal(0.9f, cfg.Parameters.TopP);
    }

    /// <summary>
    /// 회귀 잠금 — 사용자가 <see cref="ModelConfig"/> 의 기본값과 **같은 값을 명시적으로** 지정해도
    /// 전달돼야 한다. 예전 어댑터는 기본값과의 차이로 설정 여부를 추론해 이 경우를 버렸다.
    /// </summary>
    [Fact]
    public async Task ExplicitlySetDefaultValues_AreStillForwarded()
    {
        var cfg = await CaptureConfiguredAgentAsync(new ModelConfig
        {
            Provider = "openai",
            Deployment = "gpt-4o",
            Temperature = 0.7,   // ModelConfig 의 기본값과 동일 — 그래도 명시적 지정이다
            MaxTokens = 4000,    // 동상
        });

        Assert.NotNull(cfg.Parameters);
        Assert.Equal(0.7f, cfg.Parameters!.Temperature);
        Assert.Equal(4000, cfg.Parameters.MaxTokens);
    }

    /// <summary>
    /// <c>TopP</c> 는 <c>double?</c> 라 "미설정" 을 정직하게 표현할 수 있다 — 미지정 시 null 로 남아야 한다
    /// (센티널 추론과 달리 여기엔 추측이 없다).
    /// </summary>
    [Fact]
    public async Task UnsetNullableParameter_StaysNull()
    {
        var cfg = await CaptureConfiguredAgentAsync(new ModelConfig
        {
            Provider = "openai",
            Deployment = "gpt-4o",
        });

        Assert.NotNull(cfg.Parameters);
        Assert.Null(cfg.Parameters!.TopP);
    }

    /// <summary>
    /// Cross-boundary teeth — 우리가 <see cref="IronHiveAgentParametersConfig"/> 에 쓰는 모든 노브가
    /// **우리가 핀한 IronHive 버전의** <see cref="MessageRequest"/> 에 받을 곳을 갖는지 본다.
    ///
    /// IronHive 리포에도 같은 취지의 테스트가 있지만 그것은 IronHive 의 HEAD 만 지킨다. 실제로 값을
    /// 잃은 것은 **ironbees 가 낡은 버전을 핀한 구간**이었다 — 그 구간은 이쪽에서만 잡을 수 있다.
    /// 여기가 RED 면 핀한 IronHive 가 계약을 좁힌 것이므로 어댑터를 고치기 전에 핀부터 본다.
    /// </summary>
    [Fact]
    public void EveryParameterWeWrite_HasASinkOnThePinnedMessageRequest()
    {
        var sinks = typeof(MessageRequest)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);

        var missing = typeof(IronHiveAgentParametersConfig)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .Where(name => !sinks.Contains(name))
            .ToList();

        Assert.True(
            missing.Count == 0,
            $"AgentParametersConfig 가 광고하는 노브 중 MessageRequest 에 받을 곳이 없는 것: " +
            $"{string.Join(", ", missing)}. 이 상태에서는 소비자가 지정한 값이 조용히 무시된다.");
    }
}
