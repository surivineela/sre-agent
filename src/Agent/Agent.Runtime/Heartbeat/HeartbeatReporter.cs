// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Agent.Logging;
using Agent.Runtime.Services;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Heartbeat;

/// <summary>
/// Emits periodic heartbeat logs that capture the current health of the agent host.
/// </summary>
public class HeartbeatReporter
{
    private readonly ILogger<HeartbeatReporter> _logger;
    private readonly IAgentFactory<AgentContext> _agentFactory;
    private readonly IToolFactory<AgentContext> _toolFactory;
    private readonly IExtendedAgentRepository _extendedAgentRepository;
    private readonly IIncidentHandlerManagementService _incidentHandlerManagementService;

    private static readonly JsonSerializerOptions PayloadSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public HeartbeatReporter(
        ILogger<HeartbeatReporter> logger,
        IAgentFactory<AgentContext> agentFactory,
        IToolFactory<AgentContext> toolFactory,
        IExtendedAgentRepository extendedAgentRepository,
        IIncidentHandlerManagementService incidentHandlerManagementService)
    {
        _logger = logger;
        _agentFactory = agentFactory;
        _toolFactory = toolFactory;
        _extendedAgentRepository = extendedAgentRepository;
        _incidentHandlerManagementService = incidentHandlerManagementService;
    }

    /// <summary>
    /// Logs a heartbeat event using the structured action logging pipeline.
    /// </summary>
    /// <param name="cancellationToken">Token that signals the operation should be cancelled.</param>
    public async Task ReportAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var incidentHandlers = await _incidentHandlerManagementService.ListIncidentHandlers();
            var incidentHandlerCount = incidentHandlers?.Count ?? 0;

            var payload = BuildStatusPayload(
                _agentFactory.RegisteredAgentCount,
                _agentFactory.RegisteredBuiltInAgentCount,
                _agentFactory.RegisteredExtendedAgentCount,
                _toolFactory.RegisteredToolCount,
                _toolFactory.RegisteredBuiltInToolCount,
                _toolFactory.RegisteredExtendedToolCount,
                incidentHandlerCount);

            stopwatch.Stop();
            _logger.LogAgentAction(
                action: AgentActionEvents.Heartbeat,
                parameter: payload,
                status: "Success",
                duration: stopwatch.ElapsedMilliseconds,
                threadId: string.Empty,
                subAgentName: nameof(HeartbeatReporter));

            // Log detailed information about extended agents and tools
            await LogExtendedAgentsAndToolsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogAgentActionError(
                exception: ex,
                action: AgentActionEvents.Heartbeat,
                parameter: nameof(HeartbeatReporter),
                status: "Failure",
                duration: stopwatch.ElapsedMilliseconds,
                threadId: string.Empty);

            _logger.LogInternalError(ex, "Heartbeat reporter failed to emit agent status.");
        }
    }

    /// <summary>
    /// Logs detailed information about extended agents and tools.
    /// </summary>
    private async Task LogExtendedAgentsAndToolsAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Log extended agents
            var extendedAgents = await _extendedAgentRepository.GetAgentsAsync(limit: 1000);
            foreach (var agent in extendedAgents)
            {
                try
                {
                    var agentInfo = new
                    {
                        type = "ExtendedAgent",
                        name = agent.Name,
                        createdAt = agent.Metadata?.CreatedAt,
                        owner = agent.Metadata?.Owner,
                        tools = agent.Tools?.ToArray() ?? Array.Empty<string>(),
                        version = agent.Metadata?.Version,
                        tags = agent.Metadata?.Tags?.ToArray() ?? Array.Empty<string>()
                    };

                    var agentPayload = JsonSerializer.Serialize(agentInfo, PayloadSerializerOptions);
                    _logger.LogInternalInformation("[ExtendedAgent] {Payload}", agentPayload);
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, "Failed to log extended agent: {AgentName}", agent?.Name ?? "unknown");
                }
            }

            // Log extended tools
            var extendedTools = await _extendedAgentRepository.GetToolsAsync(limit: 1000);
            foreach (var tool in extendedTools)
            {
                try
                {
                    var toolInfo = new
                    {
                        type = "ExtendedTool",
                        name = tool.Name,
                        toolType = tool.Type,
                        createdAt = tool.Metadata?.CreatedAt,
                        owner = tool.Metadata?.Owner,
                        subscription = tool.Metadata != null ? ExtractSubscriptionFromMetadata(tool.Metadata) : null,
                        version = tool.Metadata?.Version,
                        tags = tool.Metadata?.Tags?.ToArray() ?? Array.Empty<string>()
                    };

                    var toolPayload = JsonSerializer.Serialize(toolInfo, PayloadSerializerOptions);
                    _logger.LogInternalInformation("[ExtendedTool] {Payload}", toolPayload);
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, "Failed to log extended tool: {ToolName}", tool?.Name ?? "unknown");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to log extended agents and tools");
        }
    }

    /// <summary>
    /// Extracts subscription from metadata tags.
    /// </summary>
    private static string? ExtractSubscriptionFromMetadata(Data.DataModels.ResourceMetadata metadata)
    {
        // Check tags for subscription information
        if (metadata.Tags != null)
        {
            var subscriptionTag = metadata.Tags.FirstOrDefault(t =>
                t.StartsWith("subscription:", StringComparison.OrdinalIgnoreCase));
            if (subscriptionTag != null)
            {
                return subscriptionTag.Split(':', 2).LastOrDefault();
            }
        }

        return null;
    }

    private static string BuildStatusPayload(int agentCount, int builtInAgentCount, int extendedAgentCount, int toolCount, int builtInToolCount, int extendedToolCount, int incidentHandlerCount)
    {
        using var process = Process.GetCurrentProcess();
        var uptime = DateTimeOffset.UtcNow - process.StartTime.ToUniversalTime();

        ThreadPool.GetAvailableThreads(out var availableWorker, out var availableIo);
        ThreadPool.GetMaxThreads(out var maxWorker, out var maxIo);

        var payload = new
        {
            totalAgentCount = agentCount,
            builtInAgentCount = builtInAgentCount,
            extendedAgentCount = extendedAgentCount,
            totalToolCount = toolCount,
            builtInToolCount = builtInToolCount,
            extendedToolCount = extendedToolCount,
            incidentHandlerCount = incidentHandlerCount
        };

        return JsonSerializer.Serialize(payload, PayloadSerializerOptions);
    }
}
