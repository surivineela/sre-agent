// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels;

namespace Agent.Data;

public interface IIncidentAPI<TIncident, TIncidentFilterDocumentPayload>
    where TIncidentFilterDocumentPayload : IncidentFilterDocumentPayload
{
    Task<TIncident> GetIncidentAsync(string id);

    Task<IEnumerable<TIncident>> GetIncidentsAsync(uint limit, uint offset, DateTime? since = null, TIncidentFilterDocumentPayload? filterPayload = null, IEnumerable<string>? statuses = null, Dictionary<string, string>? additionalProperties = null);
}
