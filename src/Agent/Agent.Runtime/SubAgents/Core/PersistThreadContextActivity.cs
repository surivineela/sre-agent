// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using System.Text.Json;

namespace Agent.Runtime.SubAgents.Core;

[DurableTask]
public class PersistThreadContextActivity : TaskActivity<PersistThreadContextInput, ThreadContext>
{
    private readonly IThreadRepository _repository;
    private readonly ILogger<PersistThreadContextActivity> _logger;

    public PersistThreadContextActivity(
        IThreadRepository repository,
        ILogger<PersistThreadContextActivity> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public override async Task<ThreadContext> RunAsync(TaskActivityContext context, PersistThreadContextInput input)
    {
        ThreadContext? threadContext;
        try
        {
            threadContext = input.ThreadContext ?? await _repository.GetThreadContextAsync(input.ThreadId);
            // If the thread context is not found, create a new one with Meta agent type as default to avoid null reference exception.
            // This is still helpful as we can get the thread context for state checking.
            // TODO(jianbosun): why there can be null thread context? Need to check those proactively created threads (e.g. CosmosDbAgent).
            threadContext ??= await _repository.AddThreadContextAsync(new ThreadContext(input.ThreadId, AgentTypeEnum.Meta, true));

            if (threadContext == null)
            {
                _logger.LogInternalError($"Failed to get or create thread context, threadId: {input.ThreadId}");
                throw new InvalidOperationException("Failed to get or create thread context");
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to get or create thread context, threadId: {ThreadId}", input.ThreadId);
            throw;
        }

        threadContext.OrchestrationState = new OrchestrationState
        {
            OrchestrationInstanceId = input.OrchestrationInstanceId,
            StepCounter = input.StepCounter,
            ReasoningState = input.ReasoningState,
            StateMessage = input.StateMessage,
            TimeStamp = input.TimeStamp
        };
        _logger.LogInternalInformation("Persisting thread context, threadId: {ThreadId}, state: {OrchestrationState}", input.ThreadId, JsonSerializer.Serialize(threadContext.OrchestrationState));

        try
        {
            threadContext = await _repository.UpdateThreadContextAsync(threadContext);

            if (threadContext == null)
            {
                _logger.LogInternalError($"Failed to update thread context, threadId: {input.ThreadId}");
                throw new InvalidOperationException("Failed to upate create thread context");
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to update thread context, threadId: {ThreadId}", input.ThreadId);
            throw;
        }
        return threadContext;
    }
}

