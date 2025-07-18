using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
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
    public AssessedAgentState AssessedState { get; set; } = AssessedAgentState.Ambiguous;

    public async Task<string?> AssessAndGetReply(List<ChatMessage> messages)
    {
        var lastMessage = messages.LastOrDefault(x => x.Role == ChatRole.Assistant);

        // Need to wait until we have a message from the assistant.
        if (lastMessage == null)
            return null;

        // If the last message is the same as the previous one, we do not need to assess again.
        if (JsonSerializer.Serialize(lastMessage) == _mostRecentMessageJson)
            return null;

        _mostRecentMessageJson = JsonSerializer.Serialize(lastMessage);

        await RunStateAssessment(messages);

        return AssessedState switch
        {
            AssessedAgentState.Ambiguous => null,
            AssessedAgentState.Working => null,
            AssessedAgentState.Complete => null,
            AssessedAgentState.WaitingForUserInput => DefaultReply,
            _ => throw new Exception($"Unexpected assessed state: {AssessedState}")
        };
    }

    private async Task RunStateAssessment(List<ChatMessage> messages)
    {
        var systemPrompt = """
            You are assisting with evaluation of AI software.
            You will be provided with a conversation history where the the user has asked the agent a question or given it a task, and your task is to determine the current state of the agent based on the last message in the conversation.

            There are a few possibilities:
            
            1. [WaitingForUserInput] The agent is still working on their task but has asked the user a question and is waiting for input
            2. [Working] The agent is still working on their task. This might include sending intermediate updates. No user input is required.
            3. [Complete] The agent has completed the task and it is unlikely to send further updates unless the user asks a follow-up question.
            4. [Ambiguous] The agent state is highly ambiguous

            In general, even when the agent completes a task, it will offer to help further by asking a follow-up question. In this case you can ignore the followup, because the original task is complete.

            Example of the task being complete:
            ```
            [user] list my linux webapps
            [assistant] ▶️ Starting discovery of your Linux web apps. First, I'll look up your available subscriptions and web apps, then filter for Linux-based ones.
            [assistant] ✅ Here are your Linux web apps:

            | Idx | Name                   | Subscription                             | Resource Group           | Location  | Resource ID |
            |-----|------------------------|------------------------------------------|--------------------------|-----------|-------------|
            | 1   | **pbatum-sre-web-eas3** | 29e3378b-0aaf-45da-b3c6-6fd0eea164e4     | pbatum-sre-web-eas-lin   | eastasia  | /subscriptions/29e3378b-0aaf-45da-b3c6-6fd0eea164e4/resourceGroups/pbatum-sre-web-eas-lin/providers/Microsoft.Web/sites/pbatum-sre-web-eas3 |
            | 2   | **pbatum-sre-web-eas4** | 29e3378b-0aaf-45da-b3c6-6fd0eea164e4     | pbatum-sre-web-eas-lin   | eastasia  | /subscriptions/29e3378b-0aaf-45da-b3c6-6fd0eea164e4/resourceGroups/pbatum-sre-web-eas-lin/providers/Microsoft.Web/sites/pbatum-sre-web-eas4 |

            Is there anything I can help you with for these apps?
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

            You will now be provided with the full conversation history, including the last message from the agent.
        """;

        var conversationBuilder = new StringBuilder();
        foreach (var m in messages)
        {
            if (!string.IsNullOrEmpty(m.Text))
            {
                conversationBuilder.AppendLine($"[{m.Role}] {m.Text}");
            }
        }

        var conversationMessageText = $"""
            Please perform your assessment based on the following conversation history:
            ```
            {conversationBuilder.ToString()}
            ```
            """;

        var assessmentMessages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.System, systemPrompt),
            new ChatMessage(ChatRole.User, conversationMessageText),
        };

        // The agent will update the assessed state with the provided tool.
        await _chatClient.GetResponseAsync(assessmentMessages, _chatOptions);
    }

    private void ProvideAssessment(AssessedAgentState state, string reasoning)
    {
        Debug.WriteLine($"Assessed agent state is `{state}`: {reasoning}");
        AssessedState = state;
    }

    public enum AssessedAgentState
    {
        Working,
        Complete,
        WaitingForUserInput,
        Ambiguous
    }
}


