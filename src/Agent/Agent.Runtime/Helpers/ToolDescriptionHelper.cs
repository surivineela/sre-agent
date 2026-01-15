// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.Helpers;

public static class ToolDescriptionHelper
{
    /// <summary>
    /// Default safe description used when function names might be exposed or when no specific mapping exists
    /// </summary>
    public const string DefaultSafeDescription = "Working...";

    /// <summary>
    /// Gets a user-friendly description for a function call, using the arguments to provide more context.
    /// </summary>
    public static string GetUserDescriptionForFunctionCall(FunctionCallContent functionCall)
    {
        var arguments = functionCall.Arguments;

        return functionCall.Name switch
        {
            // Existing cases
            "GetIncidentDetails" => FormatGetIncidentDetails(arguments),
            "GetAlertDetails" => "Fetching details of the alert...",
            "GetThreadDetails" => "Fetching details of the thread...",
            "GetTopDiscussionEntries" => FormatGetTopDiscussionEntries(arguments),
            "GetIncidentAlertDetails" => FormatGetIncidentAlertDetails(arguments),
            "SearchIncidents" => FormatSearchIncidents(arguments),
            "GetSimilarIncidents" => FormatGetSimilarIncidents(arguments),

            // Kusto MCP tools
            "kusto-mcp_kusto_cluster_get" => FormatKustoClusterGet(arguments),
            "kusto-mcp_kusto_cluster_list" => "Listing available Kusto clusters...",
            "kusto-mcp_kusto_database_list" => FormatKustoDatabaseList(arguments),
            "kusto-mcp_kusto_query" => FormatKustoQuery(arguments),
            "kusto-mcp_kusto_sample" => FormatKustoSample(arguments),
            "kusto-mcp_kusto_table_list" => FormatKustoTableList(arguments),
            "kusto-mcp_kusto_table_schema" => FormatKustoTableSchema(arguments),
            "mcp_azure_mcp_kusto_cluster_get" => FormatKustoClusterGet(arguments),
            "mcp_azure_mcp_kusto_cluster_list" => "Listing available Kusto clusters...",
            "mcp_azure_mcp_kusto_database_list" => FormatKustoDatabaseList(arguments),
            "mcp_azure_mcp_kusto_query" => FormatKustoQuery(arguments),
            "mcp_azure_mcp_kusto_sample" => FormatKustoSample(arguments),
            "mcp_azure_mcp_kusto_table_list" => FormatKustoTableList(arguments),
            "mcp_azure_mcp_kusto_table_schema" => FormatKustoTableSchema(arguments),

            // Azure DevOps MCP tools
            "ado-mcp_repo_create_pull_request" => FormatAdoPullRequest(arguments),
            "mcp_azure-devops-_repo_create_pull_request" => FormatAdoPullRequest(arguments),

            // Control Flow functions
            "Wait" => "Waiting for a specified duration...",
            "MarkPlanComplete" => "Marking the current plan as complete...",
            "NotifyUser" => "Sending you an update...",
            "AskUserForInput" => "Asking for your input...",

            // ARM Plugin functions
            "GetTlsSettings" => "Checking TLS security settings for your resources...",
            "CheckIfResourceExists" => "Verifying if the resource exists...",
            "SetMinimumTlsVersion" => "Updating TLS security settings...",
            "RestartWebApp" => "Restarting your web application...",
            "GetArmResourceAsJson" => "Fetching detailed resource information...",
            "PowerOnVirtualMachine" => "Starting your virtual machine...",
            "GetVirtualMachineBootDiagnostics" => "Retrieving boot diagnostic information...",
            "CheckConnectivityToAzureWebJobsStorage" => "Testing storage connectivity via connection string...",
            "CheckTcpConnectivity" => "Testing network connectivity...",
            "CheckDnsResolution" => "Testing DNS resolution...",
            "GetAppSetting" => "Retrieving application settings...",
            "ListKeysAndUpdateAppSettingsAsync" => "Updating storage connection settings...",
            "ConfigureAppSettingsForManagedIdentityStorage" => "Configuring application settings to use managed identity to storage...",
            "UpdateAppSettingsAsync" => "Updating application configuration...",
            "RunAzCliReadCommandsAsync" => "Reading Azure resource information...",
            "RunAzCliWriteCommandsAsync" => "Making changes to Azure resources...",
            "GetAzCliHelpAsync" => "Getting Azure CLI command help...",
            "RunAzCliWriteCommands" => "Making changes to Azure resources...",
            "RunAzCliReadCommands" => "Reading Azure resource information...",
            "GetAzCliHelp" => "Getting Azure CLI command help...",

            // Function App plugins
            "ListFunctionAppsAsync" => "Finding your Function Apps...",
            "GetFunctionAppInfoAsync" => "Fetching details about your Function App...",
            "GetFunctionAppExecutionFailures" => "Analyzing Function App execution failures...",
            "GetFunctionAppCallStacks" => "Retrieving Function App call stack information...",
            "GetFailedFunctionInvocations" => "Analyzing failed function invocations...",
            "GetTop3ExceptionsPerFunction" => "Finding the most common exceptions...",
            "GetTop3ExceptionsWithStackTraces" => "Finding exceptions with detailed stack traces...",
            "GetHostRuntimeErrorEvents" => "Checking for runtime errors...",
            "IsFunctionApp" => "Verifying if the resource is a Function App...",
            "HasHostRuntimeErrors" => "Checking for Function App runtime issues...",
            "TriggerFunctionAppSync" => "Refreshing Function App configuration...",
            "GetFunctionAppConfigurationChecks" => "Analyzing Function App configuration...",
            "GetFunctionAppDeploymentChecks" => "Checking deployment information...",
            "GetFunctionAppDeploymentHistory" => "Retrieving deployment history...",
            "GetFunctionAppDeploymentFailureAnalysis" => "Analyzing deployment failure patterns and root causes...",
            "GetFunctionAppSlotSwapHistory" => "Retrieving Function App slot swap history...",
            "UpdateWebsiteRunFromPackageAsync" => "Updating Function App deployment package source...",
            "UpdateWebsiteRunFromPackage" => "Updating Function App deployment package source...",
            "ListStorageBlobsAsync" => "Listing files in storage container...",
            "ListStorageBlobs" => "Listing files in storage container...",
            "VerifyFilesInBlobContainerAsync" => "Verifying files in storage container...",
            "VerifyFilesInBlobContainer" => "Verifying files in storage container...",
            "GetFailedRequestsPerFunction" => "Analyzing failed requests per function...",

            // Role Assignment functions
            "GetRoleAssignments" => "Checking access permissions...",
            "AddRoleAssignment" => "Granting access permissions...",
            "RemoveRoleAssignment" => "Removing access permissions...",
            "CheckRoleAssignment" => "Verifying access permissions...",
            "GetRoleDetailsFromNameAsync" => "Getting role permission details...",

            // Graph DB functions
            "Query" => "Searching the knowledge graph...",
            "FindAllNetworkConnectedResources" => "Finding network-connected resources...",
            "GetApplicationComponentsSummary" => "Getting application components overview...",
            "VisualizeApplicationComponents" => "Creating application architecture diagram...",
            "DiscoverApplications" => "Discovering applications in your subscription...",
            "AddSourceCodeNodeToContainerAppNode" => "Linking source code repository...",
            "AddIgnoreTagToResource" => "Adding ignore tag to resource...",
            "GetContainerAppsWithNodesWithoutSourceCodeNodes" => "Finding Container Apps without source code links...",
            "UpdateRepoNodeWithLastScanTime" => "Updating repository scan timestamp...",
            "GetManagedResourcesInfoAsync" => "Getting managed resources inventory...",
            "SearchResource" => "Searching for resources...",
            "SearchResourceByName" => "Finding resources by name...",
            "GetResourceCount" => "Counting resources...",
            "ListSubscriptions" => "Listing available subscriptions...",
            "ListResourceGroups" => "Listing resource groups...",
            "GetActivityLogsSummary" => "Analyzing recent activity logs...",
            "ListResourcesByType" => "Listing resources by type...",
            "GetKnowledgeGraphResourceUsageDashboard" => "Getting resource usage dashboard...",
            "VisualizeAKSMicroserviceTopology" => "Creating Kubernetes architecture diagram...",
            "GetResourceBasicProperties" => "Getting basic resource information...",
            "GetResourceDetailedProperties" => "Fetching detailed resource properties...",
            "GetResourceIdForResourceName" => "Finding resource ID by name...",

            // ACA Kusto functions
            "ExecuteFunction" => "Running Kusto query function...",
            "ListKustoFunctions" => "Listing available Kusto functions...",

            // App Code Analysis functions
            "GetCallStackForApp" => "Retrieving application call stack...",
            "WaitInMilliSeconds" => "Waiting for specified time...",
            "GetSummaryOfExceptions" => "Analyzing application exceptions...",
            "GetStackTraceOfLastException" => "Getting recent exception details...",
            "GetStackTraceOfMostCommonException" => "Getting common exception details...",
            "PerformDeploymentSwapForApp" => "Swapping application deployment slots...",
            "GetDeploymentActivity" => "Checking deployment activity...",
            "GetAppConsoleLogs" => "Retrieving application logs...",
            "GetWebAppDownAnalysisLink" => "Getting web app analysis link...",

            // Chart/Visualization functions
            "PlotTimeSeriesData" => "Creating time series chart...",
            "PlotBarChartAsync" => "Creating bar chart...",
            "PlotPieChartAsync" => "Creating pie chart...",
            "PlotScatterAsync" => "Creating scatter plot...",
            "PlotHeatmapAsync" => "Creating heatmap visualization...",
            "PlotAreaChartWithCorrelationAsync" => "Creating area chart with correlation...",
            "GetPieChartBase64Image" => "Generating pie chart image...",

            // Additional Chart/Visualization functions (missing from current list)
            "PlotPieChart" => "Creating pie chart visualization...",
            "plot_pie_chart" => "Creating pie chart visualization...",
            "PlotBarChart" => "Creating bar chart visualization...",
            "plot_bar_chart_async" => "Creating bar chart visualization...",
            "PlotScatter" => "Creating scatter plot visualization...",
            "plot_scatter" => "Creating scatter plot visualization...",
            "plot_heatmap" => "Creating heatmap visualization...",
            "plot_time_series_data_in_teams_chat" => "Creating time series chart for Teams...",
            "plot_time_series_data_in_icm" => "Creating time series chart for ICM incident...",

            // Metrics functions
            "GetFunctionAppRequestAvailability" => "Checking Function App availability metrics...",

            // Helper Agent functions
            "StartDiagnosisAgent" => "Starting resource diagnosis...",

            // ACA specific functions (from KernelFunctionNames)
            "get_job_definition" => "Getting Container App Job definition...",
            "get_job_execution_json" => "Getting job execution details...",
            "get_job_execution_events" => "Getting job execution events...",
            "get_all_job_executions_error_events" => "Getting all job execution errors...",
            "get_all_job_executions_final_status" => "Getting job execution status...",
            "get_job_execution_events_container" => "Getting container job events...",
            "get_keda_events_for_job_scaled_jobs" => "Getting KEDA scaling events...",
            "get_legion_vk_events_for_jobs_running_consumption_v2" => "Getting Legion VK events...",
            "get_issue_investigation_time_range" => "Getting investigation time range...",
            "get_initial_investigation_summary_report" => "Getting investigation summary...",
            "submit_agent_feedback" => "Submitting agent feedback...",
            "get_managed_environment_info" => "Getting Container App environment information...",
            "call_kusto_function" => "Running Kusto analytics query...",
            "list_revisions" => "Listing Container App revisions...",
            "search_design_docs" => "Searching documentation...",

            // Kubectl functions (from KubePlugin)
            "apply" => "Applying Kubernetes configuration...",
            "create" => "Creating Kubernetes resource...",
            "patch" => "Updating Kubernetes resource...",
            "replace" => "Replacing Kubernetes resource...",
            "scale" => "Scaling Kubernetes resource...",
            "label" => "Adding labels to Kubernetes resource...",
            "annotate" => "Adding annotations to Kubernetes resource...",
            "rollout" => "Managing Kubernetes rollout...",

            // Container Apps Plugin functions
            "GetContainerAppInfo" => "Getting detailed Container App information...",
            "get_container_app" => "Getting detailed Container App information...",
            "ListRevisions" => "Listing Container App revisions...",
            "ListRevisionsAsync" => "Listing Container App revisions...",
            "GetLatestRevisionAsync" => "Getting latest Container App revision...",
            "get_latest_containerapp_revision" => "Getting latest Container App revision...",
            "ListContainerApps" => "Finding your Container Apps...",
            "list_container_apps" => "Finding your Container Apps...",
            "RestartContainerApp" => "Restarting Container App revision...",
            "restart_containerapp_revision" => "Restarting Container App revision...",
            "GetContainerAppRequestMetrics" => "Getting Container App request metrics...",
            "get_containerapp_request_count_metrics" => "Getting Container App request metrics...",
            "GetContainerAppMemoryMetrics" => "Getting Container App memory usage...",
            "get_containerapp_memory_metrics" => "Getting Container App memory usage...",
            "GetContainerAppCpuMetrics" => "Getting Container App CPU usage...",
            "get_containerapp_cpu_metrics" => "Getting Container App CPU usage...",
            "get_containerapp_memory_analysis_dotnet" => "Analyzing .NET Container App memory usage...",
            "GetAllNSGRulesForContainerAppAsync" => "Getting network security group rules...",
            "get_containerapp_nsg_rules" => "Getting network security group rules...",
            "ScaleContainerApp" => "Scaling Container App resources...",
            "ModifyContainerAppScaleRule" => "Modifying Container App scaling rules...",
            "GetRevisionLogsAsync" => "Getting Container App revision logs...",
            "GetContainerAppLogsAsync" => "Getting Container App logs...",
            "UpdateTargetPort" => "Updating Container App target port...",
            "ListAvailableScalers" => "Listing available scaling options...",
            "GetScalerDetails" => "Getting scaling configuration details...",
            "GetImageReferenceFromResourceId" => "Getting container image reference...",
            "get_image_reference" => "Getting container image reference...",
            "VerifyExternalRegistry" => "Verifying container registry connectivity...",
            "verify_external_registry" => "Verifying container registry connectivity...",
            "RollbackToLastKnownWorkingRevision" => "Rolling back to previous working version...",
            "rollback_to_last_working_image" => "Rolling back to previous working version...",
            "UpdateContainerImage" => "Updating container image...",
            "update_container_image" => "Updating container image...",
            "ValidateContainerAppHealth" => "Validating Container App health...",
            "validate_containerapp_health" => "Validating Container App health...",
            "GetDeploymentTimes" => "Getting Container App deployment times...",
            "get_containerapp_deployment_times" => "Getting Container App deployment times...",

            // Container Apps Jobs Plugin functions
            "GetJobDefinition" => "Getting Container App Job definition...",
            "GetJobExecutionFinalStatus" => "Getting job execution status...",
            "GetJobExecutionEvents" => "Getting job execution events...",
            "GetAllJobExecutionsErrorEvents" => "Getting all job execution errors...",
            "GetAllJobExecutionsFinalStatus" => "Getting all job execution statuses...",
            "GetKedaEventsForJobScaledJobs" => "Getting KEDA scaling events for jobs...",
            "GetLegionVKEventsForJobsRunningConsumptionV2" => "Getting Legion VK events for consumption jobs...",
            "GetLegionSystemLogsForJobExecutionErrors" => "Getting Legion system logs for job errors...",
            "GetASIPageForContainerAppJob" => "Getting ASI page link for Container App Job...",

            // Container Apps Revision Plugin functions
            "ListRevisionsForRCA" => "Listing revisions for analysis...",
            "GetHttpScalerEventsForContainerApp" => "Getting HTTP scaler events...",
            "GetHttpScalerMetricsForContainerApp" => "Getting HTTP scaler metrics...",
            "GetKedaOperatorEventsForContainerApp" => "Getting KEDA operator events...",
            "GetASIPageForRevision" => "Getting ASI page link for revision...",
            "GetRevisionTrafficWithReplicaCount" => "Getting revision traffic and replica data...",
            "ContainerAppRevisionStatus" => "Getting revision status information...",
            "GetReplicaCount" => "Getting revision replica count...",
            "GetActiveRevisions" => "Getting active revisions...",
            "GetHpaHeartbeatMetrics" => "Getting horizontal pod autoscaler metrics...",
            "GetRevisionSpecChanges" => "Getting revision specification changes...",
            "GetArmOperations" => "Getting ARM operations for Container App...",
            "GetEventProcessorEventsWithoutReplica" => "Getting events without replica association...",
            "GetPodHeartbeatStatus" => "Getting pod heartbeat status...",
            "GetInternalEventProcessorEventsForPod" => "Getting internal pod events...",
            "GetLegionErrors" => "Getting Legion error information...",
            "GetHealthProbeFailures" => "Getting health probe failure information...",
            "GetHealthProbeSettings" => "Getting health probe configuration...",
            "GetNodeAvailabilityFailures" => "Getting node availability failure information...",
            "GenerateRevisionCustomerIssuesDashboardLink" => "Generating customer issues dashboard link...",

            // Handoff functions (transfer_to_* pattern from agent configurations)
            "transfer_to_resource_discovery_agent" => "Discovering your resources...",
            "transfer_to_metrics_and_chart_visualization_agent" => "Analyzing metrics and creating visualizations...",
            "transfer_to_aks_general_agent" => "Analyzing Kubernetes cluster configuration...",
            "transfer_to_github_issue_agent" => "Managing GitHub issues for incident tracking...",
            "transfer_to_azure_cli_command_executor_agent" => "Executing Azure CLI commands...",
            "transfer_to_code_execution_runtime_agent" => "Running analysis using Python...",
            "HandoffBack" => "Continuing with the investigation...",

            // GitHub Issue Plugin functions (from GithubIssueAgent.yaml)
            nameof(GitHubIssuePlugin.CreateGithubIssue) => "Creating GitHub issue...",
            nameof(GitHubIssuePlugin.FetchGithubIssue) => "Fetching GitHub issue details...",
            nameof(GitHubIssuePlugin.FindConnectedRepo) => "Finding connected GitHub repository...",
            nameof(GitHubIssuePlugin.CreateGithubIssueComment) => "Adding comment to GitHub issue...",
            nameof(GitHubIssuePlugin.FetchGithubSecurityDependabotAlerts) => "Fetching GitHub security alerts...",
            nameof(GitHubIssuePluginDefinition.FetchGithubIssuesLimited) => "Fetching GitHub Issues...",

            // Kubectl Plugin functions
            "RunKubectlWriteCommand" => "Executing Kubernetes write command...",
            "RunKubectlWriteCommandAsync" => "Executing Kubernetes write command...",
            "RunKubectlReadCommand" => "Reading Kubernetes resource information...",
            "RunKubectlReadCommandAsync" => "Reading Kubernetes resource information...",
            "GetKubectlHelp" => "Getting Kubernetes command help...",
            "GetKubectlHelpAsync" => "Getting Kubernetes command help...",

            // Container Apps Environment functions
            "GetContainerAppEnvironmentInfo" => "Getting Container App environment details...",
            "GetManagedEnvironmentInfo" => "Getting managed environment information...",

            // Additional ARM Plugin functions
            "GetResourceIdFromStorageServiceUri" => "Getting resource ID from storage URI...",

            // Additional Support functions (from agent plan files)
            "GetSupportProductsFromArm" => "Getting Azure support products...",
            "GetSupportProblemClassificationsForProduct" => "Getting support problem classifications...",
            "GetAzureSupportCenterDiagnosticResultsForQuestion" => "Getting diagnostic results from Azure Support Center...",

            // Additional Diagnosis functions
            "AddNewSummary" => "Adding investigation summary...",

            // Additional Function App functions (from agent plan files)
            "CollectMemoryDumpForApp" => "Collecting memory dump for analysis...",
            "ScaleUpAppServicePlanBySku" => "Scaling up App Service Plan...",
            "AutoScaleApp" => "Setting up automatic scaling...",
            "GetWebAppCpuMetrics" => "Getting web app CPU metrics...",
            "GetFunctionAppConnectivityAgent" => "Starting Function App connectivity analysis...",
            "FunctionAppConnectivityAgent" => "Starting Function App connectivity analysis...",

            // Kubernetes and AKS functions
            "KubectlGet" => "Retrieving Kubernetes resource information...",
            "KubectlDescribe" => "Getting detailed Kubernetes resource description...",
            "KubectlExplain" => "Getting Kubernetes resource schema information...",
            "KubeApiResources" => "Listing available Kubernetes API resources...",
            "GetPodLogs" => "Retrieving pod logs...",
            "GetKubeEvents" => "Getting Kubernetes cluster events...",
            "DiscoverPrometheusMetrics" => "Discovering available metrics in Prometheus...",
            "GetMetricsLabels" => "Getting available metric labels...",
            "QueryPrometheusMetrics" => "Querying Prometheus metrics...",

            // Network and Security functions
            "RemoveNSGRule" => "Removing network security group rule...",
            "AddNSGRule" => "Adding network security group rule...",
            "UpdateNSGRule" => "Updating network security group rule...",
            "NSGRulePluginDefinition" => "Managing network security group rules...",

            // Storage and Blob functions
            "CheckBlobExists" => "Checking if blob exists in storage...",
            "DownloadBlob" => "Downloading blob from storage...",
            "UploadBlob" => "Uploading blob to storage...",

            // Authentication and Identity functions
            "GetIdentityInformation" => "Getting identity and authentication details...",
            "ValidateIdentityConfiguration" => "Validating identity configuration...",
            "GetManagedIdentityDetails" => "Getting managed identity information...",

            // Monitoring and Alerting functions
            "CreateAlert" => "Creating monitoring alert...",
            "UpdateAlert" => "Updating monitoring alert...",
            "DeleteAlert" => "Deleting monitoring alert...",
            "GetAlertHistory" => "Getting alert history...",
            "CloseAzureMonitorAlert" => "Closing Azure Monitor alert...",

            // Documentation and Help functions
            "SearchDocuments" => "Searching documentation...",
            "SearchDocumentsAsync" => "Searching documentation...",
            "GetDocumentation" => "Retrieving documentation...",

            // Agent-specific tools from YAML files
            "NetworkDiagnosisTool" => "Diagnosing network connectivity issues...",
            "StartDiagnosisWorkflow" => "Starting comprehensive diagnosis workflow...",
            "GetSystemHealth" => "Checking overall system health...",
            "ValidateConfiguration" => "Validating resource configuration...",

            // Error Analysis and Debugging
            "AnalyzeErrorLogs" => "Analyzing error logs for patterns...",
            "GetErrorDetails" => "Getting detailed error information...",
            "TraceErrorFlow" => "Tracing error propagation flow...",
            "GetDiagnosticSummary" => "Getting diagnostic summary...",

            // Performance Analysis
            "GetPerformanceMetrics" => "Getting performance metrics...",
            "AnalyzePerformanceBottlenecks" => "Analyzing performance bottlenecks...",
            "GetResourceUtilization" => "Getting resource utilization metrics...",
            "MonitorPerformanceTrends" => "Monitoring performance trends...",

            // Additional Complete functions
            "Complete" => "Completing the operation...",

            // Code Interpreter Plugin tools
            "ExecutePythonCodeAsync" => "Running Python code and generating output files...",
            "GeneratePdfReportAsync" => "Creating a PDF report using Python...",
            "ListSessionFilesAsync" => "Listing files created by Python execution...",
            "GetSessionFileAsync" => "Validating a generated file for you...",

            // Container Apps Aspire Agent tools
            "CheckContainerAppWorkloadProfileExists" => "Checking if the container app environment has a workload profile which determines if it's a V1 or V2 environment...",
            "GetContainerAppEnvironmentName" => "Retrieving the environment name associated with a managed container app cluster...",
            "CheckIfAspireIsEnabled" => "Verifying if Aspire is enabled for the specified container app environment...",
            "CheckEnvoyFrontEndLogs" => "Examining Envoy controller logs for 404 errors related to Aspire endpoints...",
            "CheckAspireDashboardAccess" => "Analyzing access logs for the Aspire dashboard to check for authentication issues...",
            "CheckEnvironmentVnet" => "Verifying if the container app environment has VNET integration configured...",
            "CheckAspireAuthorizationIssues" => "Checking for authorization failures when accessing the Aspire dashboard...",
            "CheckAspireStateVerificationIssues" => "Analyzing state verification issues in the external authentication for Aspire dashboard access...",

            // APIManagement tools
            "GetAPIMErrorLogs" => "Retrieving API Management error logs...",
            "GetAPIMFailureRateByApiOperation" => "Analyzing failure rates by API operation...",
            "GetAPIMRecentFailedRequests" => "Fetching recent failed requests in API Management...",
            "GetAPIMApis" => "Listing APIs in API Management...",
            "GetAPIDetailsByName" => "Fetching details of the API by name...",
            "GetAPIOperationsByApi" => "Listing operations for the specified API...",
            "GetAPIOperationDetailedInfo" => "Fetching detailed information for the API operation...",
            "GetAPIMActivityLogs" => "Retrieving activity logs for API Management...",
            "GetAPIManagementInfo" => "Getting API Management service details...",
            "GetVNetConfigurationForApiManagement" => "Fetching virtual network configuration for API Management...",
            "GetNSGRulesForApiManagement" => "Retrieving network security group rules for API Management...",
            "GetNSGActivityLogs" => "Fetching network security group activity logs...",
            "CheckForVirtualNetworkIssues" => "Checking for virtual network configuration issues...",
            "GetPoliciesByApi" => "Retrieving policies applied to the API...",
            "GetPoliciesByOperation" => "Fetching policies applied to the API operation...",
            "GetGlobalApimPolicy" => "Retrieving global policy for API Management...",
            "APIMModifyNSGRule" => "Modifying network security group rule for API Management...",
            "APIMRemoveNSGRule" => "Removing network security group rule for API Management...",

            // AzDo Tools
            nameof(AzureDevOpsWorkItemPluginDefinition.ConnectRepositoryToResourceForAzureDevOps) => "Connecting to Azure DevOps Repository...",
            nameof(AzureDevOpsWorkItemPluginDefinition.FindConnectedRepositoryForAzureDevOps) => "Finding Connected Azure DevOps Repository...",
            nameof(AzureDevOpsWorkItemPluginDefinition.CreateAzureDevOpsWorkItem) => "Creating Azure Dev Ops Work Item...",
            nameof(AzureDevOpsWorkItemPluginDefinition.DisconnectRepositoryFromResourceForAzureDevOps) => "Disconnecting Azure Dev Ops Repository...",

            // Diagnostic Analysis Tools
            nameof(DiagnosticsPluginDefinition.GetCPUAnalysis) => "Conducting CPU Analysis...",
            nameof(DiagnosticsPluginDefinition.GetMemoryAnalysis) => "Conducting Memory Analysis...",
            nameof(DiagnosticsPluginDefinition.GetLatencyAnalysis) => "Conducting Latency Analysis...",

            nameof(RepositoryPluginDefintion.GetRepositoryType) => "Getting the Repository Type...",

            nameof(SourceCodeAnalysisAgentPluginDefinition.QuerySourceBySemanticSearch) => "Querying source code by semantic search...",
            nameof(SourceCodeAnalysisAgentPluginDefinition.CorrelateErrorsWithSourceCode) => "Correlating errors with source code...",

            // Memory Tools
            nameof(AgentMemoryPluginDefinition.SearchIncidentKnowledge) => "Searching past incident knowledge...",
            nameof(AgentMemoryPluginDefinition.SearchMemory) => "Searching knowledge base...",

            // VS Code Tools
            nameof(WorkspacePluginDefinition.ReadFile) => FormatReadFile(arguments),
            nameof(WorkspacePluginDefinition.CreateFile) => FormatCreateFile(arguments),
            nameof(WorkspacePluginDefinition.CreateDirectory) => FormatCreateDirectory(arguments),
            nameof(WorkspacePluginDefinition.ListDir) => FormatListDir(arguments),
            nameof(WorkspacePluginDefinition.ReplaceStringInFile) => FormatReplaceStringInFile(arguments),
            nameof(WorkspacePluginDefinition.MultiReplaceStringInFile) => FormatMultiReplaceStringInFile(arguments),
            nameof(WorkspacePluginDefinition.FileSearch) => FormatFileSearch(arguments),
            nameof(WorkspacePluginDefinition.GrepSearch) => FormatGrepSearch(arguments),
            nameof(WorkspacePluginDefinition.RunInTerminal) => FormatRunInTerminal(arguments),
            nameof(WorkspacePluginDefinition.TerminalLastCommand) => "Getting last terminal command...",
            nameof(WorkspacePluginDefinition.ManageTodoList) => FormatManageTodoList(arguments),
            nameof(WorkspacePluginDefinition.FetchWebpage) => FormatFetchWebpage(arguments),
            "AskUserQuestion" => "Preparing question for user input...",

            // Default case
            _ => DefaultSafeDescription
        };
    }

