using Agent.Core.Models.ServiceNow;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Agent.Core.Interfaces
{
    public interface IServiceNowAPIClient
    {
        Task<ServiceNowIncident> GetIncidentAsync(string incidentId);
        Task<List<ServiceNowIncident>> GetIncidentsAsync(uint limit, uint offset, DateTime? lastModifiedDate, string? serviceId, string? titleContains, string? priority = null);
        Task<List<ServiceNowDiscussionEntry>> GetIncidentDiscussionEntriesAsync(string incidentId);
        Task<string> PostDiscussionEntryAsync(string incidentId, string discussionEntry, bool htmlRendering = true);
        Task<string> ChangePriorityAsync(string incidentId, int priority, string discussionEntry);
        Task<string> AcknowledgeIncidentAsync(string incidentId);
        Task<string> ResolveIncidentAsync(string incidentId, string resolutionNotes);
    }
}
