// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using FirstPartyAgent.Core.Models;

namespace FirstPartyAgent.Core.Services;


public interface IChatService
{
    Task<ChatMessage> ProcessMessageAsync(MessageRequestBody message);
} 
