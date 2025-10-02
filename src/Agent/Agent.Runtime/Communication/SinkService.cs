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

    public async Task<Guid> SinkAgentMessageAsync(Guid threadId, string messageText, bool isImageContent = false, Approval? approval = null, Guid agentResponseMessageId = default, DateTime? recordedDateTime = null, AgentTaskInfo? agentTaskInfo = null, TodoInfo? todoInfo = null)
    {
        var messageId = agentResponseMessageId == default ? Guid.NewGuid() : agentResponseMessageId;
        var agentMessage = new Message(
            Id: messageId,
            TimeStamp: recordedDateTime ?? DateTime.UtcNow,
            Author: new Author(Role.SREAgent, "agent-default", "Azure SRE Agent"),
            IsImageContent: isImageContent,
            Text: messageText,
            Posted: new Posted(false),
            Approval: approval,
            AgentTaskInfo: agentTaskInfo,
            TodoInfo: todoInfo
        );

        try
        {
            await _repository.AddMessageAsync(threadId, agentMessage);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError("Error adding agent message: {Message}", ex.Message);
            throw;
        }

        return messageId;
    }

    public async Task SinkUserMessageAsync(
        ThreadMessage message,
        bool? isVisibleInUserChatHistory = true,
        DateTime? recordedMessageTime = null)
    {
        var role = string.Equals(message.UserId, "agent-default", StringComparison.OrdinalIgnoreCase) ? Role.SREAgent : Role.User;
        var userMessage = new Message(
            Id: message.MessageId,
            TimeStamp: recordedMessageTime ?? DateTime.UtcNow,
            Author: new Author(role, message.UserId, message.DisplayName),
            Text: message.Message,
            IsImageContent: false,
            Posted: message.Posted ?? new Posted(false)
        );
        try
        {
            if (isVisibleInUserChatHistory.GetValueOrDefault())
            {
                await _repository.AddMessageAsync(message.ThreadId, userMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError("Error adding user message: {Message}", ex.Message);
            throw;
        }
    }

    public async Task SinkUserMessageAsync(
        string message,
        Guid threadId)
    {
        var messageId = Guid.NewGuid();
        var userMessage = new Message(
            Id: Guid.NewGuid(),
            TimeStamp: DateTime.UtcNow,
            Author: new Author(Role.User, messageId.ToString(), string.Empty),
            Text: message,
            IsImageContent: false,
            Posted: new Posted(false)
        );

        await _repository.AddMessageAsync(threadId, userMessage);
    }
}


