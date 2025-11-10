// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Agent.Logging;
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

    private static readonly JsonSerializerOptions PayloadSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public HeartbeatReporter(
        ILogger<HeartbeatReporter> logger,
        IAgentFactory<AgentContext> agentFactory,
        IToolFactory<AgentContext> toolFactory)
    {
        _logger = logger;
        _agentFactory = agentFactory;
        _toolFactory = toolFactory;
    }

    /// <summary>
    /// Logs a heartbeat event using the structured action logging pipeline.
    /// </summary>
    /// <param name="cancellationToken">Token that signals the operation should be cancelled.</param>
    public Task ReportAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.CompletedTask;
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var payload = BuildStatusPayload(
                _agentFactory.RegisteredAgentCount,
                _agentFactory.RegisteredBuiltInAgentCount,
                _agentFactory.RegisteredExtendedAgentCount,
                _toolFactory.RegisteredToolCount);

            stopwatch.Stop();
            _logger.LogAgentAction(
                action: AgentActionEvents.Heartbeat,
                parameter: payload,
                status: "Success",
                duration: stopwatch.ElapsedMilliseconds,
                threadId: string.Empty,
                subAgentName: nameof(HeartbeatReporter));
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

        return Task.CompletedTask;
    }

    private static string BuildStatusPayload(int agentCount, int builtInAgentCount, int extendedAgentCount, int toolCount)
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
            totalToolCount = toolCount
        };

        return JsonSerializer.Serialize(payload, PayloadSerializerOptions);
    }
}
