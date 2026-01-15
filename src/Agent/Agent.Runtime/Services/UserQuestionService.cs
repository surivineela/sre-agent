// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Logging;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Services;

/// <summary>
/// Service for managing interactive user questions.
/// Uses TaskCompletionSource to block tool execution until user responds.
/// </summary>
public class UserQuestionService : IUserQuestionService
{
    private readonly ILogger<UserQuestionService> _logger;
    private readonly IAgentOutboundCommunicationService _communicationService;
    private readonly IThreadRepository _threadRepository;

    /// <summary>
    /// Stores pending questions awaiting user response.
    /// Key: QuestionId, Value: TaskCompletionSource for the response.
    /// </summary>
    private static readonly ConcurrentDictionary<Guid, TaskCompletionSource<UserQuestionResponse>> _pendingQuestions = new();

    /// <summary>
    /// Stores question metadata for updating after response.
    /// Key: QuestionId, Value: (ThreadId, MessageId, Question).
    /// </summary>
    private static readonly ConcurrentDictionary<Guid, (Guid ThreadId, Guid MessageId, UserQuestion Question)> _questionMetadata = new();

    public UserQuestionService(
        ILogger<UserQuestionService> logger,
        IAgentOutboundCommunicationService communicationService,
        IThreadRepository threadRepository)
    {
        _logger = logger;
        _communicationService = communicationService;
        _threadRepository = threadRepository;
    }

    /// <inheritdoc />
    public async Task<UserQuestionResponse> AskQuestionAsync(
        Guid threadId,
        UserQuestion question,
        CancellationToken cancellationToken = default)
    {
        if (threadId == Guid.Empty)
        {
            throw new ArgumentException("Thread ID cannot be empty.", nameof(threadId));
        }

        // Ensure question has an ID
        if (question.QuestionId == Guid.Empty)
        {
            question.QuestionId = Guid.NewGuid();
        }

        question.CreatedAt = DateTime.UtcNow;
        question.Status = UserQuestionStatus.Pending;

        _logger.LogInternalInformation(
            "Asking user question {QuestionId} in thread {ThreadId}: {Question}",
            question.QuestionId, threadId, question.Question);

        // Create completion source for blocking
        var tcs = new TaskCompletionSource<UserQuestionResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        // Register the pending question
        if (!_pendingQuestions.TryAdd(question.QuestionId, tcs))
        {
            throw new InvalidOperationException($"Question {question.QuestionId} is already pending.");
        }

        _logger.LogInternalInformation(
            "Registered question {QuestionId} in pending questions dictionary. Total pending: {Count}",
            question.QuestionId, _pendingQuestions.Count);

        try
        {
            // Stream and persist the question
            var messageId = await _communicationService.AppendAgentUserQuestionMessage(threadId, question);

            // Store metadata for updating the question when answered
            _questionMetadata[question.QuestionId] = (threadId, messageId, question);

            // Wait for user response with cancellation support
            using var registration = cancellationToken.Register(() =>
            {
                tcs.TrySetCanceled(cancellationToken);
            });

            _logger.LogInternalInformation(
                "Waiting for user response to question {QuestionId}",
                question.QuestionId);

            var response = await tcs.Task;

            _logger.LogInternalInformation(
                "Received response for question {QuestionId}: SelectedLabel={SelectedLabel}, FreeText={FreeText}",
                question.QuestionId, response.SelectedLabel, response.FreeText);

            return response;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInternalInformation(
                "Question {QuestionId} was cancelled",
                question.QuestionId);

            // Update question status
            await UpdateQuestionStatusAsync(question.QuestionId, UserQuestionStatus.Cancelled);
            throw;
        }
        finally
        {
            // Clean up pending question
            _pendingQuestions.TryRemove(question.QuestionId, out _);
            _questionMetadata.TryRemove(question.QuestionId, out _);
        }
    }

    /// <inheritdoc />
    public async Task SubmitResponseAsync(Guid threadId, Guid questionId, UserQuestionResponse response)
    {
        _logger.LogInternalInformation(
            "SubmitResponseAsync called - QuestionId={QuestionId}, ThreadId={ThreadId}, SelectedLabel={SelectedLabel}, FreeText={FreeText}",
            questionId, threadId, response.SelectedLabel, response.FreeText);

        // Log the current state of pending questions for debugging
        _logger.LogInternalInformation(
            "Current pending questions count: {Count}, Keys: [{Keys}]",
            _pendingQuestions.Count,
            string.Join(", ", _pendingQuestions.Keys.Select(k => k.ToString())));

        if (!_pendingQuestions.TryGetValue(questionId, out var tcs))
        {
            // Question not in active pending dictionary - it may have expired or been from a previous session
            // Try to update it in the database anyway so the UI shows the answered state
            _logger.LogInternalWarning(
                "Question {QuestionId} not in active pending dictionary. Attempting to update in database.",
                questionId);

            var updated = await TryUpdateExpiredQuestionAsync(threadId, questionId, response);
            if (updated)
            {
                _logger.LogInternalInformation(
                    "Updated expired question {QuestionId} in database.",
                    questionId);
                return; // Successfully updated the DB record, but agent won't receive it
            }

            throw new InvalidOperationException($"Question {questionId} not found or already answered.");
        }

        // Update the question with the response
        await UpdateQuestionWithResponseAsync(questionId, response);

        // Complete the waiting task - this unblocks AskQuestionAsync
        _logger.LogInternalInformation(
            "About to resolve TCS for question {QuestionId}, TCS status: IsCompleted={IsCompleted}, IsCanceled={IsCanceled}, IsFaulted={IsFaulted}",
            questionId, tcs.Task.IsCompleted, tcs.Task.IsCanceled, tcs.Task.IsFaulted);

        var setResultSuccess = tcs.TrySetResult(response);

        _logger.LogInternalInformation(
            "TCS.TrySetResult returned {Success} for question {QuestionId}",
            setResultSuccess, questionId);

        if (!setResultSuccess)
        {
            _logger.LogInternalWarning(
                "Failed to set result for question {QuestionId}. TCS may have been already completed, canceled, or faulted.",
                questionId);
        }
        else
        {
            _logger.LogInternalInformation(
                "Successfully unblocked AskQuestionAsync for question {QuestionId}",
                questionId);
        }
    }

