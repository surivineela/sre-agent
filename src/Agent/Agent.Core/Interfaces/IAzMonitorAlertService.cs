// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Services;
using Azure.ResourceManager.AlertsManagement.Models;

namespace Agent.Core.Interfaces;
public interface IAzMonitorAlertService
{
    Task<IEnumerable<AlertItem>> PollNewAlertsBySubscriptionId(string subscriptionId, int scanWindowInMins = 1);

    Task<bool> UpdateAlertStatus(string alertId, ServiceAlertState alertState);

    Task<bool> AcknowledgeAlert(string alertId);

    Task<bool> ResolveAlert(string alertId);
}
