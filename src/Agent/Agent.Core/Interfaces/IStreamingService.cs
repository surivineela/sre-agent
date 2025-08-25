// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Microsoft.Extensions.AI;

namespace Agent.Core.Interfaces
{
    /// <summary>
    /// Service for streaming real-time messages to clients
    /// </summary>
    public interface IStreamingService
    {
        /// <summary>
        /// Streams a message directly to clients for the specified thread
        /// </summary>
        /// <param name="threadId"></param>
        /// <param name="message"></param>
        /// <param name="type"></param>
        /// <param name="messageId"></param>
        /// <param name="recordedDateTime"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task StreamThreadUpdateAsync(Guid threadId, string message, StreamMessageType? type, Guid? messageId = null, DateTime? recordedDateTime = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Streams a message directly to clients for the specified thread
        /// </summary>
        /// <param name="threadId"></param>
        /// <param name="message"></param>
        /// <param name="type"></param>
        /// <param name="messageId"></param>
        /// <param name="recordedDateTime"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task StreamActionUpdateAsync(Guid threadId, string message, StreamMessageType? type, Guid? messageId = null, DateTime? recordedDateTime = null, CancellationToken cancellationToken = default);


        /// <summary>
        /// Streams a message directly to clients for the specified thread
        /// </summary>
        /// <param name="threadId">The thread ID to stream the message to</param>
        /// <param name="message">The message content to stream</param>
        /// <param name="type">The type of message being streamed, if null just pure normal text</param>
        /// <param name ="agentTaskId> Optional agent task ID associated with the message used when streaming a agent task notification </param>
        /// <param name="cancellationToken">Cancellation token to cancel the streaming operation</param>
        /// <returns>Task representing the async operation</returns>
        Task StreamMessageAsync(Guid threadId, string message, StreamMessageType? type, Guid? messageId = null, DateTime? recordedDateTime = null, Guid? agentTaskId = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Streams a task update directly to clients for the specified thread
        /// </summary>
        /// <param name="threadId">The thread ID to stream the task update to</param>
        /// <param name="taskData">The task data to stream</param>
        /// <param name="messageId">Optional message ID for the update</param>
        /// <param name="recordedDateTime">Optional recorded date time</param>
        /// <param name="cancellationToken">Cancellation token to cancel the streaming operation</param>
        /// <returns>Task representing the async operation</returns>
        Task StreamTaskUpdateAsync(Guid threadId, string taskData, Guid? messageId = null, DateTime? recordedDateTime = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Streams a ChatResponseUpdate message to clients for the specified thread
        /// </summary>
        /// <param name="threadId"></param>
        /// <param name="update"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task StreamChatResponseUpdateAsync(Guid threadId, ChatResponseUpdate update, CancellationToken cancellationToken = default);
    }
}
