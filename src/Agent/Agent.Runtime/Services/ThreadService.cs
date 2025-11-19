// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;

namespace Agent.Runtime.Services;

// ThreadService will always take ThreadContext as a parameter.
public class ThreadService
{
    private readonly IThreadRepository _threadRepository;

    public ThreadService(
        IThreadRepository threadRepository)
    {
        _threadRepository = threadRepository;
    }

    public async Task<string> GetLastUserMessage(Guid threadId)
    {
        var ThreadMessages = await _threadRepository.GetMessagesAsync(threadId);
        var lastUserMessage = ThreadMessages.LastOrDefault(m => m.Author.Role == Role.User);
        return lastUserMessage?.Text ?? string.Empty;
    }
}
