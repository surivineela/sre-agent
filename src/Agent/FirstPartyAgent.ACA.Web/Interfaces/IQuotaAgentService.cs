// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using FirstPartyAgent.Core.Models;
using FirstPartyAgent.Models;

namespace FirstPartyAgent.ACA.Web.Services;

public interface IQuotaAgentService
{
    public Task<QuotaIncidentState> Process(QuotaIncidentState request, IList<ConversationEntry>? disscussions);

    public Task<ChatMessage> ProcessMessageAsync(string message);
}