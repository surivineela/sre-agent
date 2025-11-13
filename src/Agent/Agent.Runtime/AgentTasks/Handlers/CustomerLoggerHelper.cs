// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Diagnostics;
using System.Text.Json;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Agent.Logging;
using Agent.Runtime.Helpers;
using Microsoft.Extensions.AI;
using OpenTelemetry.Trace;

namespace Agent.Runtime.AgentTasks.Handlers;

public sealed class CustomerLoggerHelper : IDisposable
{
    private readonly CustomerLogger _customerLogger;
    private readonly string _threadId;
    private readonly string _taskType;
    private readonly Tracer _tracer;
    
    // Span hierarchy for telemetry correlation
    private TelemetrySpan? _currentAgentSpan;
    private TelemetrySpan? _currentToolSpan;
    private TelemetrySpan? _currentModelSpan;
    private bool _disposed = false;

    public CustomerLoggerHelper(CustomerLogger customerLogger, string threadId, string taskType, Tracer tracer)
    {
        _customerLogger = customerLogger;
        _threadId = threadId;
        _taskType = taskType;
        _tracer = tracer;
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

            // Create tool span as child of agent span
            _currentToolSpan = _tracer.StartActiveSpan($"customerlogger.tool.{tool.Name}", SpanKind.Internal, _currentAgentSpan);
            _currentToolSpan.SetAttribute("thread.id", agentContext.ThreadId.ToString());
            _currentToolSpan.SetAttribute("tool.name", tool.Name);
            _currentToolSpan.SetAttribute("agent.name", agent.Name);
            _currentToolSpan.SetAttribute("event.type", "ToolStart");

            var properties = new Dictionary<string, string>
            {
                ["EventType"] = "ToolStart",
                ["ToolName"] = tool.Name,
                ["SubAgentName"] = agent.Name,
                ["ThreadId"] = agentContext.ThreadId.ToString(),
                ["TaskType"] = _taskType,
                ["ToolDescription"] = tool.Description,
                ["ToolInput"] = FormatToolArguments(input),
                ["SpanId"] = GetCurrentSpanId(),
                ["ParentSpanId"] = GetCurrentParentSpanId(),
                ["TraceId"] = GetCurrentTraceId(),
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

            // Add span correlation and end tool span
            if (_currentToolSpan != null)
            {
                // Get span IDs BEFORE ending the span
                properties["SpanId"] = GetCurrentSpanId();
                properties["ParentSpanId"] = GetCurrentParentSpanId();
                properties["TraceId"] = GetCurrentTraceId();
                _currentToolSpan.SetAttribute("tool.output", TruncateString(output?.ToString() ?? "", 200));
                _currentToolSpan.End();
                _currentToolSpan = null;
            }

            _customerLogger.LogCustomEvent("AgentToolExecution", properties);
            return Task.CompletedTask;
        };

        // AGENT HOOKS - capture agent names and handoffs
        hooks.AgentStart += (context, agent) =>
        {
            var agentContext = context.Context ?? throw new InvalidOperationException("Invalid agent context");

            // Create telemetry span for agent execution
            if (_currentAgentSpan != null)
            {
                _currentAgentSpan.End();
            }
            _currentAgentSpan = _tracer.StartActiveSpan($"customerlogger.agent.{agent.Name}", SpanKind.Internal);
            _currentAgentSpan.SetAttribute("thread.id", agentContext.ThreadId.ToString());
            _currentAgentSpan.SetAttribute("agent.name", agent.Name);
            _currentAgentSpan.SetAttribute("task.type", _taskType);
            _currentAgentSpan.SetAttribute("event.type", "AgentStart");

            var properties = new Dictionary<string, string>
            {
                ["EventType"] = "AgentStart",
                ["AgentName"] = agent.Name,
                ["ThreadId"] = agentContext.ThreadId.ToString(),
                ["TaskType"] = _taskType,
                ["SpanId"] = GetCurrentSpanId(),
                ["ParentSpanId"] = GetCurrentParentSpanId(),
                ["TraceId"] = GetCurrentTraceId()
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

            // Add span correlation if available
            if (_currentAgentSpan != null)
            {
                properties["SpanId"] = GetCurrentSpanId();
                properties["ParentSpanId"] = GetCurrentParentSpanId();
                properties["TraceId"] = GetCurrentTraceId();
                _currentAgentSpan.SetAttribute("agent.result", TruncateString(result?.ToString() ?? "", 200));
                _currentAgentSpan.End();
                _currentAgentSpan = null;
            }

            _customerLogger.LogCustomEvent("AgentExecution", properties);
            return Task.CompletedTask;
        };

        hooks.Handoff += (context, fromAgent, toAgent, handoffReasoning) =>
        {
            var agentContext = context.Context ?? throw new InvalidOperationException("Invalid agent context");

            // Create handoff span as child of current agent span
            var handoffSpan = _tracer.StartSpan($"customerlogger.handoff.{fromAgent.Name}_to_{toAgent.Name}", SpanKind.Internal, _currentAgentSpan);
            handoffSpan.SetAttribute("thread.id", agentContext.ThreadId.ToString());
            handoffSpan.SetAttribute("handoff.from", fromAgent.Name);
            handoffSpan.SetAttribute("handoff.to", toAgent.Name);
            handoffSpan.SetAttribute("handoff.reasoning", handoffReasoning);
            handoffSpan.SetAttribute("event.type", "AgentHandoff");

            var properties = new Dictionary<string, string>
            {
                ["EventType"] = "AgentHandoff",
                ["FromAgent"] = fromAgent.Name,
                ["ToAgent"] = toAgent.Name,
                ["ThreadId"] = agentContext.ThreadId.ToString(),
                ["TaskType"] = _taskType,
                ["HandoffReasoning"] = handoffReasoning,
                ["SpanId"] = GetCurrentSpanId(),
                ["ParentSpanId"] = GetCurrentParentSpanId(),
                ["TraceId"] = GetCurrentTraceId()
            };

            _customerLogger.LogCustomEvent("AgentHandoff", properties);
            
            // End handoff span immediately as it's a point-in-time event
            handoffSpan.End();
            return Task.CompletedTask;
        };

        // MODEL HOOKS - capture LLM inputs and outputs
        hooks.ModelGenerationStart += (context, agent, chatMessages, chatOptions) =>
        {
            var agentContext = context.Context ?? throw new InvalidOperationException("Invalid agent context");

            // Create model generation span as child of agent span
            _currentModelSpan = _tracer.StartActiveSpan($"customerlogger.model.{agent.Name}", SpanKind.Internal, _currentAgentSpan);
            _currentModelSpan.SetAttribute("thread.id", agentContext.ThreadId.ToString());
            _currentModelSpan.SetAttribute("agent.name", agent.Name);
            _currentModelSpan.SetAttribute("model.temperature", agent.Temperature.ToString());
            _currentModelSpan.SetAttribute("event.type", "ModelGenerationStart");

            var properties = new Dictionary<string, string>
            {
                ["EventType"] = "ModelGenerationStart",
                ["AgentName"] = agent.Name,
                ["ThreadId"] = agentContext.ThreadId.ToString(),
                ["TaskType"] = _taskType,
                ["ModelInput"] = FormatChatMessages(chatMessages),
                ["Temperature"] = agent.Temperature.ToString(),
                ["SpanId"] = GetCurrentSpanId(),
                ["ParentSpanId"] = GetCurrentParentSpanId(),
                ["TraceId"] = GetCurrentTraceId()
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

            // Add span correlation and end model span
            if (_currentModelSpan != null)
            {
                properties["SpanId"] = GetCurrentSpanId();
                properties["ParentSpanId"] = GetCurrentParentSpanId();
                properties["TraceId"] = GetCurrentTraceId();
                _currentModelSpan.SetAttribute("model.input_tokens", response?.Usage?.InputTokenCount?.ToString() ?? "0");
                _currentModelSpan.SetAttribute("model.output_tokens", response?.Usage?.OutputTokenCount?.ToString() ?? "0");
                _currentModelSpan.SetAttribute("model.id", response?.ModelId ?? "");
                _currentModelSpan.End();
                _currentModelSpan = null;
            }

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

    private static string GetCurrentParentSpanId()
    {
        // Get the ParentSpanId from the current Activity, which should be the span we just created
        return Activity.Current?.ParentSpanId.ToString() ?? "";
    }

    private static string GetCurrentSpanId()
    {
        // Get the SpanId from the current Activity for consistency with TraceController
        return Activity.Current?.SpanId.ToString() ?? "";
    }

    private static string GetCurrentTraceId()
    {
        // Get the TraceId from the current Activity for consistency with TraceController
        return Activity.Current?.TraceId.ToString() ?? "";
    }

    private static string GetSpanId(TelemetrySpan? span)
    {
        // Fallback to span context if Activity.Current is not available
        return Activity.Current?.SpanId.ToString() ?? span?.Context.SpanId.ToString() ?? "";
    }

    private static string GetTraceId(TelemetrySpan? span)
    {
        // Fallback to span context if Activity.Current is not available
        return Activity.Current?.TraceId.ToString() ?? span?.Context.TraceId.ToString() ?? "";
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _currentAgentSpan?.End();
            _currentAgentSpan = null;
            _currentToolSpan?.End();
            _currentToolSpan = null;
            _currentModelSpan?.End();
            _currentModelSpan = null;
            _disposed = true;
        }
    }
}
