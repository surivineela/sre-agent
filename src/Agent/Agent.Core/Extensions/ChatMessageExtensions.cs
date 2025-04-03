// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.AI;

namespace Agent.Core.Extensions
{
    public static class ChatMessageExtensions
    {
        public static ChatMessage GetMessage(this ChatResponse? chatResponse)
        {
            if(chatResponse == null)
            {
                throw new ArgumentNullException(nameof(chatResponse));
            }

            if(chatResponse.Messages.Count != 1)
            {
                throw new ArgumentException(
                    $"ChatResponse contains {chatResponse.Messages.Count} messages but you should only use this extension method when there is a single message. " +
                    "Update the codepath that hit this to handle the fact that there might be multiple messages. " +
                    "For example, if there were multiple cycles of tool calls, there will be multiple messages.", nameof(chatResponse));
            }

            return chatResponse.Messages[0];
        }
    }
}

