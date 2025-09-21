// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels;

namespace Agent.Runtime.Services;

public class NullableIncidentFilterManagementService : IIncidentFilterManagementService<NullableIncidentFilterDocument, IncidentFilterDocumentPayload>
{
    public Task<bool> CheckConnectivity()
    {
        return Task.FromResult(false);
    }

    public Task<bool> DeleteIncidentFilter(string filterId)
    {
        return Task.FromResult(true);
    }

    public Task<NullableIncidentFilterDocument?> GetIncidentFilter(string filterId)
    {
        return Task.FromResult<NullableIncidentFilterDocument?>(default);
    }

    public Task<List<IncidentFilterFieldOption>> ListIncidentFilterFieldOptions()
    {
        return Task.FromResult<List<IncidentFilterFieldOption>>([]);
    }

    public Task<List<NullableIncidentFilterDocument>> ListIncidentFilters()
    {
        return Task.FromResult<List<NullableIncidentFilterDocument>>([]);
    }

    public Task<NullableIncidentFilterDocument> SaveIncidentFilter(NullableIncidentFilterDocument incidentFilterDocument)
    {
        return Task.FromResult(incidentFilterDocument);
    }
}
