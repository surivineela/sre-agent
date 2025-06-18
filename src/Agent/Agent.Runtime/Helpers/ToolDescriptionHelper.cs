// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Runtime.Helpers;

public static class ToolDescriptionHelper
{
    /// <summary>
    /// Default safe description used when function names might be exposed or when no specific mapping exists
    /// </summary>
    public const string DefaultSafeDescription = "Further Analyzing...";

    public static string GetUserDescriptionForFunctionCallName(string functionName)
    {
        return functionName switch
        {
            // Existing cases
            "GetIncidentDetails" => "Fetching details of the incident.",
            "GetAlertDetails" => "Fetching details of the alert.",
            "GetThreadDetails" => "Fetching details of the thread.",

            // Control Flow functions
            "Wait" => "Waiting for a specified duration.",
            "MarkPlanComplete" => "Marking the current plan as complete.",
            "NotifyUser" => "Sending you an update.",
            "AskUserForInput" => "Asking for your input.",

            // ARM Plugin functions
            "GetTlsSettings" => "Checking TLS security settings for your resources.",
            "CheckIfResourceExists" => "Verifying if the resource exists.",
            "SetMinimumTlsVersion" => "Updating TLS security settings.",
            "RestartWebApp" => "Restarting your web application.",
            "GetArmResourceAsJson" => "Fetching detailed resource information.",
            "PowerOnVirtualMachine" => "Starting your virtual machine.",
            "GetVirtualMachineBootDiagnostics" => "Retrieving boot diagnostic information.",
            "CheckConnectivityToAzureWebJobsStorage" => "Testing storage connectivity.",
            "CheckTcpConnectivity" => "Testing network connectivity.",
            "CheckDnsResolution" => "Testing DNS resolution.",
            "GetAppSetting" => "Retrieving application settings.",
            "ListKeysAndUpdateAppSettingsAsync" => "Updating storage connection settings.",
            "UpdateAppSettingsAsync" => "Updating application configuration.",
            "RunAzCliReadCommandsAsync" => "Reading Azure resource information.",
            "RunAzCliWriteCommandsAsync" => "Making changes to Azure resources.",
            "GetAzCliHelpAsync" => "Getting Azure CLI command help.",

            // Function App plugins
            "ListFunctionAppsAsync" => "Finding your Function Apps.",
            "GetFunctionAppInfoAsync" => "Fetching details about your Function App.",
            "GetFunctionAppExecutionFailures" => "Analyzing Function App execution failures.",
            "GetFunctionAppCallStacks" => "Retrieving Function App call stack information.",
            "GetFailedRequestsPerFunction" => "Analyzing failed requests per function.",
            "GetTop3ExceptionsPerFunction" => "Finding the most common exceptions.",
            "GetHostRuntimeErrorEvents" => "Checking for runtime errors.",
            "IsFunctionApp" => "Verifying if the resource is a Function App.",
            "HasHostRuntimeErrors" => "Checking for Function App runtime issues.",
            "TriggerFunctionAppSync" => "Refreshing Function App configuration.",
            "GetFunctionAppConfigurationChecks" => "Analyzing Function App configuration.",
            "GetFunctionAppDeploymentChecks" => "Checking deployment information.",
            "GetFunctionAppDeploymentHistory" => "Retrieving deployment history.",

            // Role Assignment functions
            "GetRoleAssignments" => "Checking access permissions.",
            "AddRoleAssignment" => "Granting access permissions.",
            "RemoveRoleAssignment" => "Removing access permissions.",
            "CheckRoleAssignment" => "Verifying access permissions.",
            "GetRoleDetailsFromNameAsync" => "Getting role permission details.",

            // Graph DB functions
            "Query" => "Searching the knowledge graph.",
            "FindAllNetworkConnectedResources" => "Finding network-connected resources.",
            "GetApplicationComponentsSummary" => "Getting application components overview.",
            "VisualizeApplicationComponents" => "Creating application architecture diagram.",
            "DiscoverApplications" => "Discovering applications in your subscription.",
            "AddSourceCodeNodeToContainerAppNode" => "Linking source code repository.",
            "AddIgnoreTagToResource" => "Adding ignore tag to resource.",
            "GetContainerAppsWithNodesWithoutSourceCodeNodes" => "Finding Container Apps without source code links.",
            "UpdateRepoNodeWithLastScanTime" => "Updating repository scan timestamp.",
            "GetGeneralHealth" => "Checking resource health status.",
            "GetManagedResourcesInfoAsync" => "Getting managed resources inventory.",
            "SearchResource" => "Searching for resources.",
            "SearchResourceByName" => "Finding resources by name.",
            "GetResourceCount" => "Counting resources.",
            "ListSubscriptions" => "Listing available subscriptions.",
            "ListResourceGroups" => "Listing resource groups.",
            "GetActivityLogsSummary" => "Analyzing recent activity logs.",
            "ListResourcesByType" => "Listing resources by type.",
            "GetKnowledgeGraphResourceUsageDashboard" => "Getting resource usage dashboard.",
            "VisualizeAKSMicroserviceTopology" => "Creating Kubernetes architecture diagram.",
            "GetResourceBasicProperties" => "Getting basic resource information.",
            "GetResourceDetailedProperties" => "Fetching detailed resource properties.",
            "GetResourceIdForResourceName" => "Finding resource ID by name.",
            "GetResourceHealthInfo" => "Checking resource health metrics.",

            // ACA Kusto functions
            "ExecuteFunction" => "Running Kusto query function.",
            "ListKustoFunctions" => "Listing available Kusto functions.",

            // App Code Analysis functions
            "GetCallStackForApp" => "Retrieving application call stack.",
            "WaitInMilliSeconds" => "Waiting for specified time.",
            "GetSummaryOfExceptions" => "Analyzing application exceptions.",
            "GetStackTraceOfLastException" => "Getting recent exception details.",
            "GetStackTraceOfMostCommonException" => "Getting common exception details.",
            "PerformDeploymentSwapForApp" => "Swapping application deployment slots.",
            "GetDeploymentActivity" => "Checking deployment activity.",
            "GetAppConsoleLogs" => "Retrieving application logs.",
            "GetWebAppDownAnalysisLink" => "Getting web app analysis link.",

            // Chart/Visualization functions
            "PlotTimeSeriesData" => "Creating time series chart.",
            "PlotBarChartAsync" => "Creating bar chart.",
            "PlotPieChartAsync" => "Creating pie chart.",
            "PlotScatterAsync" => "Creating scatter plot.",
            "PlotHeatmapAsync" => "Creating heatmap visualization.",
            "PlotAreaChartWithCorrelationAsync" => "Creating area chart with correlation.",
            "GetPieChartBase64Image" => "Generating pie chart image.",

            // Metrics functions
            "GetFunctionAppRequestAvailability" => "Checking Function App availability metrics.",

            // GitHub functions
            "CreateGithubIssue" => "Creating GitHub issue.",

            // Helper Agent functions
            "StartDiagnosisAgent" => "Starting resource diagnosis.",

            // ACA specific functions (from KernelFunctionNames)
            "get_job_definition" => "Getting Container App Job definition.",
            "get_job_execution_json" => "Getting job execution details.",
            "get_job_execution_events" => "Getting job execution events.",
            "get_all_job_executions_error_events" => "Getting all job execution errors.",
            "get_all_job_executions_final_status" => "Getting job execution status.",
            "get_job_execution_events_container" => "Getting container job events.",
            "get_keda_events_for_job_scaled_jobs" => "Getting KEDA scaling events.",
            "get_legion_vk_events_for_jobs_running_consumption_v2" => "Getting Legion VK events.",
            "get_issue_investigation_time_range" => "Getting investigation time range.",
            "get_initial_investigation_summary_report" => "Getting investigation summary.",
            "submit_agent_feedback" => "Submitting agent feedback.",
            "get_managed_environment_info" => "Getting Container App environment information.",
            "call_kusto_function" => "Running Kusto analytics query.",
            "list_revisions" => "Listing Container App revisions.",
            "search_design_docs" => "Searching documentation.",

            // Kubectl functions (from KubePlugin)
            "apply" => "Applying Kubernetes configuration.",
            "create" => "Creating Kubernetes resource.",
            "patch" => "Updating Kubernetes resource.",
            "replace" => "Replacing Kubernetes resource.",
            "scale" => "Scaling Kubernetes resource.",
            "label" => "Adding labels to Kubernetes resource.",
            "annotate" => "Adding annotations to Kubernetes resource.",
            "rollout" => "Managing Kubernetes rollout.",

            // Default case
            _ => DefaultSafeDescription
        };
    }
}
