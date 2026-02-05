// Copyright (c) IYULab. All rights reserved.
// Licensed under the MIT License.

using Ironbees.Core;
using Ironbees.Core.Orchestration;

namespace Ironbees.Ironhive.Orchestration;

/// <summary>
/// Factory for creating orchestrators from Ironbees orchestration settings.
/// </summary>
public interface IIronhiveOrchestratorFactory
{
    /// <summary>
    /// Creates an orchestrator based on the provided settings.
    /// </summary>
    /// <param name="settings">Orchestration settings defining the pattern and configuration.</param>
    /// <param name="agents">The agents to participate in orchestration.</param>
    /// <param name="handoffMap">Optional handoff target map for handoff orchestration.</param>
    /// <returns>A configured orchestrator instance.</returns>
    IMultiAgentOrchestrator CreateOrchestrator(
        OrchestratorSettings settings,
        IReadOnlyList<IAgent> agents,
        IReadOnlyDictionary<string, IReadOnlyList<HandoffTargetDefinition>>? handoffMap = null);
}
