// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Agent.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Communication;

public class SinkService
{
    private readonly IThreadRepository _repository;
    private readonly ILogger<SinkService> _logger;
    public SinkService(IThreadRepository repository,
      ILogger<SinkService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Guid> SinkAgentMessageAsync(ThreadContext? threadContext, string messageText, bool isImageContent = false)
    {
        var messageId = Guid.NewGuid();
        var agentMessage = new Message(
            Id: messageId,
            TimeStamp: DateTime.UtcNow,
            Author: new Author(Role.SREAgent, "agent-default", "Azure SRE Agent"),
            IsImageContent: isImageContent,
            Text: messageText,
            Posted: new Posted(false)
        );

        try
        {
            await _repository.AddMessageAsync(threadContext?.ThreadId ?? Guid.Empty, agentMessage);

            if (threadContext != null)
            {
                threadContext.AddMessage(agentMessage);
                await _repository.UpdateThreadContextAsync(threadContext);
            }            
        }
        catch (Exception ex)
        {
            _logger.LogError("Error adding agent message: {Message}", ex.Message);
            throw;
        }

        return messageId;
    }

    public async Task SinkUserMessageAsync(
        ThreadContext? threadContext,
        ThreadMessage message,
        bool? isVisibleInUserChatHistory = true)
    {
        var userMessage = new Message(
            Id: message.MessageId,
            TimeStamp: DateTime.UtcNow,
            Author: new Author(Role.User, message.UserId, message.DisplayName),
            Text: message.Message,
            IsImageContent: false,
            Posted: new Posted(false)
        );
        try
        {
            if (isVisibleInUserChatHistory.GetValueOrDefault())
            {
                await _repository.AddMessageAsync(message.ThreadId, userMessage);
            }

            if (threadContext != null)
            {
                threadContext.AddMessage(userMessage);
                await _repository.UpdateThreadContextAsync(threadContext);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Error adding user message: {Message}", ex.Message);
            throw;
        }
    }

    public async Task<Guid> SinkSystemMessageAsync(ThreadContext threadContext, string messageText)
    {
        var messageId = Guid.NewGuid();
        var agentMessage = new Message(
            Id: messageId,
            TimeStamp: DateTime.UtcNow,
            Author: new Author(Role.System, "system-default", "System"),
            IsImageContent: false,
            Text: messageText,
            Posted: new Posted(false)
        );

        try
        {
            threadContext.AddMessage(agentMessage);
            await _repository.UpdateThreadContextAsync(threadContext);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error adding agent message: {Message}", ex.Message);
            throw;
        }

        return messageId;
    }
}


