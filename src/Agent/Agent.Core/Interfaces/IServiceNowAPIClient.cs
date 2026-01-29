using Agent.Core.Models.ServiceNow;

namespace Agent.Core.Interfaces
{
    public interface IServiceNowAPIClient
    {
        Task<ServiceNowIncident> GetIncidentAsync(string incidentId);
        Task<List<ServiceNowIncident>> GetIncidentsAsync(uint limit, uint offset, DateTime? lastModifiedDate, string? serviceId, string? titleContains, IEnumerable<string>? priorities = null);
        Task<List<ServiceNowDiscussionEntry>> GetIncidentDiscussionEntriesAsync(string incidentId);
        Task<string> PostDiscussionEntryAsync(string incidentId, string discussionEntry, bool htmlRendering = true);
        Task<string> ChangePriorityAsync(string incidentId, int priority, string discussionEntry);
        Task<string> AcknowledgeIncidentAsync(string incidentId);
        Task<string> ResolveIncidentAsync(string incidentId, string resolutionNotes);
    }
}
