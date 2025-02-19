// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using FirstPartyAgent.Models;

namespace FirstPartyAgent.Web.Services;

public interface IQuotaAgentService
{
    public Task<QuotaIncidentState> Process(QuotaIncidentState request, IList<Discussion>? disscussions);

    public Task<ChatMessage> ProcessMessageAsync(string message);
}