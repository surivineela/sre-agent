// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace FirstPartyAgent.Constants
{
    public static class KernelFunctionNames
    {
        public static class ACA
        {
            public const string GetIssueInvestigationTimeRange = "get_issue_investigation_time_range";
            public const string GetInitialInvestigationSummaryReport = "get_initial_investigation_summary_report";
            public const string SubmitAgentFeedback = "submit_agent_feedback";
            public const string GetManagedEnvironmentInformation = "get_managed_environment_info";
            public const string GetASIPageForManagedCluster = "get_managed_cluster_asi_page";
            public const string GetASIPageForManagedEnvironment = "get_managed_environment_asi_page";
            public const string GetHealthProbeFailures = "get_health_probe_failures";
            public const string GetHealthProbeSettings = "get_health_probe_settings";
            public const string GetNodeAvailabilityFailures = "get_node_availability_failures";
            public const string GetSubscriptionDetail = "get_subscription_detail";
            public const string GetSubscriptionUsage = "get_subscription_usage";
            public const string SetSubscriptionQuota = "set_subscription_quota";
            public const string ValidateQuotaRequest = "validate_quota_request";
            public const string SetContainerAppEnvironmentQuota = "set_container_app_environment_quota";
            public const string GetContainerAppEnvironmentQuotaOperationResult = "get_container_app_environment_quota_operation_result";
            public const string CallKustoQuery = "call_kusto_function";
            public const string ListRevisions = "list_revisions";
            public const string GetRevisionTrafficWithReplicaCount = "get_revision_traffic_with_replica_count";
            public const string GetActiveRevisionSessions = "get_active_revision_sessions";
            public const string GetHpaHeartbeatMetrics = "get_hpa_heartbeat_metrics";
            public const string GetRevisionSpecChanges = "get_revision_spec_changes";
            public const string GetEventProcessorEventsWithoutReplica = "get_event_processor_events_without_replica";
            public const string GetPodHeartbeatStatus = "get_pod_heartbeat_status";
            public const string GetInternalEventProcessorEventsForPod = "get_internal_event_processor_events_for_pod";
            public const string GetReplicaCount = "get_replica_count";
            public const string ContainerAppRevisionStatus = "get_container_app_revision_status";
            public const string ListKustoFunctions = "list_kusto_functions";
            public const string GetHttpScalerEventsForContainerApp = "get_http_scaler_events_for_conatinerapp";
            public const string GetKedaOperatorEventsForContainerApp = "get_keda_operator_events_for_conatinerapp";
            public const string CheckIfCustomDNSConfigured = "CheckIfCustomDNSConfigured";
            public const string GetCustomDNSServers = "GetCustomDNSServers";
            public const string GetCorednsPodFailureEvents = "GetCorednsPodFailureEvents";
            public const string GetSwiftBootstrapAgentPodFailureEvents = "GetSwiftBootstrapAgentPodFailureEvents";
            public const string GetSwiftBootstrapAgentPodHealthStatus = "GetSwiftBootstrapAgentPodHealthStatus";
            public const string GetDNSConfigUpdateStatus = "GetDNSConfigUpdateStatus";
            public const string CheckIfDNSServerFailedToResolveDot = "CheckIfDNSServerFailedToResolveDot";
            public const string GetContainerAppManagedClusterName = "get_container_app_managed_cluster_name";
            public const string GetSwiftNetworkingEvents = "get_swift_networking_events";
            public const string GetEnvoyPodLogs = "get_envoy_pod_logs";
            public const string GetEnvoyControllerLogs = "get_envoy_controller_logs";
            public const string GetEnvoyAccessLogs = "get_envoy_access_logs";
            public const string GetEnvoyPodStatus = "get_envoy_pod_status";
            public const string GetCustomerAppPodStatus = "get_customer_application_pod_status";
            public const string GetContainerAppStatus = "get_container_app_status";
            public const string GetContainerAppAdminEvents = "get_container_app_admin_events";
            public const string GetASIPageForRevision = "get_asi_page_for_revision";
        }

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
            public const string IcmGetDisscussionEntries = "icm_get_discussion_entries";
            public const string IcmAddDiscussionEntry = "icm_add_discussion_entry";
        }

        public static class AzureSearch
        {
            public const string LookupRelatedGitHubIssues = "lookup_related_github_issues";
        }
    }
}
