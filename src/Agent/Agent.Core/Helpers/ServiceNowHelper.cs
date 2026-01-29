// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------


using Agent.Core.Models.ServiceNow;

namespace Agent.Core.Helpers;
public class ServiceNowPriorityHelper
{
    public static ServiceNowIncidentPriority GetServiceNowPriorityFromString(string priority)
    {
        return priority.ToLower() switch
        {
            "1" or "critical" or "1 - critical" => ServiceNowIncidentPriority.Critical,
            "2" or "high" or "2 - high" => ServiceNowIncidentPriority.High,
            "3" or "moderate" or "medium" or "3 - moderate" => ServiceNowIncidentPriority.Moderate,
            "4" or "low" or "4 - low" => ServiceNowIncidentPriority.Low,
            "5" or "planning" or "5 - planning" => ServiceNowIncidentPriority.Planning,
            _ => ServiceNowIncidentPriority.Low
        };
    }

    public static string[] GetPriorityVariations(ServiceNowIncidentPriority priority)
    {
        return priority switch
        {
            ServiceNowIncidentPriority.Critical => new[] { "critical", "1 - critical", "1" },
            ServiceNowIncidentPriority.High => new[] { "high", "2 - high", "2" },
            ServiceNowIncidentPriority.Moderate => new[] { "moderate", "medium", "3 - moderate", "3" },
            ServiceNowIncidentPriority.Low => new[] { "low", "4 - low", "4" },
            ServiceNowIncidentPriority.Planning => new[] { "planning", "5 - planning", "5" },
            _ => new[] { "low", "4" }
        };
    }

    public static string[] NormalizePriorityForFiltering(IEnumerable<string> priorities)
    {
        var normalizedPriorities = new List<string>();
        foreach (var priority in priorities)
        {
            var serviceNowPriority = GetServiceNowPriorityFromString(priority);
            var variations = GetPriorityVariations(serviceNowPriority);
            normalizedPriorities.AddRange(variations);
        }
        return normalizedPriorities.Distinct().ToArray();
    }
}

public class ServiceNowStatusHelper
{
    public static ServiceNowIncidentStatus GetServiceNowStatusFromString(string status)
    {
        return status.ToLower() switch
        {
            "1" or "new" => ServiceNowIncidentStatus.New,
            "2" or "active" or "in progress" or "work in progress" => ServiceNowIncidentStatus.InProgress,
            "3" or "awaiting problem" => ServiceNowIncidentStatus.AwaitingProblem,
            "4" or "awaiting user info" or "on hold" => ServiceNowIncidentStatus.OnHold,
            "5" or "awaiting evidence" => ServiceNowIncidentStatus.AwaitingEvidence,
            "6" or "resolved" => ServiceNowIncidentStatus.Resolved,
            "7" or "closed" => ServiceNowIncidentStatus.Closed,
            "8" or "cancelled" or "canceled" => ServiceNowIncidentStatus.Cancelled,
            _ => ServiceNowIncidentStatus.New
        };
    }

    public static string[] GetStatusVariations(ServiceNowIncidentStatus status)
    {
        return status switch
        {
            ServiceNowIncidentStatus.New => new[] { "new", "1" },
            ServiceNowIncidentStatus.InProgress => new[] { "active", "in progress", "work in progress", "2" },
            ServiceNowIncidentStatus.AwaitingProblem => new[] { "awaiting problem", "3" },
            ServiceNowIncidentStatus.OnHold => new[] { "awaiting user info", "on hold", "4" },
            ServiceNowIncidentStatus.AwaitingEvidence => new[] { "awaiting evidence", "5" },
            ServiceNowIncidentStatus.Resolved => new[] { "resolved", "6" },
            ServiceNowIncidentStatus.Closed => new[] { "closed", "7" },
            ServiceNowIncidentStatus.Cancelled => new[] { "cancelled", "canceled", "8" },
            _ => new[] { "new", "1" }
        };
    }

    public static string[] NormalizeStatusesForFiltering(IEnumerable<string> statuses)
    {
        var normalizedStatuses = new List<string>();

        foreach (var status in statuses)
        {
            var serviceNowStatus = GetServiceNowStatusFromString(status);
            var variations = GetStatusVariations(serviceNowStatus);
            normalizedStatuses.AddRange(variations);
        }

        return normalizedStatuses.Distinct().ToArray();
    }
}
