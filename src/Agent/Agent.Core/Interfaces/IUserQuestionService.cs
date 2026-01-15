// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;

namespace Agent.Core.Interfaces;

/// <summary>
/// Service for managing interactive user questions (similar to Claude Code's AskUserQuestion).
/// Allows the agent to pause execution, ask a question, and wait for user response.
/// </summary>
public interface IUserQuestionService
{
    /// <summary>
    /// Asks the user a question and waits for their response.
    /// This method blocks until the user responds or the operation is cancelled.
    /// </summary>
    /// <param name="threadId">The thread ID where the question is asked.</param>
    /// <param name="question">The question to present to the user.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The user's response.</returns>
    Task<UserQuestionResponse> AskQuestionAsync(
        Guid threadId,
        UserQuestion question,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits a user's response to a pending question.
    /// This resolves the waiting AskQuestionAsync call.
    /// </summary>
    /// <param name="threadId">The thread ID where the question was asked.</param>
    /// <param name="questionId">The ID of the question being answered.</param>
    /// <param name="response">The user's response.</param>
    Task SubmitResponseAsync(Guid threadId, Guid questionId, UserQuestionResponse response);

    /// <summary>
    /// Cancels a pending question (e.g., when the thread is cancelled).
    /// </summary>
    /// <param name="questionId">The ID of the question to cancel.</param>
    Task CancelQuestionAsync(Guid questionId);

    /// <summary>
    /// Checks if there is a pending question for the given ID.
    /// </summary>
    /// <param name="questionId">The question ID to check.</param>
    /// <returns>True if the question is pending, false otherwise.</returns>
    bool HasPendingQuestion(Guid questionId);
}
