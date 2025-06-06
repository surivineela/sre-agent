using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace Agent.Core.Helpers;

/// <summary>
/// Assists with auto-replying to chat messages based on the state of an AI agent.
/// For example, used in tests and evals to prevent the agent from getting stuck because it asked the user a question.
/// </summary>
public class AutoReplyHelper
{
    /// <param name="chatClient">Must have function invocation enabled</param>
    /// <exception cref="ArgumentNullException"></exception>
    public AutoReplyHelper(IChatClient chatClient)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));

        var assessmentTools = new List<AITool> { AIFunctionFactory.Create(ProvideAssessment) };
        _chatOptions = new ChatOptions { ToolMode = ChatToolMode.RequireAny, Tools = assessmentTools };
    }

    private IChatClient _chatClient;
    private string? _mostRecentMessageJson = null;
    private ChatOptions _chatOptions;

    public string GroundedContext { get; set; } = "No ground truth provided";
    public string DefaultReply { get; set; } = "Please do your best to figure it out.";
    public AssessedAgentState AssessedState { get; private set; } = AssessedAgentState.Ambiguous;

    public async Task<string?> GetReply(List<ChatMessage> messages)
    {
        var lastMessage = messages?.LastOrDefault();

        if (lastMessage == null)
            return null;

        if (JsonSerializer.Serialize(lastMessage) == _mostRecentMessageJson)
            return null;

        _mostRecentMessageJson = JsonSerializer.Serialize(lastMessage);

        if(string.IsNullOrEmpty(lastMessage.Text))
        {
            // We do not assess tool calls yet.
            return null;
        }

        await RunStateAssessment(lastMessage.Text);

        return AssessedState switch
        {
            AssessedAgentState.Ambiguous => null,
            AssessedAgentState.Working => null,
            AssessedAgentState.Findings => null,
            AssessedAgentState.WaitingForUserInput => DefaultReply,
            _ => throw new Exception($"Unexpected assessed state: {AssessedState}")
        };
    }

    private async Task RunStateAssessment(string message)
    {
        await _chatClient.GetResponseAsync($"""
            You are assisting with evaluation of AI software, your task is to determine the current state of the agent based on the last message in the conversation.
            There are a few possibilities:
            1. the agent is still working and is sending updates as that work proceeds
            2. the agent has performed some analysis and has findings
            3. the agent has asked the user a question and is waiting for input
            4. the agent state is highly ambiguous
                
            The above is in assessment priority order.
            For example, the agent might report some initial findings (2) but indicate that it will perform additional analysis (1).
            In this case (1) takes priority, so the agent is still working.

            To help with your assessment, here are some notes about the ground truth of this scenario:
            ```
            {this.GroundedContext}
            ```

            Example of the agent waiting for user input:
            ```
            Could you please confirm if you know the Azure subscription or resource group where this Container App is deployed?
            If not, I will present you with a list of possible matches to ensure we are looking at the correct resource.
            ```

            Example of the agent having some initial findings but still working:
            ```
            I found persistent System.OutOfMemoryException errors in diagnosticbench-app-202504091010.
            The application is failing due to memory exhaustion in the main business logic (Program.cs:526).
            I will now perform a deep memory analysis to identify the root cause and begin remediation.
            ```
                
            Read the message below and perform your assessment:
            ```
            {message}
            ```
            """, _chatOptions);
    }

    private void ProvideAssessment(AssessedAgentState state, string reasoning)
    {
        Debug.WriteLine($"Assessed agent state is `{state}`: {reasoning}");
        AssessedState = state;
    }
    public enum AssessedAgentState
    {
        Working,
        Findings,
        WaitingForUserInput,
        Ambiguous
    }
}


