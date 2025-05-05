// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using FirstPartyAgent.Models;

namespace FirstPartyAgent.Core.Plugins.Interfaces;

public interface IIcmPlugin
{
    public Task<Incident?> GetIncidentInfo(string incidentId);
    public Task<bool> MitigateIncident(string incidentId, string reason);
    public Task<bool> ResolveIncident(string incidentId, string reason);
    public Task<List<DiscussionEntry>?> GetDiscussionEntries(string incidentId, DateTimeOffset queryFrom);
    public Task<bool> AddDiscussionEntry(string incidentId, string text);
    public Task<bool> AddTag(string incidentId, string tag);
    public Task<string> SummarizeICM(string incidentId);
}
