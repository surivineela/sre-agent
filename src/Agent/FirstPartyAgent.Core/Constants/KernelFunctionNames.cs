// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace FirstPartyAgent.Constants
{
    public static class KernelFunctionNames
    {

        public static class Jobs
        {
            public const string GetJobDefinition = "get_job_definition";
            public const string GetJobExecutionJson = "get_job_execution_json";
            public const string GetJobExecutionEvents = "get_job_execution_events";
            public const string GetAllJobExecutionsErrorEvents = "get_all_job_executions_error_events";
            public const string GetAllJobExecutionsFinalStatus = "get_all_job_executions_final_status";
            public const string GetJobExecutionEventsContainer = "get_job_execution_events_container";
            public const string GetKedaEventsForJobScaledJobs = "get_keda_events_for_job_scaled_jobs";
            public const string GetLegionVKEventsForJobsRunningConsumptionV2 = "get_legion_vk_events_for_jobs_running_consumption_v2";
        }

        public static class ACA
        {
            public const string GetIssueInvestigationTimeRange = "get_issue_investigation_time_range";
            public const string GetInitialInvestigationSummaryReport = "get_initial_investigation_summary_report";
            public const string SubmitAgentFeedback = "submit_agent_feedback";
            public const string GetManagedEnvironmentInformation = "get_managed_environment_info";
            public const string GetASIPageForManagedCluster = "get_managed_cluster_asi_page";
            public const string GetASIPageForManagedClusterForApp = "get_managed_cluster_asi_page_for_app";
            public const string GetASIPageForManagedEnvironment = "get_managed_environment_asi_page";
            public const string GetManagedClusterEnvironmentResourceId = "get_managed_cluster_environment_resource_id";
            public const string GetManagedEnvironmentProvisioningStatus = "get_managed_environment_provisioning_status";
            public const string GetManagedEnvironmentAdminEvents = "get_managed_environment_admin_events";
            public const string GetManagedEnvironmentOperationErrors = "get_managed_environment_operation_errors";
            public const string GetHealthProbeFailures = "get_health_probe_failures";
            public const string GetHealthProbeSettings = "get_health_probe_settings";
            public const string GetNodeAvailabilityFailures = "get_node_availability_failures";
            public const string GetSubscriptionDetail = "get_subscription_detail";
            public const string GetSubscriptionUsage = "get_subscription_usage";
            public const string GetSubscriptionQuota = "get_subscription_quota";
            public const string SetSubscriptionQuota = "set_subscription_quota";
            public const string ValidateQuotaRequest = "validate_quota_request";
            public const string GetContainerAppEnvironmentQuota = "get_container_app_environment_quota";
            public const string SetContainerAppEnvironmentQuota = "set_container_app_environment_quota";
            public const string GetContainerAppEnvironmentQuotaOperationResult = "get_container_app_environment_quota_operation_result";
            public const string CallKustoQuery = "call_kusto_function";
            public const string ListRevisions = "list_revisions";
            public const string SearchAzureContainerAppsDocumentation = "search_design_docs";
            public const string GetRevisionTrafficWithReplicaCount = "get_revision_traffic_with_replica_count";
            public const string GetActiveRevisionSessions = "get_active_revision_sessions";
            public const string GetHpaHeartbeatMetrics = "get_hpa_heartbeat_metrics";
            public const string GetRevisionSpecChanges = "get_revision_spec_changes";
            public const string GetArmOperations = "get_arm_operations";
            public const string GetEventProcessorEventsWithoutReplica = "get_event_processor_events_without_replica";
            public const string GetPodHeartbeatStatus = "get_pod_heartbeat_status";
            public const string GetInternalEventProcessorEventsForPod = "get_internal_event_processor_events_for_pod";
            public const string GetLegionErrors = "get_legion_errors";
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
            public const string GetEnvoyAccessRequestCountTimeSeries = "get_envoy_access_request_count_time_series";
            public const string GetEnvoyAccessLogs = "get_envoy_access_logs";
            public const string GetEnvoyPodStatus = "get_envoy_pod_status";
            public const string GetContainerAppStatus = "get_container_app_status";
            public const string GetContainerAppPodStatus = "get_container_app_pod_status";
            public const string GetContainerAppAdminEvents = "get_container_app_admin_events";
            public const string GetASIPageForRevision = "get_asi_page_for_revision";
            public const string GetSessionPoolInfo = "get_session_pool_info";
            public const string GetChangesInSessionPool = "get_changes_in_session_pool";
            public const string GetSessionPodLogs = "get_session_pod_logs";
            public const string GetSessionPoolCreateOrUpdateLogs = "get_session_pool_create_or_update_logs";
            public const string GetCodeInterpreterSessionExecutionEventLogs = "get_code_interpreter_session_execution_event_logs";
            public const string GetCustomContainerSessionActivatorLogs = "get_custom_container_session_activator_logs";
            public const string GetMetricsMdmCount = "get_metrics_mdm_count";
            public const string GetMdmPodHeartbeatMissedTimes = "get_mdm_pod_heartbeat_missed_times";
            public const string GetBillingPodLeaderElection = "get_billing_pod_leader_election";
            public const string GetMissedMdmMetricTimes = "get_missed_mdm_metric_times";
            public const string GetAksClusterCcpNamespace = "get_aks_cluster_ccpNamespace";
            public const string GetLogConfiguration = "get_log_configuration";
            public const string GetEventProcessorErrors = "get_event_processor_errors";
            public const string GetEventProcessorLeaderElectionEvents = "get_event_processor_leader_election_events";
            public const string GetSystemComponentErrorEvents = "get_system_component_error_events";
            public const string GetAppsAndjobsVolumeForEnv = "get_apps_and_jobs_volume_for_env";
            public const string GetEventProcessorPods = "get_event_processor_pods";
            public const string GetLogProcessorPods = "get_log_processor_pods";
            public const string GetEventProcessorPodStatus = "get_event_processor_pod_status";
            public const string GetLogProcessorPodStatus = "get_log_processor_pod_status";
            public const string GetContainerAppWorkloadProfile = "get_container_app_workload_profile";
            public const string GetContainerAppInfraLayer = "get_container_app_infra_layer";
            public const string GetVKPodLeaderElection = "get_vk_pod_leader_election";
            public const string GetAKSKubeletRuntimeErrors = "get_aks_kubelet_runtime_errors";
            public const string GetInputPressureOnLogProcessor = "get_input_pressure_on_log_processor";
            public const string GetMemoryPressureOnFluentbit = "get_memory_pressure_on_fluentbit";
            public const string GetFluentbitOutputCount = "get_fluentbit_output_count";
            public const string GetFluentbitBufferPressure = "get_fluentbit_buffer_pressure";
            public const string GetFluentbitOutputErrors = "get_fluentbit_output_errors";
            public const string GetK4appsHelmChartUpgradeTimes = "get_k4apps_helm_chart_upgrade_times";
            public const string GetAksNodeImageUpgradeTimes = "get_aks_node_image_upgrade_times";
            public const string GetLegionHostRoleOSUpgradeTimes = "get_legion_host_role_os_upgrade_times";
            public const string GenerateRevisionCustomerIssuesDashboardLink = "generate_revision_customer_issues_dashboard_link";
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
            public const string LookupRelatedGitHubIssues = "LookupRelatedGitHubIssues";
            public const string GetTsgContent = "GetTsgContent";
        }
    }
}