    /// <inheritdoc />
    public Task CancelQuestionAsync(Guid questionId)
    {
        _logger.LogInternalInformation("Cancelling question {QuestionId}", questionId);

        if (_pendingQuestions.TryGetValue(questionId, out var tcs))
        {
            tcs.TrySetCanceled();
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public bool HasPendingQuestion(Guid questionId)
    {
        return _pendingQuestions.ContainsKey(questionId);
    }

    /// <summary>
    /// Updates the question in the database with the user's response.
    /// </summary>
    private async Task UpdateQuestionWithResponseAsync(Guid questionId, UserQuestionResponse response)
    {
        if (!_questionMetadata.TryGetValue(questionId, out var metadata))
        {
            _logger.LogInternalWarning("Question metadata not found for {QuestionId}", questionId);
            return;
        }

        try
        {
            var (threadId, messageId, question) = metadata;

            // Update question with response
            question.Status = UserQuestionStatus.Answered;
            question.SelectedOptionLabel = response.SelectedLabel;
            question.FreeTextResponse = response.FreeText;
            question.AnsweredAt = DateTime.UtcNow;

            // Get and update the message
            var message = await _threadRepository.GetMessageAsync(threadId, messageId);
            if (message != null)
            {
                var updatedMessage = message with { UserQuestion = question };
                await _threadRepository.UpdateMessageAsync(threadId, updatedMessage);

                _logger.LogInternalInformation(
                    "Updated message {MessageId} with question response",
                    messageId);
            }

            // Notify frontend of the update so the UI reflects the answered state
            await _communicationService.NotifyUserQuestionUpdate(threadId, question, messageId);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to update question {QuestionId} with response", questionId);
        }
    }

    /// <summary>
    /// Attempts to update an expired question directly in the database.
    /// This handles cases where the question was from a previous session or the agent timed out.
    /// </summary>
    private async Task<bool> TryUpdateExpiredQuestionAsync(Guid threadId, Guid questionId, UserQuestionResponse response)
    {
        try
        {
            // Get all messages in the thread and find the one with this question
            var messages = await _threadRepository.GetMessagesAsync(threadId);
            var message = messages.FirstOrDefault(m =>
                m.UserQuestion != null && m.UserQuestion.QuestionId == questionId);

            if (message == null || message.UserQuestion == null)
            {
                _logger.LogInternalWarning("Could not find message with question {QuestionId} in thread {ThreadId}.", questionId, threadId);
                return false;
            }

            // Check if already answered
            if (message.UserQuestion.Status == UserQuestionStatus.Answered)
            {
                _logger.LogInternalInformation("Question {QuestionId} was already answered.", questionId);
                return false;
            }

            // Update the question properties
            message.UserQuestion.Status = UserQuestionStatus.Answered;
            message.UserQuestion.SelectedOptionLabel = response.SelectedLabel;
            message.UserQuestion.FreeTextResponse = response.FreeText;
            message.UserQuestion.AnsweredAt = DateTime.UtcNow;
            await _threadRepository.UpdateMessageAsync(threadId, message);

            // Notify frontend of the update so the UI reflects the answered state
            await _communicationService.NotifyUserQuestionUpdate(threadId, message.UserQuestion, message.Id);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to update expired question {QuestionId}", questionId);
            return false;
        }
    }

    /// <summary>
    /// Updates just the status of a question (e.g., when cancelled).
    /// </summary>
    private async Task UpdateQuestionStatusAsync(Guid questionId, UserQuestionStatus status)
    {
        if (!_questionMetadata.TryGetValue(questionId, out var metadata))
        {
            return;
        }

        try
        {
            var (threadId, messageId, question) = metadata;
            question.Status = status;

            var message = await _threadRepository.GetMessageAsync(threadId, messageId);
            if (message != null)
            {
                var updatedMessage = message with { UserQuestion = question };
                await _threadRepository.UpdateMessageAsync(threadId, updatedMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to update question {QuestionId} status", questionId);
        }
    }
}
