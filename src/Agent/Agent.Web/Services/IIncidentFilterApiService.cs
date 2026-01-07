// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels;
using Agent.Web.ApiResources;
using Agent.Web.Views.v2;

namespace Agent.Web.Services;

public interface IIncidentFilterApiService
{
    // Incident Filter operations
    Task<ApiCommandResult<IIncidentFilterDocument>> GetIncidentFilterAsync(string filterId);
    Task<ApiCommandResult<IIncidentFilterDocument>> CreateOrUpdateIncidentFilterAsync(string filterId, IIncidentFilterDocument model, bool dryRun = false);
    Task<ApiCommandResult<IIncidentFilterDocument>> DeleteIncidentFilterAsync(string filterId, bool dryRun = false);
    Task<ApiCommandResult<List<IIncidentFilterDocument>>> GetIncidentFiltersAsync();
}
