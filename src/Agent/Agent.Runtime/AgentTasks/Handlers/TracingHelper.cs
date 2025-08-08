// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using System.Text.Json.Serialization;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Agent.Logging;
using Microsoft.Extensions.AI;
using OpenTelemetry.Trace;

namespace Agent.Runtime.AgentTasks.Handlers;

public sealed class TracingHelper : IDisposable
{
    private readonly Tracer _tracer;
    private readonly string _threadId;
    private readonly string _taskType;
    private TelemetrySpan? _rootSpan;
    private TelemetrySpan? _currentStepSpan;
    private TelemetrySpan? _currentAgentSpan;
    private TelemetrySpan? _currentToolSpan;
    private TelemetrySpan? _currentGenerationSpan;
    private bool _disposed = false;

    /// <summary>
    /// Gets a value indicating whether the object has been disposed.
    /// </summary>
    public bool IsDisposed => _disposed;

    public TracingHelper(Tracer tracer, string threadId, string taskType, string? serializedTaskInput = null)
    {
        _tracer = tracer;
        _threadId = threadId;
        _taskType = taskType;
        _rootSpan = tracer.StartRootSpan($"task.{taskType}");
        _rootSpan.SetAttribute(TraceAttribute.ThreadId, threadId);
        _rootSpan.SetAttribute(TraceAttribute.OperationName, TraceOperationName.AgentTask);
        _rootSpan.SetAttribute(TraceAttribute.AgentTaskType, taskType);
        if (serializedTaskInput is not null)
        {
            _rootSpan.SetAttribute(TraceAttribute.AgentTaskInput, serializedTaskInput);
        }
    }

