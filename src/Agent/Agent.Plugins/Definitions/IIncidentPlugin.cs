// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
using Agent.Data.DataModels;

namespace Agent.Plugins.Definitions;

public interface IIncidentPlugin
{
    /// <summary>
    /// Get PagerDuty incidents realted to a resource
    /// </summary>
    /// <param name="resourceId">Azure resource id</param>
    /// <param name="maxResults">max number of incidents to return</param>
    /// <returns>a list of pager duty incidents</returns>
    Task<List<PagerDutyIncidentDocument>> GetPagerDutyIncidentsAsync(string resourceId, uint maxResults = 5);
}