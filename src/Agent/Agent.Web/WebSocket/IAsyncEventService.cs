// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.AI;

namespace Agent.Web.WebSocket
{
    public interface IAsyncEventService
    {
        void SendMessageAsync(string message);
        void SendMessageAsync(ChatResponseUpdate message);
    }
}
