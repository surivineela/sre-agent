// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using FirstPartyAgent.Models;

namespace FirstPartyAgent.Plugins
{
    public interface IIcmPlugin
    {
        public Task<Incident?> GetIncidentInfo(string incidentId);
        public Task<List<Incident>> GetIncidents(string tenant, string metrics);
        public Task<bool> MitigateIncident(string incidentId, string reason);
        public Task<bool> ResolveIncident(string incidentId, string reason);
        public Task<bool> AddTag(string incidentId, string tag);
        public Task<List<DiscussionEntry>?> GetDiscussionEntries(string incidentId, DateTimeOffset queryFrom);
        public Task<bool> AddDiscussionEntry(string incidentId, string text);
    }
}
