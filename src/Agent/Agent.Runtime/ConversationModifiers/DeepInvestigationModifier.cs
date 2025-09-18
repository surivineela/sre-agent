// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Extensions;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Agent.Runtime.AgentTasks;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.ConversationModifiers;

public sealed class DeepInvestigationModifier : IConversationModifier
{
    // Singleton modifier instance
    public static DeepInvestigationModifier Instance { get; } = new();

    // Private ctor to enforce singleton
    private DeepInvestigationModifier() { }

    public string DisplayName => "Deep Investigation";
    public string Description => "Performs deeper analysis planning before handing off to the main reasoning loop.";

    public string? UserPromptOverride => """
    Based on the conversation context and the user's message, determine if a deeper investigation is required to solve the user query.
    If so, call 'StartIncidentInvestigationTask' to begin the investigation process. If not, indicate that a deeper investigation is not needed at this time.
    """;

    public Agent<AgentContext> GetModifierAgent()
    {
        return new("DeepInvestigationAgent")
        {
            Instructions = """
            <core_responsibilities>
            - Determine if a deep investigation is needed to address the user's query
            - Check if there is already a running investigation to avoid duplicate tasks
            - Start the investigation task if necessary
            </core_responsibilities>

            <execution_guidelines>
            Your task is to determine if a deeper investigation is needed based on the user's query and the current
            context of the conversation.

            A deep investigation is a task that performs a deep analysis of an issue, gathers information, explores multiple root cause possibilities, and
            provides a comprehensive report of findings and recommendations. If the user's query warrants this type of complex investigation, then you should initiate it.

            If the user's request is vague or unclear, do not start a deep investigation. If the user is asking for a simple information retrieval task, do not
            start a deep investigation.

            An investigation should be started if the user is asking about a problem that may have an open-ended nature or requires extensive exploration.

            You can check if there are any ongoing investigations by calling the 'ListAllActiveTasks' tool. Even if there is an ongoing investigation task,
            you may still start a new investigation if specifically requested by the user.

            If you determine that a deeper investigation is needed, you must call the 'StartIncidentInvestigationTask' tool and then end your turn. If you determine that a new investigation
            task is not needed, DO NOT call the 'StartIncidentInvestigationTask' tool, and instead indicate to the user why you are not starting a new investigation.
            </execution_guidelines>

            <workflow>
            1. Use the above guidelines to determine if the user request and conversation context warrant a deep investigation
            2. Before you start any new investigation task, use 'ListAllActiveTasks' to see if there is a task already running
            3. If there is a task already running, determine whether or not it is appropriate to start a new task anyways (i.e. if the user explicitly asks for one)
            4. If you determine a new deep investigation task should be started, use 'StartIncidentInvestigationTask' to initiate it
            5. Return an appropriate response to the user about whether a new investigation was started or not, be concise and do not include unnecessary details. Do not explain 'why' you decided to start a deep investigation or not.
            </workflow>

            <output>
            Be succinct in your response to the user, do not include any extraneous information about your thought process. Do no explain why you started the deep investigation or not, simply state what you did.
            </output>

            <verbosity>
            You are an intermediate agent, your response should be minimal and should not contain any extraneous information or explanations.
            </verbosity>
            """,
            FactoryTools = [
                nameof(AgentTaskPluginDefinition.StartIncidentInvestigationTask),
                nameof(AgentTaskPluginDefinition.ListAllActiveTasks)
            ],
            ReasoningEffortLevel = ChatOptionsExtensions.MinimalReasoningEffort
        };
    }

    public Task<ModificationResult> ProcessModificationAsync(RunResult<AgentContext> agentOutput, CancellationToken cancellationToken)
    {
        // Check if the StartIncidentInvestigationTask tool was invoked by examining the agent's messages
        bool taskStarted = WasStartIncidentInvestigationTaskInvoked(agentOutput);

        // If the agent did NOT invoke StartIncidentInvestigationTask, pass to main loop
        // If the agent DID invoke it, don't pass to main loop (investigation is handled separately)
        var result = new ModificationResult
        {
            PassToMainLoop = !taskStarted
        };

        return Task.FromResult(result);
    }

    private static bool WasStartIncidentInvestigationTaskInvoked(RunResult<AgentContext> agentOutput)
    {
        // Check both input and new messages for function calls to StartIncidentInvestigationTask
        var allMessages = agentOutput.Input.Concat(agentOutput.NewItems);

        foreach (var message in allMessages)
        {
            foreach (var content in message.Contents)
            {
                if (content is FunctionCallContent functionCall &&
                    functionCall.Name == nameof(AgentTaskPluginDefinition.StartIncidentInvestigationTask))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
