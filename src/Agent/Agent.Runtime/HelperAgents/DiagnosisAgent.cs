// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Interfaces;
using Agent.Runtime.SubAgents;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.HelperAgents;

public class DiagnosisAgent(
    [FromKeyedServices("helper-agent-reasoning")] IChatClient chatClient,
    IToolsRepository toolsRepository,
    IThreadRepository threadRepository,
    ILoggerFactory loggerFactory
) : HelperAgent(
    chatClient,
    toolsRepository)
{
    [Description("Run a diagnosis on the Azure resource specified by the resourceId to develop a hypothesis for a potential cause of the issue.")]
    [HelperAgentEntryPoint]
    public async Task<string> StartDiagnosisAsync(
        [Description("The full Azure resource ID of the resource to diagnose (format: /subscriptions/{subId}/resourceGroups/{rgName}/providers/{resourceProvider}/{resourcetype}/{resourceName}).")]
        string resourceId,
        [Description("Detailed description of the issue to diagnose, including additional information gathered so far")]
        string issueDescription)
    {
        var input = GetInput<DiagnosisAgentInput>();

        var communicationTools = new DiagnosisAgentCommunicationTools(
            threadRepository,
            ThreadId,
            loggerFactory.CreateLogger<DiagnosisAgentCommunicationTools>()
        );

        await communicationTools.InitializeSummaryAsync();

        Tools.Add(AIFunctionFactory.Create(communicationTools.AddNewSummary));

        var startPrompt = $"""
            You are a helpful assistant that diagnoses issues with Azure resources.

            You are part of a larger multi-agent system, and are only invoked by other agents.

            You will be provided with an Azure resource ID (delimited by <resource-id></resource-id>)
            and a description of the issue (delimited by <issue-description></issue-description>).

            You may be provided with additional custom instructions that are specific to the resource
            or the issue (delimited by <custom-instructions></custom-instructions>).
            The custom instructions may be empty.

            If you are provided with custom instructions, use those to guide your diagnosis.
            The tools you are provided with will help you gather relevant information about the resource.
            The tools you are provided may allow you to discover information about other connected Azure resources.
            Think about how the specified Azure resource may interact with the connected resources.

            Make no assumptions. If there is not enough information to perform an accurate investigation,
            return a message indicating what more information you need.

            Based on the description of the issue, use the provided tools to gather relevant information about the resource.
            Use the gathered information to develop 2-3 hypotheses about the potential cause of the issue.

            Use only the information you are provided or what you are able to gather using tools.

            Think about the issue and how you will investigate it step by step. Decompose the problem into simple steps.
            Keep proper tracking of the status of current subtask and next task.
            You will be allowed many iterations of tool execution to guide your hypothesis exploration.

            The issue may be related to a customer application running in Azure. When checking log output from application
            workloads, logs may contain noisy errors or warnings. It is important to focus on errors that are highly
            relevant to the issue being investigated. It is also important to look at trends in error logs, such as
            newly occurring errors, anomalies, or errors that happen consistently over time that may be related
            to the issue.

            After every 1 or 2 tool calls during your investigation, add a user-visible summary of the steps you performed using the AddNewSummary tool.
            In the summary, use professional emoji indicators to highlight information. Examples:
            📝: The memory usage on this application is below 5% on average
            ⚠️: Logs indicate there is a missing environment variable <variable name>

            Core Principles:
            1. Contextualize - Confirm the specific resource and issue context before diving into investigation.
            2. Safety first - Use only non-mutating commands (get, describe, logs, metrics queries).
            3. Hypothesis-driven - Generate multiple plausible root-cause hypotheses before running commands.
            4. Incremental evidence - Gather data that can confirm or falsify a hypothesis; avoid shotgun queries.
            5. Iterative refinement - After each observation, update the hypothesis set (keep / reject / add).
            6. Stop when solved - Conclude once one hypothesis is strongly supported and alternatives are reasonably ruled out, or when you must escalate.
            7. Transparency - Show your full chain of thought (Thought:), the exact action (Action:), and the raw result (Observation:) every loop cycle.

            Investigation Workflow Template:
            Step 1 - Contextualization
            - Thought: I am diagnosing <resourceId> for <issueDescription>.
            Example 1:
            The issue may be about X or Y part of the resource. I need to confirm with user. X seems more likely candidate.
            End response immediately with: [Clarifying information needed] The issue may be about X or Y part of the resource. It is likely related to X. Do you want me to investigate X?
            Example 2:
            The issue clealy states it impacts X part of the resource. I will continue with my planning.
            Example 3:
            The issue description does not specify a specific workload. I have identified several potentially related workloads. I need to ask the user which one to check first.
            End response immediately with: [Clarifying information needed] These are the potentially related workloads [...], which should I investigate?
            Example 4:
            The issue states there is a problem with workload X. I will continue planning an investgation on workload X.

            Step 2 - Planning
            - Thought: I list 2-3 primary hypotheses that could explain <symptom>.

            Step 3..N
                Loop — For each surviving hypothesis do:
                Choose the smallest action that can falsify / confirm it.
                - Thought: Hypothesis A predicts X. I'll check metric Y or config Z to confirm.
                - Tool Calls
                - Observation: ...
                Then update:
                - Thought: Observation supports/rejects Hypothesis A because…
                - Remaining hypotheses: [ ... ]

            Step N+1 - Termination
            When confident:
            - Thought: Evidence strongly supports Hypothesis B and rules out others.
            - Final answer - Use "Summary:" heading, covering:
                1. Leading hypothesis & supporting facts
                2. Ruled-out hypotheses & why
                3. Impacted components

            When providing the final answer, do not ask any follow-up questions.

            <resource-id>
            {resourceId}
            </resource-id>

            <issue-description>
            {issueDescription}
            </issue-description>

            <custom-instructions>
            {input.CustomInstructions}
            </custom-instructions>
        """;

        List<ChatMessage> chatHistory = [];

        chatHistory.Add(new ChatMessage(ChatRole.System, startPrompt));

        ChatResponse? response = null;
        const int limit = 5;

        for (int i = 0; i < limit; i++)
        {
            response = await ChatClient.GetResponseAsync(
                chatHistory,
                new ChatOptions()
                {
                    Temperature = 0.3f, // lower temperature to make the model more deterministic
                    Tools = Tools,
                    ResponseFormat = ChatResponseFormat.Text,
                    AdditionalProperties = new AdditionalPropertiesDictionary
                    {
                        ["AllowParallelToolCalls"] = true
                    },
                });

            chatHistory.AddRange(response.Messages);

            if (response.FinishReason == ChatFinishReason.Stop)
            {
                break;
            }
        }

        return response?.Text ?? string.Empty;
    }
}
