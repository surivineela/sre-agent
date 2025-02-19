// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using FirstPartyAgent.Models;

namespace FirstPartyAgent.ACA.Web.Services;

public interface ITaskStorageService
{
    Task SaveTaskAsync(QuotaIncidentState incident);

    // Get tasks from the storage for further processing.
    // Note it doesn't remove the tasks from the storage, but only marks them invisible for 30 seconds.
    Task<Dictionary<string, QuotaIncidentState>> GetAllTasksAsync();

    Task RemoveTaskAsync(string incidentId);

    Task UpdateTaskAsync(QuotaIncidentState incident);
}