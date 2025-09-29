// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels;

namespace Agent.Plugins.Interface;

public interface IPagerDutyIncidentPlugin
{
    /// <summary>
    /// Get PagerDuty incidents related to a resource
    /// </summary>
    /// <param name="resourceId">Azure resource id</param>
    /// <param name="maxResults">max number of incidents to return</param>
    /// <returns>a list of pager duty incidents</returns>
    Task<List<PagerDutyIncidentDocument>> GetPagerDutyIncidentsAsync(string resourceId, uint maxResults = 5);

    /// <summary>
    /// Get a specific PagerDuty incident by incident ID
    /// </summary>
    /// <param name="incidentId">PagerDuty incident ID</param>
    /// <returns>a list containing the specific incident</returns>
    Task<List<PagerDutyIncidentDocument>> GetPagerDutyIncidentById(string incidentId);

    Task ResolvePagerDutyIncidentAsync(string incidentId);
    Task AcknowledgePagerDutyIncidentAsync(string incidentId);

    Task<string> AddNoteToIncident(string incidentId, string note);

    /// <summary>
    /// Close the Azure Monitor Alert.
    /// This only updates the Alert Status on SRE backend.
    /// </summary>
    /// <param name="alertId"></param>
    /// <returns></returns>
    Task CloseAzureMonitorAlert(string alertId);
}
