// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace FirstPartyAgent.Constants
{
    public static class KernelFunctionNames
    {
        public static class Kusto
        {
            public const string ExecuteKustoQuery = "execute_kusto_query";
            public const string ExecuteFunction = "execute_kusto_user_defined_function";
            public const string ListKustoFunctions = "list_kusto_user_defined_functions";
            public const string CreateAgentChatMessageForKustoQuery = "create_agent_chat_message_for_kusto_query";
        }

        public static class Icm
        {
            public const string GetIncidentDetails = "get_icm_incident_details";

            public const string IcmGetIncidentInfo = "icm_get_incident_info";
            public const string IcmGetIncidentsByTeam = "icm_get_incidents_by_team";
            public const string IcmMitigateIncident = "icm_mitigate_incident";
            public const string IcmResolveIncident = "icm_resolve_incident";
            public const string IcmAddTag = "icm_add_tag";
            public const string IcmGetDiscussionEntries = "icm_get_discussion_entries";
            public const string IcmAddDiscussionEntry = "icm_add_discussion_entry";
        }

        public static class AzureSearch
        {
            public const string LookupRelatedGitHubIssues = "LookupRelatedGitHubIssues";
            public const string GetTsgContent = "GetTsgContent";
        }
    }
}
