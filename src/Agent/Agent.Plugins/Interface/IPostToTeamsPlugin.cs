// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Bot.Schema;

namespace Agent.Plugins.Interface
{
    public interface IPostToTeamsPlugin
    {
        Task<string> PostAsync(string message);
        Task<bool> PostTeamsMessage(string threadId, Activity message, string messageId = "");
        Task<bool> CreateTeamsThread(string threadId, string initialMessage, string messageId = "");
    }
}

