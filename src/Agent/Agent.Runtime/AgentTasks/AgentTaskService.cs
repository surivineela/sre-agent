// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using System.Threading.Channels;
using Agent.Core.Models.Api.v1;
using Agent.Data.Repositories;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.AgentTasks;

public class AgentTaskService(
    IAgentTasksRepository agentTasksRepository,
    AgentTaskHandlerFactory agentTaskHandlerFactory,
    ILogger<AgentTaskService> logger
) : BackgroundService
{
    private readonly Channel<AgentTask> _agentTaskExecutionQueue = Channel.CreateUnbounded<AgentTask>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false,
        AllowSynchronousContinuations = true
    });

    private readonly ConcurrentDictionary<Guid, AgentTaskExecution> _agentTaskExecutions = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (await _agentTaskExecutionQueue.Reader.WaitToReadAsync(stoppingToken))
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    _agentTaskExecutionQueue.Writer.Complete();
                    break;
                }

                while (_agentTaskExecutionQueue.Reader.TryRead(out var agentTask))
                {
                    var handler = agentTaskHandlerFactory.GetHandler(agentTask.Type);

                    var execution = new AgentTaskExecution(
                        execution: (cancellationToken) => handler.ExecuteAsync(agentTask, cancellationToken),
                        serviceStopToken: stoppingToken
                    );

                    // don't actually run the execution until we confirmed it can be added to the dictionary
                    if (_agentTaskExecutions.TryAdd(agentTask.Id, execution))
                    {
                        _ = execution.RunAsync();
                    }
                    else
                    {
                        // key already exists
                        logger.LogInternalWarning("Agent task execution already exists: {AgentTaskId}", agentTask.Id);
                    }
                }
            }
        }
        catch (OperationCanceledException e)
        {
            logger.LogInternalInformation("Agent task service is stopping");
            _agentTaskExecutionQueue.Writer.TryComplete(e);
        }
    }

    public async Task StartAgentTaskAsync(AgentTask task)
    {
        if (task == null)
        {
            throw new ArgumentNullException(nameof(task), "Agent task cannot be null");
        }

        task = await agentTasksRepository.CreateAgentTaskAsync(task);

        if (!_agentTaskExecutionQueue.Writer.TryWrite(task))
        {
            throw new InvalidOperationException($"Failed to add agent task to the execution queue: {task.Id}");
        }
    }

    public void CancelAgentTask(Guid agentTaskId)
    {
        if (_agentTaskExecutions.TryGetValue(agentTaskId, out var execution))
        {
            execution.Cancel();
            _agentTaskExecutions.TryRemove(agentTaskId, out _);
        }
        else
        {
            // not found, log
            logger.LogInternalWarning("Agent task not found: {AgentTaskId}", agentTaskId);
        }
    }
}
