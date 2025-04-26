// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Azure.ResourceManager.AlertsManagement.Models;

namespace Agent.Runtime.Services;
public interface IAzMonitorAlertService
{
    Task<IEnumerable<AlertItem>> PollNewAlertsBySubscriptionId(string subscriptionId, int scanWindowInMins = 1);

    Task<bool> UpdateAlertStatus(string alertId, ServiceAlertState alertState);

    Task<bool> AcknowledgeAlert(string alertId);
}
