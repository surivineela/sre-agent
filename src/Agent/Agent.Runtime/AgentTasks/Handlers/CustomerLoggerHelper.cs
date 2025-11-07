// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Agent.Logging;
using Agent.Runtime.Helpers;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.AgentTasks.Handlers;

public sealed class CustomerLoggerHelper
{
    private readonly CustomerLogger _customerLogger;
    private readonly string _threadId;
    private readonly string _taskType;

    public CustomerLoggerHelper(CustomerLogger customerLogger, string threadId, string taskType)
    {
        _customerLogger = customerLogger;
        _threadId = threadId;
        _taskType = taskType;
    }

    public RunHooks<AgentContext> GetCustomerLoggerHooks()
    {
        // Defensive check - ensure this is only used for first-party agents
        if (!FirstPartyHelper.IsFirstPartyTenant())
        {
            return new RunHooks<AgentContext>(); // Return empty hooks - no telemetry will be sent
        }

        var hooks = new RunHooks<AgentContext>();

        // TOOL HOOKS - capture tool names, inputs, outputs
        hooks.ToolStart += (context, agent, functionCall, tool, input) =>
        {
            var agentContext = context.Context ?? throw new InvalidOperationException("Invalid agent context");

            var properties = new Dictionary<string, string>
            {
                ["EventType"] = "ToolStart",
                ["ToolName"] = tool.Name,
                ["SubAgentName"] = agent.Name,
                ["ThreadId"] = agentContext.ThreadId.ToString(),
                ["TaskType"] = _taskType,
                ["ToolDescription"] = tool.Description,
                ["ToolInput"] = FormatToolArguments(input),
                ["CallId"] = functionCall.CallId
            };

            _customerLogger.LogCustomEvent("AgentToolExecution", properties);
            return Task.CompletedTask;
        };

        hooks.ToolEnd += (context, agent, functionCallContent, tool, output) =>
        {
            var agentContext = context.Context ?? throw new InvalidOperationException("Invalid agent context");

            var properties = new Dictionary<string, string>
            {
                ["EventType"] = "ToolEnd",
                ["ToolName"] = tool.Name,
                ["SubAgentName"] = agent.Name,
                ["ThreadId"] = agentContext.ThreadId.ToString(),
                ["TaskType"] = _taskType,
                ["ToolOutput"] = TruncateString(output?.ToString() ?? "", 1000),
                ["CallId"] = functionCallContent.CallId
            };

            _customerLogger.LogCustomEvent("AgentToolExecution", properties);
            return Task.CompletedTask;
        };

        // AGENT HOOKS - capture agent names and handoffs
        hooks.AgentStart += (context, agent) =>
        {
            var agentContext = context.Context ?? throw new InvalidOperationException("Invalid agent context");

            var properties = new Dictionary<string, string>
            {
                ["EventType"] = "AgentStart",
                ["AgentName"] = agent.Name,
                ["ThreadId"] = agentContext.ThreadId.ToString(),
                ["TaskType"] = _taskType
            };

            _customerLogger.LogCustomEvent("AgentExecution", properties);
            return Task.CompletedTask;
        };

        hooks.AgentEnd += (context, agent, result) =>
        {
            var agentContext = context.Context ?? throw new InvalidOperationException("Invalid agent context");

            var properties = new Dictionary<string, string>
            {
                ["EventType"] = "AgentEnd",
                ["AgentName"] = agent.Name,
                ["ThreadId"] = agentContext.ThreadId.ToString(),
                ["TaskType"] = _taskType,
                ["Result"] = TruncateString(result?.ToString() ?? "", 500)
            };

            _customerLogger.LogCustomEvent("AgentExecution", properties);
            return Task.CompletedTask;
        };

        hooks.Handoff += (context, fromAgent, toAgent, handoffReasoning) =>
        {
            var agentContext = context.Context ?? throw new InvalidOperationException("Invalid agent context");

            var properties = new Dictionary<string, string>
            {
                ["EventType"] = "AgentHandoff",
                ["FromAgent"] = fromAgent.Name,
                ["ToAgent"] = toAgent.Name,
                ["ThreadId"] = agentContext.ThreadId.ToString(),
                ["TaskType"] = _taskType,
                ["HandoffReasoning"] = handoffReasoning
            };

            _customerLogger.LogCustomEvent("AgentHandoff", properties);
            return Task.CompletedTask;
        };

        // MODEL HOOKS - capture LLM inputs and outputs
        hooks.ModelGenerationStart += (context, agent, chatMessages, chatOptions) =>
        {
            var agentContext = context.Context ?? throw new InvalidOperationException("Invalid agent context");

            var properties = new Dictionary<string, string>
            {
                ["EventType"] = "ModelGenerationStart",
                ["AgentName"] = agent.Name,
                ["ThreadId"] = agentContext.ThreadId.ToString(),
                ["TaskType"] = _taskType,
                ["ModelInput"] = FormatChatMessages(chatMessages),
                ["Temperature"] = agent.Temperature.ToString()
            };

            _customerLogger.LogCustomEvent("ModelGeneration", properties);
            return Task.CompletedTask;
        };

        hooks.ModelGenerationEnd += (context, agent, response) =>
        {
            var agentContext = context.Context ?? throw new InvalidOperationException("Invalid agent context");

            var properties = new Dictionary<string, string>
            {
                ["EventType"] = "ModelGenerationEnd",
                ["AgentName"] = agent.Name,
                ["ThreadId"] = agentContext.ThreadId.ToString(),
                ["TaskType"] = _taskType,
                ["ModelOutput"] = FormatChatMessages(response?.Messages ?? []),
                ["InputTokens"] = response?.Usage?.InputTokenCount?.ToString() ?? "0",
                ["OutputTokens"] = response?.Usage?.OutputTokenCount?.ToString() ?? "0",
                ["ModelId"] = response?.ModelId ?? ""
            };

            _customerLogger.LogCustomEvent("ModelGeneration", properties);
            return Task.CompletedTask;
        };

        return hooks;
    }

    private static readonly JsonSerializerOptions _toolArgumentsJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static string FormatToolArguments(IEnumerable<KeyValuePair<string, object?>>? arguments)
    {
        if (arguments == null)
        {
            return string.Empty;
        }

        try
        {
            var argsDict = arguments.ToDictionary(kv => kv.Key, kv => kv.Value);
            return JsonSerializer.Serialize(argsDict, _toolArgumentsJsonOptions);
        }
        catch (Exception)
        {
            return string.Join(", ", arguments.Select(kv => $"{kv.Key}: {kv.Value?.ToString() ?? "null"}"));
        }
    }

    private static string FormatChatMessages(IEnumerable<ChatMessage> messages)
    {
        if (messages == null || !messages.Any())
        {
            return string.Empty;
        }

        try
        {
            var messagesSummary = messages.Select(m => new
            {
                Role = m.Role.ToString(),
                ContentLength = m.Text?.Length ?? 0,
                ContentPreview = m.Text?.Substring(0, Math.Min(200, m.Text?.Length ?? 0)) ?? ""
            }).ToList();

            return JsonSerializer.Serialize(messagesSummary, _toolArgumentsJsonOptions);
        }
        catch (Exception ex)
        {
            return $"Error formatting messages: {ex.Message}";
        }
    }

    private static string TruncateString(string input, int maxLength)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        return input.Length <= maxLength ? input : input.Substring(0, maxLength) + "...";
    }
}