    #region VS Code Tool Formatters

    private static string FormatReadFile(IDictionary<string, object?>? arguments)
    {
        var filePath = GetArgument<string>(arguments, "filePath");
        if (string.IsNullOrEmpty(filePath))
        {
            return "Reading file...";
        }
        var fileName = Path.GetFileName(filePath);
        return $"Reading {fileName}...";
    }

    private static string FormatCreateFile(IDictionary<string, object?>? arguments)
    {
        var filePath = GetArgument<string>(arguments, "filePath");
        if (string.IsNullOrEmpty(filePath))
        {
            return "Creating file...";
        }
        var fileName = Path.GetFileName(filePath);
        return $"Creating {fileName}...";
    }

    private static string FormatCreateDirectory(IDictionary<string, object?>? arguments)
    {
        var dirPath = GetArgument<string>(arguments, "dirPath");
        if (string.IsNullOrEmpty(dirPath))
        {
            return "Creating directory...";
        }
        var dirName = Path.GetFileName(dirPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return $"Creating directory {dirName}...";
    }

    private static string FormatListDir(IDictionary<string, object?>? arguments)
    {
        var path = GetArgument<string>(arguments, "path");
        if (string.IsNullOrEmpty(path))
        {
            return "Listing directory...";
        }
        var dirName = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return $"Listing {dirName}...";
    }

    private static string FormatReplaceStringInFile(IDictionary<string, object?>? arguments)
    {
        var filePath = GetArgument<string>(arguments, "filePath");
        if (string.IsNullOrEmpty(filePath))
        {
            return "Editing file...";
        }
        var fileName = Path.GetFileName(filePath);
        return $"Editing {fileName}...";
    }

    private static string FormatMultiReplaceStringInFile(IDictionary<string, object?>? arguments)
    {
        var explanation = GetArgument<string>(arguments, "explanation");
        if (!string.IsNullOrEmpty(explanation))
        {
            return explanation;
        }
        return "Making multiple edits...";
    }

    private static string FormatFileSearch(IDictionary<string, object?>? arguments)
    {
        var query = GetArgument<string>(arguments, "query");
        if (string.IsNullOrEmpty(query))
        {
            return "Searching for files...";
        }
        return $"Searching for {query}...";
    }

    private static string FormatGrepSearch(IDictionary<string, object?>? arguments)
    {
        var query = GetArgument<string>(arguments, "query");
        var includePattern = GetArgument<string>(arguments, "includePattern");

        if (string.IsNullOrEmpty(query))
        {
            return "Searching in files...";
        }

        if (!string.IsNullOrEmpty(includePattern))
        {
            return $"Searching for '{query}' in {includePattern}...";
        }

        return $"Searching for '{query}'...";
    }

    private static string FormatRunInTerminal(IDictionary<string, object?>? arguments)
    {
        var explanation = GetArgument<string>(arguments, "explanation");
        if (!string.IsNullOrEmpty(explanation))
        {
            return explanation;
        }
        return "Running terminal command...";
    }

    private static string FormatManageTodoList(IDictionary<string, object?>? arguments)
    {
        var operation = GetArgument<string>(arguments, "operation");
        return operation?.ToLowerInvariant() switch
        {
            "write" => "Updating todo list...",
            "read" => "Reading todo list...",
            _ => "Managing todo list..."
        };
    }

    private static string FormatFetchWebpage(IDictionary<string, object?>? arguments)
    {
        var query = GetArgument<string>(arguments, "query");
        if (!string.IsNullOrEmpty(query))
        {
            return $"Fetching webpage for '{query}'...";
        }
        return "Fetching webpage...";
    }

    #endregion

    #region ICM Tool Formatters

    private static string FormatGetIncidentDetails(IDictionary<string, object?>? arguments)
    {
        var incidentId = GetArgument<long>(arguments, "incidentId");
        return incidentId > 0 ? $"Fetching details of incident {incidentId}..." : "Fetching details of the incident...";
    }

    private static string FormatGetTopDiscussionEntries(IDictionary<string, object?>? arguments)
    {
        var incidentId = GetArgument<long>(arguments, "incidentId");
        return incidentId > 0 ? $"Fetching top discussion entries from incident {incidentId}..." : "Fetching top discussion entries from incident...";
    }

    private static string FormatGetIncidentAlertDetails(IDictionary<string, object?>? arguments)
    {
        var incidentId = GetArgument<long>(arguments, "incidentId");
        return incidentId > 0 ? $"Fetching alert details for incident {incidentId}..." : "Fetching alert details for incident...";
    }

    private static string FormatSearchIncidents(IDictionary<string, object?>? arguments)
    {
        var searchString = GetArgument<string>(arguments, "searchString");
        return !string.IsNullOrEmpty(searchString) ? $"Searching incidents for '{searchString}'..." : "Searching for incidents...";
    }

    private static string FormatGetSimilarIncidents(IDictionary<string, object?>? arguments)
    {
        var query = GetArgument<string>(arguments, "query");
        if (!string.IsNullOrEmpty(query))
        {
            return $"Finding incidents similar to '{query}'...";
        }
        return "Finding similar incidents...";
    }

    #endregion

    #region Kusto MCP Tool Formatters

    private static string FormatKustoClusterGet(IDictionary<string, object?>? arguments)
    {
        var cluster = GetArgument<string>(arguments, "cluster");
        return !string.IsNullOrEmpty(cluster) ? $"Getting info for Kusto cluster {cluster}..." : "Getting Kusto cluster information...";
    }

    private static string FormatKustoDatabaseList(IDictionary<string, object?>? arguments)
    {
        var cluster = GetArgument<string>(arguments, "cluster");
        var clusterUri = GetArgument<string>(arguments, "cluster-uri");
        var name = cluster ?? clusterUri;
        return !string.IsNullOrEmpty(name) ? $"Listing databases in {name}..." : "Listing databases in Kusto cluster...";
    }

    private static string FormatKustoQuery(IDictionary<string, object?>? arguments)
    {
        var database = GetArgument<string>(arguments, "database");
        return !string.IsNullOrEmpty(database) ? $"Running Kusto query on {database}..." : "Running Kusto query...";
    }

    private static string FormatKustoSample(IDictionary<string, object?>? arguments)
    {
        var table = GetArgument<string>(arguments, "table");
        return !string.IsNullOrEmpty(table) ? $"Getting sample data from {table}..." : "Getting sample data from Kusto table...";
    }

    private static string FormatKustoTableList(IDictionary<string, object?>? arguments)
    {
        var database = GetArgument<string>(arguments, "database");
        return !string.IsNullOrEmpty(database) ? $"Listing tables in {database}..." : "Listing tables in Kusto database...";
    }

    private static string FormatKustoTableSchema(IDictionary<string, object?>? arguments)
    {
        var table = GetArgument<string>(arguments, "table");
        return !string.IsNullOrEmpty(table) ? $"Getting schema for {table}..." : "Getting Kusto table schema...";
    }

    #endregion

    #region Azure DevOps MCP Tool Formatters

    private static string FormatAdoPullRequest(IDictionary<string, object?>? arguments)
    {
        var title = GetArgument<string>(arguments, "title");
        var repositoryId = GetArgument<string>(arguments, "repositoryId");
        var sourceRef = GetArgument<string>(arguments, "sourceRefName");

        if (!string.IsNullOrEmpty(title))
        {
            if (!string.IsNullOrEmpty(repositoryId))
            {
                return $"Creating PR '{title}' in {repositoryId}...";
            }
            else if (!string.IsNullOrEmpty(sourceRef))
            {
                // Extract branch name from refs/heads/branch-name
                var branch = sourceRef.StartsWith("refs/heads/") ? sourceRef.Substring(11) : sourceRef;
                return $"Creating PR '{title}' from {branch}...";
            }

            return $"Creating PR '{title}'...";
        }

        return "Creating pull request in Azure DevOps...";
    }

    #endregion

    #region Helper Methods

    private static T? GetArgument<T>(IDictionary<string, object?>? arguments, string key)
    {
        if (arguments == null || !arguments.TryGetValue(key, out var value) || value == null)
        {
            return default;
        }

        if (value is T typedValue)
        {
            return typedValue;
        }

        // Handle JsonElement conversion
        if (value is JsonElement jsonElement)
        {
            try
            {
                return jsonElement.Deserialize<T>();
            }
            catch
            {
                return default;
            }
        }

        return default;
    }

    #endregion
}
