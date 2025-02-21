// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;

namespace FirstPartyAgent.Web.Services;


public interface IChatService
{
    Task<ChatMessage> ProcessMessageAsync(string message);
} 
