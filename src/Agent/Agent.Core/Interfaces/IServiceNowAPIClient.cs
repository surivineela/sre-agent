using Agent.Core.Models.ServiceNow;

namespace Agent.Core.Interfaces
{
    /// <summary>
    /// Result of a connection health check.
    /// </summary>
    public record ConnectionHealthResult(bool IsHealthy, string? Status = null, string? ErrorMessage = null);

    public interface IServiceNowAPIClient
    {
        /// <summary>
        /// Checks the health of the ServiceNow connection.
        /// For OAuth: Verifies the API Connection resource exists and is authenticated, then tries to fetch an incident.
        /// For Basic Auth: Tries to fetch an incident to verify credentials work.
        /// </summary>
        /// <returns>Connection health result with status and any error message.</returns>
        Task<ConnectionHealthResult> CheckConnectionHealthAsync();

        Task<ServiceNowIncident> GetIncidentAsync(string incidentId);
        Task<List<ServiceNowIncident>> GetIncidentsAsync(uint limit, uint offset, DateTime? lastModifiedDate, string? serviceId, string? titleContains, IEnumerable<string>? priorities = null);
        Task<List<ServiceNowDiscussionEntry>> GetIncidentDiscussionEntriesAsync(string incidentId);
        Task<string> PostDiscussionEntryAsync(string incidentId, string discussionEntry, bool htmlRendering = true);
        Task<string> ChangePriorityAsync(string incidentId, int priority, string discussionEntry);
        Task<string> AcknowledgeIncidentAsync(string incidentId);
        Task<string> ResolveIncidentAsync(string incidentId, string resolutionNotes);
    }
}
