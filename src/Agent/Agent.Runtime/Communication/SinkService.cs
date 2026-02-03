// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
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

    public async Task<Guid> SinkAgentMessageAsync(
        Guid threadId,
        string messageText,
        bool isImageContent = false,
        Approval? approval = null,
        Guid agentResponseMessageId = default,
        DateTime? recordedDateTime = null,
        AgentTaskInfo? agentTaskInfo = null,
        MemorySearchResult? memorySearchResult = null,
        TodoInfo? todoInfo = null,
        bool isComplete = true,
        StreamMessageType? messageType = null)
    {
        var messageId = agentResponseMessageId == default ? Guid.NewGuid() : agentResponseMessageId;
        // Always construct a fresh message object for the initial add scenario.
        var agentMessage = new Message(
            Id: messageId,
            TimeStamp: recordedDateTime ?? DateTime.UtcNow,
            Author: new Author(Role.SREAgent, "agent-default", "Azure SRE Agent"),
            IsImageContent: isImageContent,
            Text: messageText,
            Posted: new Posted(false),
            Approval: approval,
            AgentTaskInfo: agentTaskInfo,
            MemorySearchResult: memorySearchResult,
            TodoInfo: todoInfo,
            IsComplete: isComplete,
            MessageType: messageType
        );

        try
        {
            // Check if message already exists; if so, update it instead of replacing.
            var existingMessage = await _repository.GetMessageAsync(threadId, messageId);
            if (existingMessage != null)
            {
                // Append directly without inserting any newline separator.
                var baseText = existingMessage.Text ?? string.Empty;
                var appendedText = baseText + messageText;

                // Create updated message with new text, task info, and IsComplete flag
                var updatedMessage = existingMessage with
                {
                    Text = appendedText,
                    AgentTaskInfo = agentTaskInfo ?? existingMessage.AgentTaskInfo,
                    IsComplete = isComplete
                };

                // Use full update API to update the entire message
                await _repository.UpdateMessageAsync(threadId, updatedMessage);
            }
            else
            {
                await _repository.AddMessageAsync(threadId, agentMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError("Error adding/appending agent message: {Message}", ex.Message);
            throw;
        }

        return messageId;
    }

    public async Task SinkUserMessageAsync(
        ThreadMessage message,
        bool? isVisibleInUserChatHistory = true,
        DateTime? recordedMessageTime = null)
    {
        // Skip if message was already posted to the repository (Teams=true means already added)
        if (message.Posted?.Teams == true)
        {
            _logger.LogInternalInformation("Message {MessageId} already posted, skipping sink", message.MessageId);
            return;
        }

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

    public async Task<Guid> SinkAgentKnowledgeGraphMessageAsync(
        Guid threadId,
        string messageText,
        Guid agentResponseMessageId,
        KnowledgeGraphSearchResult knowledgeGraphSearchResult)
    {
        var messageId = agentResponseMessageId == default ? Guid.NewGuid() : agentResponseMessageId;
        var agentMessage = new Message(
            Id: messageId,
            TimeStamp: DateTime.UtcNow,
            Author: new Author(Role.SREAgent, "agent-default", "Azure SRE Agent"),
            IsImageContent: false,
            Text: messageText,
            Posted: new Posted(false),
            KnowledgeGraphSearchResult: knowledgeGraphSearchResult
        );

        try
        {
            await _repository.AddMessageAsync(threadId, agentMessage);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError("Error adding knowledge graph message: {Message}", ex.Message);
            throw;
        }

        return messageId;
    }

    public async Task<Guid> SinkAgentGrepSearchMessageAsync(
        Guid threadId,
        string messageText,
        Guid agentResponseMessageId,
        GrepSearchResult grepSearchResult)
    {
        var messageId = agentResponseMessageId == default ? Guid.NewGuid() : agentResponseMessageId;
        var agentMessage = new Message(
            Id: messageId,
            TimeStamp: DateTime.UtcNow,
            Author: new Author(Role.SREAgent, "agent-default", "Azure SRE Agent"),
            IsImageContent: false,
            Text: messageText,
            Posted: new Posted(false),
            GrepSearchResult: grepSearchResult
        );

        try
        {
            await _repository.AddMessageAsync(threadId, agentMessage);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError("Error adding grep search message: {Message}", ex.Message);
            throw;
        }

        return messageId;
    }

    public async Task<Guid> SinkAgentReadFileMessageAsync(
        Guid threadId,
        string messageText,
        Guid agentResponseMessageId,
        ReadFileResult readFileResult)
    {
        var messageId = agentResponseMessageId == default ? Guid.NewGuid() : agentResponseMessageId;
        var agentMessage = new Message(
            Id: messageId,
            TimeStamp: DateTime.UtcNow,
            Author: new Author(Role.SREAgent, "agent-default", "Azure SRE Agent"),
            IsImageContent: false,
            Text: messageText,
            Posted: new Posted(false),
            ReadFileResult: readFileResult
        );

        try
        {
            await _repository.AddMessageAsync(threadId, agentMessage);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError("Error adding read file message: {Message}", ex.Message);
            throw;
        }

        return messageId;
    }

    public async Task<Guid> SinkAgentTerminalMessageAsync(
        Guid threadId,
        string messageText,
        Guid agentResponseMessageId,
        TerminalExecutionResult terminalResult)
    {
        var messageId = agentResponseMessageId == default ? Guid.NewGuid() : agentResponseMessageId;
        var agentMessage = new Message(
            Id: messageId,
            TimeStamp: DateTime.UtcNow,
            Author: new Author(Role.SREAgent, "agent-default", "Azure SRE Agent"),
            IsImageContent: false,
            Text: messageText,
            Posted: new Posted(false),
            TerminalResult: terminalResult
        );

        try
        {
            await _repository.AddMessageAsync(threadId, agentMessage);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError("Error adding terminal message: {Message}", ex.Message);
            throw;
        }

        return messageId;
    }

    public async Task<Guid> SinkAgentUserQuestionMessageAsync(
        Guid threadId,
        string messageText,
        Guid agentResponseMessageId,
        UserQuestion userQuestion)
    {
        var messageId = agentResponseMessageId == default ? Guid.NewGuid() : agentResponseMessageId;
        var agentMessage = new Message(
            Id: messageId,
            TimeStamp: DateTime.UtcNow,
            Author: new Author(Role.SREAgent, "agent-default", "Azure SRE Agent"),
            IsImageContent: false,
            Text: messageText,
            Posted: new Posted(false),
            UserQuestion: userQuestion
        );

        try
        {
            await _repository.AddMessageAsync(threadId, agentMessage);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError("Error adding user question message: {Message}", ex.Message);
            throw;
        }

        return messageId;
    }

    public async Task<Guid> SinkAgentTaskToolExecutionGroupMessageAsync(
        Guid threadId,
        string messageText,
        Guid agentResponseMessageId,
        TaskToolExecutionGroup executionGroup)
    {
        var messageId = agentResponseMessageId == default ? Guid.NewGuid() : agentResponseMessageId;
        var agentMessage = new Message(
            Id: messageId,
            TimeStamp: DateTime.UtcNow,
            Author: new Author(Role.SREAgent, "agent-default", "Azure SRE Agent"),
            IsImageContent: false,
            Text: messageText,
            Posted: new Posted(false),
            TaskToolExecutionGroup: executionGroup
        );

        try
        {
            // Try to update first (for re-persisting on GroupEnd)
            var updated = await _repository.UpdateMessageAsync(threadId, agentMessage);
            if (updated == null)
            {
                // Message doesn't exist, add it (first time on GroupStart)
                await _repository.AddMessageAsync(threadId, agentMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError("Error adding/updating task tool execution group message: {Message}", ex.Message);
            throw;
        }

        return messageId;
    }
}

