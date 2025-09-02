// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Agent.Framework;

namespace Agent.Runtime.ConversationModifiers;

public interface IConversationModifier
{

    /// <summary>
    /// Human-friendly name of the modifier.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Brief description of what this modifier does.
    /// </summary>
    string Description { get; }

    string? UserPromptOverride { get; }

    /// <summary>
    /// Gets the modifier agent instance that will preprocess a message before the main reasoning loop.
    /// </summary>
    Agent<AgentContext> GetModifierAgent();

    /// <summary>
    /// Processes the modifier agent output and returns the modification result.
    /// </summary>
    /// <param name="agentOutput">The output from running the modifier agent</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The modification result indicating whether to pass to main loop</returns>
    Task<ModificationResult> ProcessModificationAsync(RunResult<AgentContext> agentOutput, CancellationToken cancellationToken);
}

public static class Modifiers
{
    /// <summary>
    /// Deep Investigation modifier singleton.
    /// </summary>
    public static IConversationModifier DeepInvestigation { get; } = DeepInvestigationModifier.Instance;

    /// <summary>
    /// Registry of all known modifiers by enum value.
    /// </summary>
    private static IReadOnlyDictionary<ConversationModifierEnum, IConversationModifier> AllByKey { get; } =
        new Dictionary<ConversationModifierEnum, IConversationModifier>
        {
            [ConversationModifierEnum.DeepInvestigation] = DeepInvestigation
        };

    /// <summary>
    /// Try get a modifier by its enum value. Returns false if not found.
    /// </summary>
    public static bool TryGet(ConversationModifierEnum key, out IConversationModifier? modifier)
    {
        return AllByKey.TryGetValue(key, out modifier);
    }
}