    public RunHooks<AgentContext> GetAgentTaskTracingHooks()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TracingHelper));
        }

        return new RunHooks<AgentContext>
        {
            OnAgentStart = (context, agent) =>
            {
                if (_currentAgentSpan is not null)
                {
                    _currentAgentSpan.End();
                    _currentAgentSpan = null;
                }

                var agentContext = context.Context ?? throw new InvalidOperationException("Invalid agent context");

                _currentAgentSpan = _tracer.StartActiveSpan($"invoke.agent.{agent.Name}", SpanKind.Internal, _currentStepSpan);
                _currentAgentSpan.SetAttribute(TraceAttribute.ThreadId, agentContext.ThreadId.ToString());
                _currentAgentSpan.SetAttribute(TraceAttribute.AgentName, agent.Name);
                _currentAgentSpan.SetAttribute(TraceAttribute.OperationName, TraceOperationName.InvokeAgent);

                return Task.CompletedTask;
            },
            OnAgentEnd = (context, agent, output) =>
            {
                _currentAgentSpan?.End();
                _currentAgentSpan = null;
                return Task.CompletedTask;
            },
            OnHandoff = (context, agent, handoffAgent, handoffReasoning) =>
            {
                var agentContext = context.Context ?? throw new InvalidOperationException("Invalid agent context");
                _currentToolSpan = _tracer.StartSpan($"handoff", SpanKind.Internal, _currentAgentSpan);
                _currentToolSpan.SetAttribute(TraceAttribute.ThreadId, agentContext.ThreadId.ToString());
                _currentToolSpan.SetAttribute(TraceAttribute.OperationName, TraceOperationName.Handoff);
                _currentToolSpan.SetAttribute(TraceAttribute.AgentName, agent.Name);
                _currentToolSpan.SetAttribute(TraceAttribute.HandoffAgentName, handoffAgent.Name);
                _currentToolSpan.SetAttribute(TraceAttribute.HandoffReasoning, handoffReasoning);
                _currentToolSpan.End();
                _currentToolSpan = null;
                _currentAgentSpan?.End();
                return Task.CompletedTask;
            },
            OnToolStart = (context, agent, tool, input) =>
            {
                var agentContext = context.Context ?? throw new InvalidOperationException("Invalid agent context");
                _currentToolSpan = _tracer.StartActiveSpan($"tool.{tool.Name}", SpanKind.Internal, _currentAgentSpan);
                _currentToolSpan.SetAttribute(TraceAttribute.ThreadId, agentContext.ThreadId.ToString());
                _currentToolSpan.SetAttribute(TraceAttribute.OperationName, TraceOperationName.Tool);
                _currentToolSpan.SetAttribute(TraceAttribute.AgentName, agent.Name);
                _currentToolSpan.SetAttribute(TraceAttribute.ToolName, tool.Name);
                _currentToolSpan.SetAttribute(TraceAttribute.ToolInput, FormatToolArguments(input));
                _currentToolSpan.SetAttribute(TraceAttribute.ModelTemperature, agent.Temperature.ToString());
                _currentToolSpan.SetAttribute(TraceAttribute.ToolDescription, tool.Description);

                return Task.CompletedTask;
            },
            OnToolEnd = (context, agent, tool, output) =>
            {
                _currentToolSpan?.SetAttribute(TraceAttribute.ToolOutput, output?.ToString() ?? string.Empty);
                _currentToolSpan?.End();
                _currentToolSpan = null;
                return Task.CompletedTask;
            },
            OnModelGenerationStart = (context, agent, messages, chatOptions) =>
            {
                var agentContext = context.Context ?? throw new InvalidOperationException("Invalid agent context");
                _currentGenerationSpan = _tracer.StartActiveSpan($"model_generation", SpanKind.Internal, _currentAgentSpan);
                _currentGenerationSpan.SetAttribute(TraceAttribute.ThreadId, agentContext.ThreadId.ToString());
                _currentGenerationSpan.SetAttribute(TraceAttribute.AgentName, agent.Name);
                _currentGenerationSpan.SetAttribute(TraceAttribute.OperationName, TraceOperationName.ModelGeneration);
                _currentGenerationSpan.SetAttribute(TraceAttribute.ModelInput, FormatChatMessages(messages));

                return Task.CompletedTask;
            },
            OnModelGenerationEnd = (context, agent, response) =>
            {
                _currentGenerationSpan?.SetAttribute(TraceAttribute.ModelOutput, FormatChatMessages(response?.Messages ?? []));
                _currentGenerationSpan?.SetAttribute(TraceAttribute.ModelInputTokensCount, response?.Usage?.InputTokenCount?.ToString() ?? string.Empty);
                _currentGenerationSpan?.SetAttribute(TraceAttribute.ModelOutputTokensCount, response?.Usage?.OutputTokenCount?.ToString() ?? string.Empty);
                _currentGenerationSpan?.SetAttribute(TraceAttribute.ModelTotalTokensCount, response?.Usage?.TotalTokenCount?.ToString() ?? string.Empty);
                _currentGenerationSpan?.SetAttribute(TraceAttribute.ModelTemperature, agent?.Temperature.ToString() ?? string.Empty);
                _currentGenerationSpan?.End();
                _currentGenerationSpan = null;
                return Task.CompletedTask;
            }
        };
    }

    public void StartAgentTaskStepSpan(string stepName)
    {
        if (_currentStepSpan is not null)
        {
            _currentStepSpan.End();
        }
        _currentStepSpan = _tracer.StartActiveSpan($"task.{_taskType}.{stepName}", SpanKind.Internal, _rootSpan);
        _currentStepSpan.SetAttribute(TraceAttribute.ThreadId, _threadId);
        _currentStepSpan.SetAttribute(TraceAttribute.AgentTaskStep, stepName);
        _currentStepSpan.SetAttribute(TraceAttribute.OperationName, TraceOperationName.AgentTaskStep);
    }

    public void EndAgentTaskStepSpan()
    {
        _currentStepSpan?.End();
        _currentStepSpan = null;
    }

    private static readonly JsonSerializerOptions _toolArgumentsJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly JsonSerializerOptions _chatMessageJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
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
            var formattedMessages = messages.Select(message => new
            {
                Role = message.Role.ToString(),
                Contents = message.Contents?.Select(content => new
                {
                    Type = content.GetType().Name,
                    Value = content
                })
            }).ToList();

            return JsonSerializer.Serialize(formattedMessages, _chatMessageJsonOptions);
        }
        catch (Exception ex)
        {
            return $"Error formatting messages: {ex.Message}\n" +
                   string.Join("\n", messages.Select(m => $"{m.Role}: {m.Text?[..50]}..."));
        }
    }

    ~TracingHelper()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Dispose managed resources
                _rootSpan?.End();
                _rootSpan = null;
                _currentStepSpan?.End();
                _currentStepSpan = null;
                _currentAgentSpan?.End();
                _currentAgentSpan = null;
                _currentToolSpan?.End();
                _currentToolSpan = null;
                _currentGenerationSpan?.End();
                _currentGenerationSpan = null;
            }

            _disposed = true;
        }
    }
}
