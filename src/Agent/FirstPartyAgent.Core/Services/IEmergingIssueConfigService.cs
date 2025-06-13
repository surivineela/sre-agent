using FirstPartyAgent.Core.Models;
using FirstPartyAgent.AgentPrompts;

namespace FirstPartyAgent.Core.Services;

public interface IEmergingIssueConfigService
{
    /// <summary>
    /// Checks if the Emerging Issue service is enabled
    /// </summary>
    /// <returns>True if enabled, false otherwise</returns>
    bool IsEnabled();
    
    /// <summary>
    /// Registers a new emerging issue
    /// </summary>
    /// <param name="emergingIssue">The emerging issue to register</param>
    /// <returns>The ID of the registered emerging issue</returns>
    Task<string> RegisterEmergingIssue(EmergingIssueConfig emergingIssue);
    
    /// <summary>
    /// Updates an existing emerging issue
    /// </summary>
    /// <param name="emergingIssue">The updated emerging issue</param>
    /// <returns>The task representing the asynchronous operation</returns>
    Task UpdateEmergingIssue(EmergingIssueConfig emergingIssue);
    
    /// <summary>
    /// De-registers (removes) an emerging issue
    /// </summary>
    /// <param name="incidentId">The incident ID of the emerging issue to de-register</param>
    /// <returns>The task representing the asynchronous operation</returns>
    Task DeregisterEmergingIssue(string incidentId);
    
    /// <summary>
    /// Gets an emerging issue by incident ID
    /// </summary>
    /// <param name="incidentId">The incident ID to search for</param>
    /// <returns>The emerging issue configuration if found</returns>
    Task<EmergingIssueConfig> GetEmergingIssue(string incidentId);
    
    /// <summary>
    /// Lists all emerging issues
    /// </summary>
    /// <returns>A list of all emerging issues</returns>
    Task<List<EmergingIssueConfig>> ListEmergingIssues();
    
    /// <summary>
    /// Lists emerging issues filtered by owning team
    /// </summary>
    /// <param name="owningTeam">The owning team to filter by</param>
    /// <returns>A list of emerging issues for the specified team</returns>
    Task<List<EmergingIssueConfig>> ListEmergingIssuesByTeam(string owningTeam);
}
