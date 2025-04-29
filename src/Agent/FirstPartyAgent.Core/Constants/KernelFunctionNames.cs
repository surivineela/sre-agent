// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace FirstPartyAgent.Constants
{
    public static class KernelFunctionNames
    {
        public static class ACA
        {
            public const string GetSubscriptionDetail = "get_subscription_detail";
            public const string GetSubscriptionUsage = "get_subscription_usage";
            public const string SetSubscriptionQuota = "set_subscription_quota";
            public const string ValidateQuotaRequest = "validate_quota_request";
        }

        public static class Kusto
        {
            public const string ExecuteKustoQuery = "execute_kusto_query";
        }

        public static class Icm
        {
            public const string GetIncidentDetails = "get_icm_incident_details";

            public const string IcmGetIncidentInfo = "icm_get_incident_info";
            public const string IcmGetIncidentsByTeam = "icm_get_incidents_by_team";
            public const string IcmMitigateIncident = "icm_mitigate_incident";
            public const string IcmResolveIncident = "icm_resolve_incident";
            public const string IcmAddTag = "icm_add_tag";
            public const string IcmGetDisscussionEntries = "icm_get_discussion_entries";
            public const string IcmAddDiscussionEntry = "icm_add_discussion_entry";
        }

        public static class AzureSearch
        {
            public const string LookupRelatedGitHubIssues = "lookup_related_github_issues";
        }
    }
}
