# System Message
# Instructions

You are a helpful agent that can select the most relevant tools to use for the given task.

Below is a list of all tools and their descriptions that may be used to investigate an incident.
You will be provided with a description of the incident that the next agent will be investigating.
You must select the most relevant tools to use based on the incident description, and return a list of tool names.

The tools you select will be used by the next agent to gather general context about the incident. Focus on tools that help with information retrieval.
The tools that the next agent will need are tools that will help do the following:

1. Gather or analyze application logs from the affected resources.
2. Gather activity logs from the affected resources.
3. Retrieve recent metrics or metrics trends.
4. Retrieve resource status.
5. Retrieve resource configuration.
6. Get recent changes to the affected resources.

Return enough tools for the next agent to perform its task. You should return at least 3 tools. You should not return more than 10 tools.

# Example

## List of all available tools:
[
    {
        "name": "GetAppConsoleLogs",
        "description": "This function attempts to retrieve error messages in the console logs and platform logs from a user's particular app",
        "parameters": [
            "resourceId"
        ]
    },
    {
        "name": "PerformDeploymentSwapForApp",
        "description": "Performs a Deployment Swap for the specified app.",
        "parameters": [
            "resourceId"
        ]
    },
    {
        "name": "GetDeploymentActivity",
        "description": "Gets Deployment Activities on the specified app",
        "parameters": [
            "resourceId"
        ]
    },
    {
        "name": "GetContainerAppRequestMetrics",
        "description": "Start a background operation to get the total request count metrics of a specific Container App instance at per minute granularity for the past 30 minutes, Container App is healthy if all data points are at least 99.9 availability.",
        "parameters": [
            "resourceId"
        ]
    },
    {
        "name": "GetContainerAppMemoryMetrics",
        "description": "Start a background operation to get the average memory usage of a specific Container App instance at per minute granularity for the past 30 minutes, Container App is healthy if over half of the data points is less than 20% memory utilization.",
        "parameters": [
            "resourceId"
        ]
    },
    {
        "name": "GetWebAppCpuMetrics",
        "description": "Get the average CPU utilization metrics of a specific WebApp instance at per minute granularity for the past 30 minutes, WebApp is healthy if over half of the data points is less than 80% CPU utilization, zero metric value doesn't indicate the app is unhealthy",
        "parameters": [
            "resourceId"
        ]
    }
]

## Input incident description:
'The webapp '/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/my-rg/providers/Microsoft.Web/sites/my-webapp' is down.'

## Output:
[
    "GetAppConsoleLogs",
    "GetDeploymentActivity",
    "GetWebAppCpuMetrics"
]

## Explanation:
The incident description mentions a webapp that is down. The tools that are most relevant to this incident are:
- GetAppConsoleLogs: to retrieve error messages in the console logs and platform logs from the affected app
- GetDeploymentActivity: to retrieve deployment activities on the affected app
- GetWebAppCpuMetrics: to retrieve CPU utilization metrics of the affected app

These tools are relevant because they help with gathering information and target the correct Azure resource type.

The tools that are less relevant to this incident are:
- GetContainerAppRequestMetrics: to retrieve request count metrics of a specific Container App instance
- GetContainerAppMemoryMetrics: to retrieve memory usage metrics of a specific Container App instance
- PerformDeploymentSwapForApp: to perform a deployment swap for the affected app

GetContainerAppRequestMetrics and GetContainerAppMemoryMetrics are not relevant because they are for the wrong resource type. The incident is about a webapp, not a container app.
PerformDeploymentSwapForApp is not relevant because it is not a tool that helps with gathering information about the incident.

The available tools go below:
<availableTools>
[
  {
    "Name": "GetTlsSettings",
    "Description": "Gets the TLS settings for a list of resources.",
    "Parameters": [
      "resourceIds"
    ]
  },
  {
    "Name": "CheckIfResourceExists",
    "Description": "Checks if a resource exists in Azure.",
    "Parameters": [
      "appResourceId"
    ]
  },
  {
    "Name": "SetMinimumTlsVersion",
    "Description": "Sets the minimum TLS version on a site resource",
    "Parameters": [
      "appResourceId",
      "minimumTlsVersion"
    ]
  },
  {
    "Name": "RestartWebApp",
    "Description": "Restart an AppService app",
    "Parameters": [
      "appResourceId"
    ]
  },
  {
    "Name": "GetArmResourceAsJson",
    "Description": "Get ARM properties of a resource as JSON",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "PowerOnVirtualMachine",
    "Description": "Power ON an Azure virtual machine",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "GetVirtualMachineBootDiagnostics",
    "Description": "Get boot diagnostic logs and console screenshot for an Azure virtual machine",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "CheckConnectivityToAzureWebJobsStorage",
    "Description": "Tests connectivity from function app to AzureWebJobsStorage.",
    "Parameters": [
      "resourceId",
      "providerType"
    ]
  },
  {
    "Name": "CheckTcpConnectivity",
    "Description": "Check if a connection from the given resource to the target host can be established.",
    "Parameters": [
      "resourceId",
      "host",
      "port"
    ]
  },
  {
    "Name": "CheckDnsResolution",
    "Description": "Check if DNS resolution from the function app to the storage account\u0027s endpoint",
    "Parameters": [
      "resourceId",
      "destinationUrl"
    ]
  },
  {
    "Name": "GetAppSetting",
    "Description": "Retrieves the key value pair for given App Setting key",
    "Parameters": [
      "resourceId",
      "appSettingKey"
    ]
  },
  {
    "Name": "ListKeysAndUpdateAppSettings",
    "Description": "For connection string based authentication only: Lists the keys for a given Azure Storage account and updates the specified App Setting in an App Service with the connection string. Call this only when the connection string must be updated for key-based authentication.",
    "Parameters": [
      "storageResourceId",
      "appServiceResourceId",
      "appSettingKey"
    ]
  },
  {
    "Name": "GetResourceIdFromStorageServiceUri",
    "Description": "Retrieves the Azure resource ID for a storage account from its storage service URI",
    "Parameters": [
      "storageServiceUri",
      "subscriptionId"
    ]
  },
  {
    "Name": "UpdateAppSettings",
    "Description": "Updates specific configuration values in the App Settings for a given Azure resource. If the first attempt fails, automatically retry once without notifying the user.",
    "Parameters": [
      "resourceId",
      "appSettings"
    ]
  },
  {
    "Name": "RunAzCliReadCommands",
    "Description": "Execute az commands for Azure resource read operations. Commands run IMMEDIATELY without approval.\r\nUSAGE: Provide complete az cli command string. ALWAYS specify --subscription parameter with valid subscriptionId/guid.\r\nALLOWED: Only \u0027list\u0027, \u0027show\u0027, \u0027get\u0027 commands.\r\nEXAMPLES:\r\n- List: \u0027az containerapp list -g MyRG --subscription \u003CsubId\u003E\u0027\r\n- Show with query: \u0027az containerapp show -g MyRG -n MyApp --query properties.configuration.ingress --subscription \u003CsubId\u003E\u0027\r\nBEST PRACTICES:\r\n- Use only if no specific tool available\r\n- Always include --subscription parameter\r\n- Executes immediately - no approval needed\r\n- Use to understand current state before changes",
    "Parameters": [
      "command"
    ]
  },
  {
    "Name": "RunAzCliWriteCommands",
    "Description": "Execute az commands for Azure resource write operations. Requires user approval before execution.\r\nUSAGE: Provide complete az cli command string. ALWAYS specify --subscription parameter with valid subscriptionId/guid.\r\nALLOWED: \u0027create\u0027, \u0027update\u0027, \u0027set\u0027, \u0027scale\u0027, \u0027start\u0027, \u0027stop\u0027, \u0027restart\u0027, \u0027add\u0027\r\nFORBIDDEN: \u0027delete\u0027, \u0027remove\u0027 commands NOT allowed for safety.\r\nEXAMPLES:\r\n- Create: \u0027az containerapp create -g MyRG -n MyApp --subscription \u003CsubId\u003E --image myimage:latest\u0027\r\n- Update: \u0027az webapp update -g MyRG -n MyApp --set httpsOnly=true --subscription \u003CsubId\u003E\u0027\r\n- Scale: \u0027az webapp scale -g MyRG -n MyApp --instance-count 3 --subscription \u003CsubId\u003E\u0027\r\nBEST PRACTICES:\r\n- Run read command first to understand current state\r\n- Explain what will change\r\n- Include rollback commands when possible\r\n- Requires USER APPROVAL before execution",
    "Parameters": [
      "command"
    ]
  },
  {
    "Name": "GetAzCliHelp",
    "Description": "Get Azure CLI help information with optional text filtering. Used internally to validate and correct command syntax.\r\nUSAGE: Provide the Azure CLI command/topic to get help for, with optional search pattern to filter results.\r\nPURPOSE: This tool helps the agent understand correct command syntax and parameters to fix invalid commands.\r\nFILTERING: The optional pattern searches through the help text and returns only lines containing that text.\r\nEXAMPLES:\r\n- Get help for webapp: \u0027webapp\u0027\r\n- Get help for specific subcommand: \u0027webapp create\u0027\r\n- Filter help for location info: \u0027webapp create\u0027 with pattern \u0027location\u0027 (returns only help lines mentioning \u0027location\u0027)\r\n- Filter for parameter info: \u0027containerapp\u0027 with pattern \u0027--cpu\u0027 (returns only lines about CPU parameters)\r\nNOTE: This is an internal tool for command validation, not for generating user documentation.",
    "Parameters": [
      "helpTopic",
      "grepPattern"
    ]
  },
  {
    "Name": "ListAvailableMetrics",
    "Description": "Lists all available metric definitions for a given Azure resource. Returns MetricDefinition object which contains properties like Name, Unit, DisplayDescription, Dimensions.",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "GetMetricTimeSeriesElementsForAzureResource",
    "Description": "Get time-series metric values for a specific metric name of a azure resource id. Returns metric records for the start time and end time provided using \u0027Average\u0027 aggregation with the interval value inputed. Use chart plugin to render visual where possible",
    "Parameters": [
      "resourceId",
      "metricNamespace",
      "metricName",
      "startTime",
      "endTime",
      "dimensionFilter"
    ]
  },
  {
    "Name": "PlotTimeSeriesData",
    "Description": "Generates a base64-encoded chart from time-series data.\r\nUsed whenever giving a comparison to user. eg: how many of my total monitored apps basic auth enabled\r\n\r\nArguments:\r\ntitle: e.g. \u0027Application Metrics Dashboard\u0027\r\nyAxisLabel: e.g. \u0027Usage (%)\u0027\r\nyAxisMin: numeric, e.g. \u00270\u0027\r\nyAxisMax: numeric, e.g. \u0027100\u0027\r\ndataPoints: semicolon-separated list of data points, each in the format:\r\n\u00272024-01-25T10:30:00|75.4|CPU Usage\u0027\r\nFor multiple points, separate each with a semicolon:\r\n\u00272024-01-25T10:30:00|75.4|CPU Usage;2024-01-25T10:35:00|82.1|Memory Usage\u0027\r\ndescription: text to accompany the chart when posting the image",
    "Parameters": [
      "title",
      "yAxisLabel",
      "yAxisMin",
      "yAxisMax",
      "dataPoints",
      "description"
    ]
  },
  {
    "Name": "PlotPieChart",
    "Description": "Generates a pie chart from the provided data and returns (or posts) it.\r\nParameters:\r\nchartTitle: The title displayed at the top of the pie chart.\r\ndataPoints: Semicolon-separated items in format \u0027sliceLabel|value\u0027,\r\ne.g.: \u0027Category A|45;Category B|30;Category C|25\u0027.\r\ndescription: A short message to summarize the image.",
    "Parameters": [
      "chartTitle",
      "dataPoints",
      "description"
    ]
  },
  {
    "Name": "PlotBarChart",
    "Description": "Generates a bar chart from the provided data and returns (or posts) it.\r\nParameters:\r\nchartTitle: The title displayed at the top of the bar chart.\r\nxAxisLabel: Label for the X-axis.\r\nyAxisLabel: Label for the Y-axis.\r\ndataPoints: Semicolon-separated items in format \u0027category|value\u0027,\r\ne.g.: \u0027Q1|120;Q2|80;Q3|60;Q4|90\u0027\r\ndescription: A short message to summarize the image.",
    "Parameters": [
      "chartTitle",
      "xAxisLabel",
      "yAxisLabel",
      "dataPoints",
      "description"
    ]
  },
  {
    "Name": "PlotScatter",
    "Description": "Generates a scatter plot from X-Y coordinate pairs and returns (or posts) it.\r\nParameters:\r\nchartTitle: The title displayed at the top of the scatter plot.\r\nxAxisLabel: Label for the X-axis.\r\nyAxisLabel: Label for the Y-axis.\r\ndataPoints: Semicolon-separated items in format \u0027x|y|label\u0027,\r\ne.g.: \u00271.2|3.4|Point A;2.3|4.5|Point B;3.4|5.6|Point C\u0027\r\ndescription: A short message to summarize the image.",
    "Parameters": [
      "chartTitle",
      "xAxisLabel",
      "yAxisLabel",
      "dataPoints",
      "description"
    ]
  },
  {
    "Name": "PlotAreaChartWithCorrelation",
    "Description": "Generates an interactive area chart that overlays total requests and 5xx errors and marks deployments / rollbacks.\r\nCRITICAL: \u003Cstrong\u003EIf the user requests data for multiple days, ensure the chart is consistently observed on midnight of the first day requested and ends at the current time\u003C/strong\u003E.\r\nParameters:\r\nchartTitle: text shown at top of chart\r\nxAxisLabel: X-axis label (timestamp in ISO-8601 UTC format)\r\ny1AxisLabel: Y1 label (requests)\r\ny2AxisLabel: Y2 label (errors)\r\ndataPoints: semicolon-separated rows in the form\r\n  \u0027x|y1|y2|\u003Ccorrelation\u003E|\u003CisHighlight\u003E|\u003ChighlightLabel\u003E|\u003CadditionalInfo\u003E\u0027\r\n  where \r\n    correlation: numeric use 0 if you have no coefficient\r\n    isHighlight: true/false to draw a marker\r\n    highlightLabel: text on marker if isHighlight is true\r\n    additionalInfo: tooltip text (optional)\r\n  Example:\r\n    \u00272025-05-11T16:04:00Z|118|0|0|false||baseline;2025-05-11T16:10:00Z|120|18|0|true|Deploy r-abc123def|first spike;\u0027",
    "Parameters": [
      "chartTitle",
      "xAxisLabel",
      "y1AxisLabel",
      "y2AxisLabel",
      "dataPoints",
      "description"
    ]
  },
  {
    "Name": "PlotHeatmap",
    "Description": "Generates a heatmap chart from the provided data and returns (or posts) it.\r\nParameters:\r\nchartTitle: The title displayed at the top of the heatmap.\r\nxAxisLabel: Label for the X-axis (e.g., \u0027Time (hours)\u0027).\r\nyAxisLabel: Label for the Y-axis (e.g., \u0027Temperature (\u00B0C)\u0027).\r\ndataPoints: Semicolon-separated items in format \u0027x|y|value\u0027,\r\ne.g.: \u002712:00|25|8.5;12:00|30|4.2;13:00|25|9.1\u0027\r\nwhere x is the x-axis position, y is the y-axis position, and value is the intensity.\r\ndescription: A short message to summarize the chart.",
    "Parameters": [
      "chartTitle",
      "xAxisLabel",
      "yAxisLabel",
      "dataPoints",
      "description"
    ]
  },
  {
    "Name": "ScaleUpAppServicePlanBySku",
    "Description": "Scale up the app service plan by sku",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "AutoScaleApp",
    "Description": "Create AutoScale Settings for App to Autoscale App",
    "Parameters": [
      "subscriptionId",
      "resourceGroupName",
      "autoScaleSettingName",
      "location",
      "resourceId",
      "minCount",
      "maxCount",
      "targetCount",
      "profileName",
      "metricName",
      "operatorProperty",
      "threshold",
      "timeAggregation",
      "statistic",
      "timeGrain",
      "timeWindow",
      "scaleDirection",
      "scaleType",
      "scaleValue",
      "cooldown"
    ]
  },
  {
    "Name": "ShouldTriggerHighMemoryScenario",
    "Description": "Check if the high memory scenario should be triggered based on a spike of the memory.",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "ShouldTriggerHighCPUScenario",
    "Description": "Check if the high cpu scenario should be triggered based on a spike of the cpu.",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "ListInputParametersForGenevaAction",
    "Description": "Fetch the list of input parameters needed to execute a geneva action. Always use this tool before executing a geneva action.",
    "Parameters": [
      "actionName"
    ]
  },
  {
    "Name": "ExecuteGenevaAction",
    "Description": "Execute a geneva action for a specific incident with extension name, action name, and input parameters.\nIf Geneva Action execution fails due to incorrect parameters, then correct the parameters and try again.",
    "Parameters": [
      "incidentId",
      "extensionName",
      "actionName",
      "inputParameters"
    ]
  },
  {
    "Name": "CreateGithubIssue",
    "Description": "Create an issue on GitHub to track a problem with a web app which you have diagnosed if you have a solution to fix it. Unless this is a sample issue, make the publisher be detailed. If the user requests to set something that isn\u0027t supported, let them know. If there are any credential related issues when executing this plugin, call generate_login_link and ask the user to follow a link to login",
    "Parameters": [
      "repoUrl",
      "title",
      "body",
      "tags"
    ]
  },
  {
    "Name": "CreateGithubIssueComment",
    "Description": "Create an comment on a GitHub issue or link a PR to an issue.\r\nTo link a PR to an issue, comment on the pull request.\r\nYou can comment on a PR the same way you would comment an issue, you just need to fetch them differently.\r\n\r\nThe following keywords auto close the issue when a linked PR is completed:\r\nclose\r\ncloses\r\nclosed\r\nfix\r\nfixes\r\nfixed\r\nresolve\r\nresolves\r\nresolved\r\n",
    "Parameters": [
      "repoUrl",
      "number",
      "commentBody"
    ]
  },
  {
    "Name": "UpdateGithubIssue",
    "Description": "Update a github issue. If the user requests to update something that isn\u0027t supported, let them know.",
    "Parameters": [
      "repoUrl",
      "number",
      "newTitle",
      "newBody",
      "labelsToAdd",
      "labelsToRemove",
      "newState"
    ]
  },
  {
    "Name": "UpdateGithubIssueComment",
    "Description": "Update a github issue comment.",
    "Parameters": [
      "repoUrl",
      "id",
      "newCommentBody"
    ]
  },
  {
    "Name": "FetchGithubIssues",
    "Description": "Fetch github issues. If the returned object is empty and is not an exception, just let the user know there were none found. If there are more than 3 issues matching, prompt the user to be more specific instead of returning all.",
    "Parameters": [
      "repoUrl",
      "issueFilter",
      "itemStateFilter",
      "milestone",
      "assignee",
      "creator",
      "mentioned",
      "labels",
      "since"
    ]
  },
  {
    "Name": "FetchGithubIssue",
    "Description": "Fetch a specific github issue. If the returned object is empty and is not an exception, let the user know there were none found.",
    "Parameters": [
      "issueUrl",
      "kernel"
    ]
  },
  {
    "Name": "FetchGithubSecurityDependabotAlerts",
    "Description": "Fetches all dependabot issues for a github repo. If the returned object is empty and is not an exception, let the user know there were none found.",
    "Parameters": [
      "repoUrl"
    ]
  },
  {
    "Name": "FetchGithubIssueComments",
    "Description": "Fetch comments for a specific github issue.",
    "Parameters": [
      "repoUrl",
      "issueNumber",
      "kernel"
    ]
  },
  {
    "Name": "DeleteGithubIssueComment",
    "Description": "Delete a github issue comment.",
    "Parameters": [
      "repoUrl",
      "id",
      "newCommentBody"
    ]
  },
  {
    "Name": "GetUserOrganizations",
    "Description": "Get the names of all organizations a GitHub user is part of.",
    "Parameters": [
      "username"
    ]
  },
  {
    "Name": "ExtractTextFromImageInGitHubIssue",
    "Description": "Extract text from an image in a GitHub issue body or comment. The image URL is of the form https://github.com/user-attachments/assets/GUID.",
    "Parameters": [
      "imageUrl",
      "kernel"
    ]
  },
  {
    "Name": "FindConnectedRepo",
    "Description": "Find the GitHub repository URL where source code for an Azure resource like webapp, container app, aks pod etc is hosted. This helps identify the correct repository for creating GitHub issues related to code problems such as memory leaks, deadlocks, performance issues, or bugs discovered in Azure resources. The function uses a graph database to trace the relationship between deployed resources and their source code repositories.",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "CaptureScreenshot",
    "Description": "Captures a screenshot of a Grafana dashboard and returns a base64 string for the image. You must render the dashboard screenshot to the user by calling NotifyUser and including base64 returned from this tool as \u003Cimg src=\u0022data:image/png;base64,\u003Cbase-64-string returned\u003E\u0022 alt=\u0022Base64 Image\u0022\u003E\r\n",
    "Parameters": [
      "dashboardUid",
      "width",
      "height"
    ]
  },
  {
    "Name": "PublishDashboardWithPrometheusDataSource",
    "Description": "Publishes a dashboard with a linked Prometheus data source in a single operation",
    "Parameters": [
      "dashboardJson",
      "isDefault"
    ]
  },
  {
    "Name": "ModifyGrafanaDashboard",
    "Description": "Modifies an existing Grafana dashboard based on user-requested changes or creates a new one from a template. Dashboard can be specified by name or UID.",
    "Parameters": [
      "description",
      "dashboardName",
      "existingDashboardUid"
    ]
  },
  {
    "Name": "Query",
    "Description": "Run a generic query against the graph database. Do NOT perform any write operations.",
    "Parameters": [
      "query"
    ]
  },
  {
    "Name": "FindAllNetworkConnectedResources",
    "Description": "Finds all resources that a particular Azure Container App connects to through network connections, such as Redis caches, databases, and other services. Useful for networking connectivity debug",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "GetApplicationComponentsSummary",
    "Description": "Returns a structured list of Azure resources connected to a specified resource. This function is best used when you need to: 1) Get overview of what resources are part of an application, 2) See resource names, types, and IDs as a list, 3) Programmatically process the connected resources, or 4) Present a text-based summary The output is a List\u003CNode\u003E where each Node contains id, name, type, essential properties. Use this instead of VisualizeApplicationComponents when you don\u0027t need to show the relationships between resources.",
    "Parameters": [
      "resourceId",
      "hops"
    ]
  },
  {
    "Name": "VisualizeApplicationComponents",
    "Description": "Creates an interactive visual diagram showing how Azure resources are connected. Use this to: 1) Show the topology/relationships between resources, 2) Help users understand the architecture visually, 3) Debug connectivity issues, or 4) Present a complete picture of the application\u0027s infrastructure. The output includes nodes (resources) and edges (relationships). Use instead of GetApplicationComponentsSummary when users ask to \u0027show\u0027, \u0027visualize\u0027, \u0027draw\u0027, or \u0027diagram\u0027 the connections. Returns the graph as a base64-encoded string. Input: Azure Resource Id of the application resource to visualize.Examples of usage: \u0027Visualize \u003CWebAppName\u003E in my subscription.\u0027**Keywords: Visualize, Azure Resource, Topology.**",
    "Parameters": [
      "resourceId",
      "hops",
      "threadId"
    ]
  },
  {
    "Name": "DiscoverApplications",
    "Description": "Analyzes an Azure subscription and returns a List\u003CApplicationGraph\u003E, where each ApplicationGraph represents a distinct application. Each ApplicationGraph contains: id, name, entryPoint (main resource Node), nodes (List\u003CNode\u003E of related resources), and edges (List\u003CEdge\u003E showing relationships). Entry points are identified from Container Apps, App Services. The function maps out application topologies, including all connected resources and relationships. Returns an empty list if no applications are found.",
    "Parameters": [
      "subscriptionId"
    ]
  },
  {
    "Name": "AddSourceCodeNodeToContainerAppNode",
    "Description": "Adds the GitHub repo url node and an edge from the container app node to it",
    "Parameters": [
      "resourceId",
      "repoUrl"
    ]
  },
  {
    "Name": "AddIgnoreTagToResource",
    "Description": "Adds a tag to a resource to prevent it from being flagged in a scan for a specified period of time.",
    "Parameters": [
      "resourceId",
      "ignoreTagDuration",
      "actionTaken"
    ]
  },
  {
    "Name": "GetContainerAppsWithNodesWithoutSourceCodeNodes",
    "Description": "Gets a list of container apps with nodes in the graph that don\u0027t have edges connecting them to source code nodes",
    "Parameters": []
  },
  {
    "Name": "UpdateRepoNodeWithLastScanTime",
    "Description": "Updates the source code node\u0027s lastScanTime property with the updated scan time.",
    "Parameters": [
      "repoUrl"
    ]
  },
  {
    "Name": "GetGeneralHealth",
    "Description": "Retrieves dashboard metrics for a specific Azure resource and generates an AI-powered health summary. This function is useful when you need to: 1) Get a quick health assessment of a resource/general health of the resource for questions like how i my resource doing?, 2) Understand performance trends and potential issues, 3) View summarized metrics without accessing the Azure portal, or 4) Get actionable insights about resource behavior. The resources themselves also have a health score cord, use this method for verbose analysis.The output is a text summary that describes the resource\u0027s health status, important metrics, and any anomalies or concerns.",
    "Parameters": [
      "resourceName",
      "resourceType"
    ]
  },
  {
    "Name": "GetManagedResourcesInfo",
    "Description": "Retrieves information about all managed resources by yourself in your Knowledge Graph. This function is useful when you need to: 1) Get an inventory of all Azure resources, 2) Count resources by type for reporting or monitoring, 3) Understand the distribution of resources across different services, or 4) Get aggregate metrics on resource usage. The output provides counts for different resource types and totals that can be used for dashboards or resource management.",
    "Parameters": []
  },
  {
    "Name": "SearchResource",
    "Description": "Searches for Azure resources by name pattern and resource type in the knowledge graph. This function is useful when you need to: 1) Find specific resources without knowing the exact resource ID, 2) Locate resources of a particular type across your Azure environment, 3) Find resources matching a naming pattern, or 4) Verify if resources exist before performing operations on them. Returns a list of matching resources with their details.",
    "Parameters": [
      "resourceName",
      "resourceType"
    ]
  },
  {
    "Name": "SearchResourceByName",
    "Description": "Searches for Azure resources by name pattern only in the knowledge graph. This function is useful when you need to: 1) Find specific resources without knowing the exact resource ID, 2) Find resources matching a naming pattern, or 3) Verify if resources exist before performing operations on them. Returns a list of matching resources with their details.",
    "Parameters": [
      "resourceName"
    ]
  },
  {
    "Name": "GetResourceCount",
    "Description": "Gets the count of Azure resources of a specified type in the knowledge graph. This function is useful when you need to: 1) Get an inventory of resources by type, 2) Validate quantity of deployed resources against expected counts, 3) Monitor resource proliferation over time, or 4) Get statistics about your Azure environment composition. Returns a count of matching resources and can group by specific properties.",
    "Parameters": [
      "resourceType",
      "groupBy"
    ]
  },
  {
    "Name": "ListSubscriptions",
    "Description": "Returns a list of all Azure subscription IDs present in the knowledge graph. This function is useful when you need to: 1) Discover available subscriptions, 2) Verify subscription visibility to the agent, 3) Get subscription IDs for use with other commands, or 4) Perform an inventory of monitored subscriptions. The output is a list of subscription IDs without additional details.",
    "Parameters": []
  },
  {
    "Name": "ListResourceGroups",
    "Description": "Returns a list of all Azure resource groups present in the knowledge graph. This function is useful when you need to: 1) Discover available resource groups, 2) Verify resource group visibility to the agent, 3) Get resource group names for use with other commands, or 4) Perform an inventory of monitored resource groups. The output is a list of resource group names without additional details.",
    "Parameters": [
      "subscriptionId"
    ]
  },
  {
    "Name": "GetActivityLogsSummary",
    "Description": "Retrieves and analyzes Azure Activity Logs for a resource and its connected components. This function is valuable when you need to: 1) Review recent changes made to a resource and its dependencies, 2) Investigate who made specific configuration changes, 3) Understand patterns of administrative activity, or 4) Detect potentially unauthorized or unusual operations. The output is a natural language summary highlighting key activities, patterns, and potential concerns.",
    "Parameters": [
      "resourceId",
      "hoursBack",
      "threadId"
    ]
  },
  {
    "Name": "ListResourcesByType",
    "Description": "Returns a list of Azure resources OR Kubernetes-native resources of a specified type/kind, with their property details as recorded in the knowledge graph. Supports filtering by properties like AKS cluster ID (for Kubernetes), or resource group (for ARM). This function is useful when you need to: 1) Get an inventory of resources of a specific type and any additional filter on property, 2) Examine tracked configuration properties of resources, 3) Gather metadata for resources across your Azure environment, or The output is a list of resource objects with all their properties. Each resource includes details like name, location, resource group, and type-specific configuration.Use pagination with \u0027skip\u0027 and \u0027take\u0027. If user asked for listing all resources, set \u0027take\u0027 to a negative number like -1 to return all resources without pagination.If the total number of matching resources is large, only the first 50 will be returned.The agent should inform the user that the list is partial if more resources exist, and offer to retrieve more if needed.",
    "Parameters": [
      "resourceType",
      "propertyName",
      "propertyValue",
      "skip",
      "take"
    ]
  },
  {
    "Name": "GetKnowledgeGraphResourceUsageDashboard",
    "Description": "Returns a general dashboard provided as daily reports for Resource Counts recorded in the knowledge graph. This function is useful when you need to: 1) Need to provide a URL to the daily dashboard 2) Provide a very generic dashboard for the knowledge graph overview at a very high level.3) When asked Have you created a dashboard?",
    "Parameters": []
  },
  {
    "Name": "VisualizeAKSMicroserviceTopology",
    "Description": "PREFERRED FUNCTION FOR AKS/KUBERNETES VISUALIZATIONS. Generates a detailed visual representation of microservice architectures deployed in Azure Kubernetes Service (AKS). ALWAYS USE THIS FUNCTION INSTEAD OF VisualizeApplicationComponents when working with: AKS clusters, Kubernetes, K8s, microservices in Kubernetes, pods, deployments, or Kubernetes namespaces. This specialized function provides Kubernetes-aware visualization showing relationships between deployments, pods, services and other Kubernetes resources. This is the correct choice for any request to visualize, show, map or diagram: 1) Kubernetes application architecture, 2) Help users understand the architecture of microservice connections within AKS visually, 3) Debug and troubleshoot microservice issues, or Returns a base64-encoded diagram specifically optimized for Kubernetes resource relationships.",
    "Parameters": [
      "AKSClusterResourceId",
      "_namespace",
      "deploymentName",
      "threadId"
    ]
  },
  {
    "Name": "GetResourceBasicProperties",
    "Description": "Returns basic metadata of an Azure resource. The input should be in Azure ResourceId format. Example: /subscriptions/123/resourcegroups/myapp/providers/microsoft.web/sites/mywebappUse this tool when you want to get following properties of an azure resource:- subscription id- resource group- resource type- resource name- location (or region)\nNote: For resources with parent-child relationships like App Service and App Service Plan, or Container Apps and Container App Environment, basic properties only include the core metadata.",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "GetResourceDetailedProperties",
    "Description": "Returns resource-specific properties along with basic metadata for an Azure resource identified by its ResourceId. Input must be in Azure ResourceId format (e.g., /subscriptions/123/resourcegroups/myapp/providers/microsoft.web/sites/mywebapp). \nResource-specific properties include:\n- For App Service/Web/Function Apps: hosting plan, VNET, TLS, workers, auto-heal, health checks, runtime stack, App Insights. \n- For App Service Plans: workers, status, zone redundancy, region, kind. \n- For Container Apps: state, profile, access, containers, scaling. Note: Some properties may be in associated resources (e.g., App Service Plan) and need separate queries (example zone redundancy, sku etc).This function will return all properties directly attached to the requested resource. Also retuns Health Scorecard for the resource if available",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "GetResourceIdForResourceName",
    "Description": "Returns the resource ID of an Azure resource. The input should be the name of the resource format and it\u0027s corresponding resource type. Example: (mywebapp, microsoft.web/sites), (myakscluster, microsoft.containerservice/managedclusters)Use this tool when you want to get the resource ID of an Azure resource.",
    "Parameters": [
      "resourceName",
      "resourceType"
    ]
  },
  {
    "Name": "GetResourceHealthInfo",
    "Description": "Retrieves detailed health metrics for a specific Azure resource from the graph database. This function is useful when you need to: 1) Check the current health state of a resource, 2) Get performance metrics like CPU, memory usage and availability, 3) Verify if a resource is active or potentially idle, or 4) Get insight into the resource\u0027s performance characteristics. The output provides health state, availability, transaction count, latency, CPU and memory usage when available.",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "GetAKSClusterResourceId",
    "Description": "Get AKS cluster resource ID from subscription, resource group name and AKS cluster name.\r\n        Used whenever user want to access AKS cluster but didn\u0027t specify the resource ID.\r\n        ",
    "Parameters": [
      "Subscription",
      "ResourceGroupName",
      "AKSClusterName"
    ]
  },
  {
    "Name": "GetKubeNamespaces",
    "Description": "Get all namespaces in the Kubernetes cluster.\r\nUsed whenever user want to list namespaces or not specified namespace when asking for resources. eg: list all namespaces in my kubernetes cluster",
    "Parameters": [
      "AKSClusterResourceId"
    ]
  },
  {
    "Name": "GetKubePods",
    "Description": "Get all pods belong to the specific resource and namespace.\r\nUsed whenever user wants to list pods in a specific deployment or statefulset. eg: list all pods in the \u0027nginx-deployment\u0027 in the \u0027default\u0027 namespace.\r\nIf user didn\u0027t specify namespace in the context, try to use \u0027default\u0027 namespace",
    "Parameters": [
      "AKSClusterResourceId",
      "_namespace",
      "kind",
      "name"
    ]
  },
  {
    "Name": "RolloutRestartDeployment",
    "Description": "Restart a deployment in the specified namespace.\r\nUsed whenever user wants to restart or rollout restart a deployment, it can also be used by restart pod if the pod belongs to the deployment.\r\neg: restart the \u0027nginx-deployment\u0027 in the \u0027default\u0027 namespace.\r\nIf user didn\u0027t specify namespace in the context, try to use \u0027default\u0027 namespace",
    "Parameters": [
      "AKSClusterResourceId",
      "_namespace",
      "deploymentName"
    ]
  },
  {
    "Name": "ScaleDeployment",
    "Description": "Scale a deployment in the specified namespace.\r\nUsed whenever user wants to scale a deployment, it can also be used by scale pod if the pod belongs to the deployment.\r\neg: scale the \u0027nginx-deployment\u0027 in the \u0027default\u0027 namespace to 3 replicas.\r\nIf user didn\u0027t specify namespace in the context, try to use \u0027default\u0027 namespace",
    "Parameters": [
      "AKSClusterResourceId",
      "_namespace",
      "deploymentName",
      "replicas",
      "agentmode"
    ]
  },
  {
    "Name": "GetKubePodLogs",
    "Description": "Get the logs of a pod in the specified namespace.\r\nUsed whenever user wants to check the logs of a specific pod.\r\neg: show me the last 100 lines of logs from pod \u0027nginx-pod-xyz\u0027 in the \u0027default\u0027 namespace.\r\nIf user didn\u0027t specify namespace in the context, try to use \u0027default\u0027 namespace",
    "Parameters": [
      "AKSClusterResourceId",
      "_namespace",
      "pod",
      "container",
      "lines"
    ]
  },
  {
    "Name": "ListCRDs",
    "Description": "List all Custom Resource Definitions (CRDs) in the cluster.\r\nUsed whenever user wants to check what custom resources are available in the cluster.\r\neg: show me all CRDs in the cluster",
    "Parameters": [
      "AKSClusterResourceId"
    ]
  },
  {
    "Name": "ListCustomResources",
    "Description": "List custom resource objects in a namespace with specific API group and kind.\r\nUsed whenever user wants to list custom resource objects like Istio VirtualServices, ArgoCD Applications, etc.\r\neg: list all VirtualServices in the \u0027istio-system\u0027 namespace.",
    "Parameters": [
      "AKSClusterResourceId",
      "_namespace",
      "apiGroup",
      "kind"
    ]
  },
  {
    "Name": "GetKubeResourceEvents",
    "Description": "Get the events of a Kubernetes resource (Deployment, StatefulSet, DaemonSet, Pod, Service, Node, PV, or Custom Resource Object) by name.\r\nUsed whenever user wants to check the events or history of a specific resource object.\r\neg: show me the events of the pod \u0027nginx-pod-xyz\u0027 in the \u0027default\u0027 namespace.",
    "Parameters": [
      "AKSClusterResourceId",
      "apiGroup",
      "kind",
      "name",
      "_namespace"
    ]
  },
  {
    "Name": "GetKubeResourceSpecStatus",
    "Description": "Get the YAML spec and status of a Kubernetes resource (Deployment, StatefulSet, DaemonSet, Pod, Service, Node, PV, PVC, or Custom Resource Object) by name.\r\ne.g. show me the YAML spec and status of \u0027my-service\u0027 deployment in the \u0027default\u0027 namespace.\r\ne.g. get spec for node aks-nodepool1-12345678-vmss000000.",
    "Parameters": [
      "AKSClusterResourceId",
      "apiGroup",
      "kind",
      "name",
      "_namespace"
    ]
  },
  {
    "Name": "GetRecentlyUpdatedWorkloads",
    "Description": "Get a list of Kubernetes workloads (Deployments, StatefulSets) that were updated within a specified time frame.\r\nUsed to monitor recent changes or identify workloads that might be related to recent issues.\r\neg: show me all workloads updated in the last 15 minutes.",
    "Parameters": [
      "AKSClusterResourceId",
      "_namespace",
      "minutesAgo"
    ]
  },
  {
    "Name": "ListKubeResources",
    "Description": "Get all Kubernetes resources in the specified namespace with specified kind.\r\nSupported kinds include Deployment, Service, Statefulset, Pod, Job, Configmap, Secret, Ingress, ReplicaSet, Daemonset, and Node.\r\ne.g., \u0027list all deployments in the default namespace\u0027, \u0027list all nodes\u0027.\r\nIt can also be invoked multiple times to list deployments in different namespaces. eg: list all deployments in the \u0027default\u0027 and \u0027kube-system\u0027 namespaces.\r\nIf user didn\u0027t specify namespace in the context, try to use \u0027default\u0027 namespace",
    "Parameters": [
      "AKSClusterResourceId",
      "kind",
      "_namespace"
    ]
  },
  {
    "Name": "ScaleStatefulSet",
    "Description": "Scale a StatefulSet in the specified namespace.\r\nUsed whenever user wants to scale a StatefulSet, it can also be used to scale pods that belong to a StatefulSet.\r\neg: scale the \u0027redis\u0027 StatefulSet in the \u0027default\u0027 namespace to 3 replicas.\r\nIf user didn\u0027t specify namespace in the context, try to use \u0027default\u0027 namespace",
    "Parameters": [
      "AKSClusterResourceId",
      "_namespace",
      "statefulSetName",
      "replicas"
    ]
  },
  {
    "Name": "GetAPIServerStatus",
    "Description": "Get the status of the apiserver for the AKS cluster.\r\nUsed whenever user wants to check the apiserver status of the AKS cluster. Apiserver is the main component of Kubernetes control plane.\r\neg: show me the status of apiserver",
    "Parameters": [
      "AKSClusterResourceId",
      "timeRange"
    ]
  },
  {
    "Name": "GetEtcdStatus",
    "Description": "Get the status of the etcd for the AKS cluster.\r\nUsed whenever user wants to check the etcd status of the AKS cluster. Etcd is the key-value store used by Kubernetes to store all cluster data which is the main component of Kubernetes control plane.\r\neg: show me the status of etcd",
    "Parameters": [
      "AKSClusterResourceId",
      "timeRange"
    ]
  },
  {
    "Name": "DiagnoseAKSApp",
    "Description": "Used to diagnose an AKS application (deployment or statefulset resource) in the specified AKS namespace to get all detailed information belong to the resource.\r\nIt will first get all spec, status, and events of the resource, then get all pods belong to the resource.\r\nFor each pod, it will pod spec, status, events, logs, CPU/Memory metrics to this pod.\r\ne.g.: diagnose the \u0027nginx\u0027 deployment in the \u0027default\u0027 namespace.\r\ne.g.: check what\u0027s wrong with my \u0027redis\u0027 statefulset in the \u0027databse-system\u0027 namespace.\r\n",
    "Parameters": [
      "AKSClusterResourceId",
      "_namespace",
      "kind",
      "name"
    ]
  },
  {
    "Name": "PatchKubernetesYaml",
    "Description": "Applies one Kubernetes YAML object to the specified AKS cluster using server-side apply.\r\n        When patch for array values, make sure all existing values are included in the YAML object.\r\nUsed whenever user wants to create or update resources in a Kubernetes cluster using YAML.\r\neg: please apply this YAML object to my AKS cluster to create a new deployment.\r\neg: update my service with this YAML manifest.",
    "Parameters": [
      "AKSClusterResourceId",
      "yamlContent"
    ]
  },
  {
    "Name": "GetKubeResourceMetricsRange",
    "Description": "Get the value of specific metric for Kubernetes Workload during a time range.\r\nThe supported metrics include cpu, memory, availability percentage.\r\nThe supported workload include deployment, statefulset, pod, and node.\r\neg: please give me the cpu usage rate for deployment flask from 2023-03-01T20:10:30.781Z to 2023-03-20T20:10:30.781Z.\r\neg: please give me the memory usage rate for deployment checkout for last 1 hour.\r\neg: please give me the availability rate for statefulset for last 2 hour.",
    "Parameters": [
      "AKSClusterResourceId",
      "kind",
      "name",
      "metricsType",
      "startTime",
      "endTime",
      "_namespace"
    ]
  },
  {
    "Name": "ListWorkloadRevisions",
    "Description": "List all revisions for a specific Kubernetes workload (Deployment or StatefulSet) and sort by revision number.\r\nFor deployments, it fetches ReplicaSets owned by the deployment.\r\nFor StatefulSets, it fetches ControllerRevision objects.\r\nUsed whenever user wants to check the revision history of a workload.\r\neg: show me all revisions of the \u0027nginx\u0027 deployment in the \u0027default\u0027 namespace.",
    "Parameters": [
      "AKSClusterResourceId",
      "_namespace",
      "kind",
      "name"
    ]
  },
  {
    "Name": "RunKubectlReadCommand",
    "Description": "Safely execute kubectl commands to retrieve Kubernetes resource information. Several subcommands are supported, including \u0027get\u0027, \u0027describe\u0027, \u0027logs\u0027, \u0027top\u0027, \u0027api-resources\u0027, and \u0027api-versions\u0027.\r\nUSAGE: Provide the complete kubectl command as a string.\r\nBASIC EXAMPLES:\r\n- Specific namespace: \u0027kubectl get pods -n production -o name\u0027\r\n- Describe a resource: \u0027kubectl describe pod my-pod -n default\u0027\r\n- Get logs from a pod: \u0027kubectl logs my-pod -n default --container my-container --tail 100\u0027\r\nADVANCED EXAMPLES:\r\n- Complete security info: \u0027kubectl get pods -o custom-columns=NAME:.metadata.name,NAMESPACE:.metadata.namespace,PRIVILEGED:.spec.containers[*].securityContext.privileged,HOST_NETWORK:.spec.hostNetwork,HOST_PID:.spec.hostPID,CAPABILITIES:.spec.containers[*].securityContext.capabilities.add\u0027\r\nBEST PRACTICES:\r\n- Always specify the namespace you care about: \u0027kubectl get pods -n default\u0027",
    "Parameters": [
      "AKSClusterResourceId",
      "command"
    ]
  },
  {
    "Name": "RunKubectlWriteCommand",
    "Description": "Safely execute kubectl commands to update/create/delete Kubernetes resource. Several subcommands are supported, including \u0027create\u0027, \u0027apply\u0027, \u0027delete\u0027, \u0027patch\u0027, \u0027replace\u0027, \u0027scale\u0027, \u0027rollout\u0027, \u0027label\u0027 and \u0027annotate\u0027.\r\nUSAGE: Provide the complete kubectl command as a string.\r\nBASIC EXAMPLES:\r\n- Create a deployment: \u0027kubectl create deployment my-deployment --image=my-image -n production\u0027\r\n- Apply a configuration: \u0027kubectl apply -f my-config.yaml -n default\u0027\r\n- Delete a pod: \u0027kubectl delete pod my-pod -n default\u0027\r\n- Scale a deployment: \u0027kubectl scale deployment my-deployment --replicas=3 -n production\u0027\r\n- Rollout restart a deployment: \u0027kubectl rollout restart deployment my-deployment -n default\u0027\r\n- Patch a resource: \u0027kubectl patch deployment my-deployment -p \\\u0022{\\\u0022spec\\\u0022:{\\\u0022replicas\\\u0022:3}}\\\u0022 -n default\u0027\r\n- Label a resource: \u0027kubectl label pod my-pod my-label=my-value -n default\u0027\r\nBEST PRACTICES:\r\n- Always specify the namespace you care about: \u0027kubectl get pods -n default\u0027",
    "Parameters": [
      "AKSClusterResourceId",
      "command",
      "stdin"
    ]
  },
  {
    "Name": "RunKubectlCommandHelp",
    "Description": "Provides help information about kubectl commands and resources.\r\n            Used whenever user needs guidance on using kubectl commands or understanding Kubernetes resources.\r\n            eg: \u0027How do I use kubectl get pods?\u0027, \u0027What options are available for kubectl describe?\u0027.",
    "Parameters": [
      "AKSClusterResourceId",
      "command"
    ]
  },
  {
    "Name": "KubectlGet",
    "Description": "Retrieve Kubernetes resources with optional label filtering and custom columns.",
    "Parameters": [
      "AKSClusterResourceId",
      "kind",
      "namespace",
      "selector",
      "columnsCsv"
    ]
  },
  {
    "Name": "KubectlDescribe",
    "Description": "Run \u0027kubectl describe\u0027 on a single object. Must specify kind, name, and namespace (or empty for cluster\u2011scoped kinds).",
    "Parameters": [
      "AKSClusterResourceId",
      "kind",
      "name",
      "namespace"
    ]
  },
  {
    "Name": "KubectlExplain",
    "Description": "Run \u0027kubectl explain\u0027 for API documentation. Always specify full resourcePath (e.g. \u0027pod.spec.containers\u0027) and whether recursion is desired.",
    "Parameters": [
      "AKSClusterResourceId",
      "resourcePath",
      "recursive",
      "apiVersion"
    ]
  },
  {
    "Name": "KubeApiResources",
    "Description": "Run \u0027kubectl api-resources\u0027 with optional filters and explicit output columns.",
    "Parameters": [
      "AKSClusterResourceId",
      "namespaced",
      "apiGroup"
    ]
  },
  {
    "Name": "GetPodLogs",
    "Description": "Retrieve Kubernetes pod logs with grep filtering, truncation, and all built-in kubectl log options.",
    "Parameters": [
      "AKSClusterResourceId",
      "podOrResource",
      "namespace",
      "container",
      "grepTerms",
      "caseSensitive",
      "tailLines",
      "since",
      "timestamps",
      "previous",
      "allContainers",
      "showPrefix"
    ]
  },
  {
    "Name": "GetKubeEvents",
    "Description": "Retrieve Kubernetes events with grep filtering, truncation, and built-in event filtering options.",
    "Parameters": [
      "AKSClusterResourceId",
      "namespace",
      "fieldSelector",
      "grepTerms",
      "caseSensitive",
      "sortBy",
      "eventTypes"
    ]
  },
  {
    "Name": "DiscoverPrometheusMetrics",
    "Description": "Discover available Prometheus metrics with optional filtering.",
    "Parameters": [
      "AKSClusterResourceId",
      "namePattern",
      "metricType"
    ]
  },
  {
    "Name": "GetMetricsLabels",
    "Description": "Discover available label names and values for a specific metric to build more targeted queries.",
    "Parameters": [
      "AKSClusterResourceId",
      "metricName",
      "labelName"
    ]
  },
  {
    "Name": "QueryPrometheusMetrics",
    "Description": "Query Prometheus metrics with comprehensive filtering and aggregation options to control output volume.",
    "Parameters": [
      "AKSClusterResourceId",
      "query",
      "duration",
      "step",
      "labelFilters",
      "aggregateFunction",
      "aggregateBy",
      "limit",
      "minValue"
    ]
  },
  {
    "Name": "ProfileDotnetAppCpuInAKSContainer",
    "Description": "Performs CPU profiling for a .NET application running in a specific pod and container.\r\n            The analysis (\u0027topN\u0027 report) is also performed inside the container, and its result is returned.\r\n            Failures during tool installation or profiling will be reported in the output.\r\n            eg: \u0027Profile CPU of \u0027my-app-pod\u0027 in \u0027default\u0027 for 60s.\u0027",
    "Parameters": [
      "AKSClusterResourceId",
      "_namespace",
      "podName",
      "targetContainerName",
      "durationSeconds"
    ]
  },
  {
    "Name": "AnalyzeDotnetAppMemoryInAKSContainer",
    "Description": "Performs memory analysis for a .NET application running in a specific pod and container within an AKS cluster.\r\n    This involves collecting a memory dump, running an analyzer tool inside the container, and returning the analysis results.\r\n    This tool can help identify memory leaks, high memory usage patterns, and other memory-related issues in .NET applications.\r\n    Use this when investigating memory problems for a .NET app in AKS.\r\n    eg: \u0027Analyze the memory of the .NET app in pod \u0027cart-service-pod-abc789\u0027 in the \u0027e-commerce\u0027 namespace.\u0027\r\n    eg: \u0027My .NET app \u0027order-processor\u0027 in pod \u0027proc-pod-123\u0027 seems to be using too much memory, can you analyze it?\u0027",
    "Parameters": [
      "AKSClusterResourceId",
      "_namespace",
      "podName",
      "targetContainerName"
    ]
  },
  {
    "Name": "StartGetWebAppCpuMetrics",
    "Description": "Start a background task to get the average CPU utilization metrics of a specific WebApp instance at per minute granularity for the past 30 minutes, WebApp is healthy if over half of the data points is less than 80% CPU utilization, zero metric value doesn\u0027t indicate the app is unhealthy",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "GetWebAppCpuMetrics",
    "Description": "Get the average CPU utilization metrics of a specific WebApp instance at per minute granularity for the past 30 minutes, WebApp is healthy if over half of the data points is less than 80% CPU utilization, zero metric value doesn\u0027t indicate the app is unhealthy",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "GetSuccessfulRequestVolume",
    "Description": "Get the 2XX request volume of a specific resource at per minute granularity",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "GetFunctionAppRequestAvailability",
    "Description": "Get the request availability of a specific FunctionApp (DO NOT CALL FOR FLEX or CONSUMPTION SKU) at per minute granularity for the past 30 minutes, FunctionApp is healthy if all data points are at least 99.9 availability",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "GetMemoryMetrics",
    "Description": "Get the average memory utilization metrics of a specific WebApp or FunctionApp instance at per minute granularity for the past 30 minutes, WebApp is healthy if over half of the data points is less than 80% memory utilization.",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "StartGetMemoryMetrics",
    "Description": "Start a background operation to get the average memory utilization metrics of a specific WebApp or FunctionApp instance at per minute granularity for the past 30 minutes, WebApp is healthy if over half of the data points is less than 80% memory utilization.",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "GetThreadMetrics",
    "Description": "Get the average thread count metrics of a web app",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "ExecuteClusterKustoQuery",
    "Description": "Executes a fully qualified Kusto query on a specific cluster and database, returning the result in JSON format.",
    "Parameters": [
      "cluster",
      "database",
      "fullQuery"
    ]
  },
  {
    "Name": "HandoffBack",
    "Description": "Handoff the current context to the upper level agent when the current request is out of your current scope.\r\n        Do not use this tool if there are other appropriate handoff tools available.\r\n        Use this tool when you do not have any other tools or handoffs to properly handle the current task.",
    "Parameters": []
  },
  {
    "Name": "GetAPIManagementInfo",
    "Description": "PREFERRED METHOD FOR API MANAGEMENT DETAILS: Gets detailed information about a specific Azure API Management instance by its resource ID. Returns an APIManagementDescriptor with the following properties: ResourceId, Name, Type, Location, ResourceGroup, PublisherEmail, PublisherName, SkuName, VirtualNetworkConfiguration, GatewayUri, GatewayRegionalUri, HostnameConfigurations, PublicIPAddresses, PrivateIPAddresses, VirtualNetworkType, PublicNetworkAccess, CustomProperties, Certificates, EnableClientCertificate, ProvisioningState, PlatformVersion, DeveloperPortalUri, DeveloperPortalStatus, PortalUri, ScmUri, ManagementApiUri, and CreatedAtUtc. Always use this specialized method for API Management instances instead of generic resource search functions for more complete and accurate information. For metrics and usage information (such as requests, throughput, errors, cost, etc.), format the output in markdown tabular format.",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "ListAPIManagement",
    "Description": "PREFERRED METHOD FOR API MANAGEMENT RESOURCES: Lists all Azure API Management resources in the specified subscription. Returns a string of APIManagementDescriptors, each with the following properties: ResourceId, Name, Type, Location, ResourceGroup, and PublisherEmail. These exact properties are returned to the customer for each API Management resource. This is the most direct and efficient way to get API Management resource information - use this instead of generic resource search methods. Returns an empty list if no API Management resources are found.",
    "Parameters": [
      "subscriptionId"
    ]
  },
  {
    "Name": "GetAPIMErrorLogs",
    "Description": "Retrieves recent failed requests (non-successful) from an Azure API Management instance using connected Application Insights. Supports optional filtering by status code and allows specifying how many results to return. You can specify a time window using startDaysAgo/endDaysAgo (relative to now, in days). If neither is provided, defaults to the past 5 days up to 0 days ago.",
    "Parameters": [
      "apiManagementResourceId",
      "statusCode",
      "top",
      "startDaysAgo",
      "endDaysAgo"
    ]
  },
  {
    "Name": "GetAPIMActivityLogs",
    "Description": "Retrieves the management activity (changes, deploymenents, admin actions) logs for a specified Azure API Management instance over the past 7 days. Returns a markdown table with columns: Timestamp, Operation, Event, Status, URI, Caller. This method queries Azure Monitor\u0027s management event logs for the resource. Use this to audit changes, deployments, or administrative actions on the API Management instance. If startTime and endTime are not provided, defaults to the past 2 days. Pass in the datetimes as parameters to override the default window.",
    "Parameters": [
      "apiManagementResourceId",
      "startDaysAgo",
      "endDaysAgo"
    ]
  },
  {
    "Name": "GetAPIMFailureRateByApiOperation",
    "Description": "Calculates the failure rate for each API operation over a specified time range using relative days. startDaysAgo and endDaysAgo are optional integers relative to now (e.g., 3 and 0 means from 3 days ago to now). If not provided, startDaysAgo defaults to 3 and endDaysAgo defaults to 0. Returns a markdown table with columns: ApiId, OperationId, ResponseCode, LastErrorReason, TotalCount, FailedCount, FailureRatePercent.",
    "Parameters": [
      "apiManagementResourceId",
      "startDaysAgo",
      "endDaysAgo"
    ]
  },
  {
    "Name": "GetAPIMRecentFailedRequests",
    "Description": "Retrieves the most recent failed requests (up to a specified limit) with full request/response details. Defaults to the past 24 hours and top 10 results if no parameters are provided. Returns a markdown table with columns: TimeGenerated, CorrelationId, ApiId, OperationId, Url, Method, CallerIpAddress, ResponseCode, LastErrorReason, LastErrorMessage, RequestSize, ResponseSize, RequestHeaders, ResponseHeaders, RequestBody, ResponseBody.",
    "Parameters": [
      "apiManagementResourceId",
      "lookbackHours",
      "topN"
    ]
  },
  {
    "Name": "GetAPIMApis",
    "Description": "Retrieves the list of APIs defined in the specified Azure API Management instance. Returns a markdown table with columns: ApiId, Name, Description, Path, Protocols, ServiceUrl. This method queries the API Management service for its defined APIs.",
    "Parameters": [
      "apiManagementResourceId",
      "workspaceName"
    ]
  },
  {
    "Name": "GetAPIDetailsByName",
    "Description": "Retrieves detailed information about a specific API in the Azure API Management instance by its name. Returns an APIManagementApiDescriptor with properties like Id, Name, Type, and detailed properties including display name, revision, description, subscription requirements, service URL, backend ID, path, protocols, authentication settings, and subscription key parameter names. This method queries the API Management service for the specified API.",
    "Parameters": [
      "apiManagementResourceId",
      "apiName",
      "workspaceName"
    ]
  },
  {
    "Name": "GetAPIOperationsByApi",
    "Description": "Retrieves the list of operations for a specific API in the Azure API Management instance. Returns a markdown table with columns: OperationId, Name, Description, Method, UrlTemplate, ResponseCodes. This method queries the API Management service for operations defined under the specified API.",
    "Parameters": [
      "apiManagementResourceId",
      "apiName",
      "workspaceName"
    ]
  },
  {
    "Name": "GetAPIOperationDetailedInfo",
    "Description": "Retrieves detailed information about a specific operation in an API within the Azure API Management instance. Returns a markdown table with columns: OperationId, Name, Policies, Method, Responses, Properties, etc. This method queries the API Management service for detailed operation information.",
    "Parameters": [
      "apiManagementResourceId",
      "apiName",
      "operationName",
      "workspaceName"
    ]
  },
  {
    "Name": "GetCallStackForApp",
    "Description": "This function attempts to retrieve the stack traces for a user\u0027s particular app",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "WaitInMilliSeconds",
    "Description": "This function forces a delay for the application to trigger a wait",
    "Parameters": [
      "numMilliSeconds"
    ]
  },
  {
    "Name": "GetSummaryOfExceptions",
    "Description": "This function retrieves the summary of the exceptions on the app",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "GetStackTraceOfLastException",
    "Description": "This function retrieves the stack trace of the most recent exception",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "GetStackTraceOfMostCommonException",
    "Description": "This function retrieves the stack trace of the most recent exception",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "GetStackTracesOfNMostCommonExceptions",
    "Description": "This function retrieves the stack traces of the n most common app exceptions",
    "Parameters": [
      "resourceId",
      "num"
    ]
  },
  {
    "Name": "PerformDeploymentSwapForApp",
    "Description": "Performs a Deployment Swap for the specified app.",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "GetDeploymentActivity",
    "Description": "Gets Deployment Activities on the specified app",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "GetAppConsoleLogs",
    "Description": "This function attempts to retrieve error messages in the console logs and platform logs from a user\u0027s particular app",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "GetWebAppDownAnalysisLink",
    "Description": "This function retrieves the link to the Applens web app down analysis",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "GetContainerAppInfo",
    "Description": "PREFERRED METHOD FOR CONTAINER APP DETAILS: Gets detailed information about a specific Azure Container App by its resource ID. Returns a ContainerAppDescriptor with resource ID, name, location, state, workload profile, FQDN, AppHealthInfo, and environment details. Always use this specialized method for Container Apps instead of generic resource search functions for more complete and accurate information.For the AppHealthInfo information (such requests, cpu, and memory metrics, cost etc. format the output in markdown tabular format.",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "ListRevisions",
    "Description": "List all revisions for a container app by its resource ID.",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "GetLatestRevision",
    "Description": "Get the latest active revision for a Container App instance",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "ListContainerApps",
    "Description": "PREFERRED METHOD FOR CONTAINER APPS: Lists all Azure Container Apps in the specified subscription. Returns detailed ContainerAppDescriptor objects with resource ID, name, location, state, workload profile, FQDN, and environment details. This is the most direct and efficient way to get Container App information - use this instead of generic resource search methods. Returns an empty list if no Container Apps are found.",
    "Parameters": [
      "subscriptionId"
    ]
  },
  {
    "Name": "RestartContainerApp",
    "Description": "Restarts a container app. Use this to restart a container app to resolve transient issues that may be fixed by restarting the instance.",
    "Parameters": [
      "appResourceId",
      "revisionName"
    ]
  },
  {
    "Name": "GetContainerAppRequestMetrics",
    "Description": "Start a background operation to get the total request count metrics of a specific Container App instance at per minute granularity for the past 30 minutes, Container App is healthy if all data points are at least 99.9 availability.",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "GetContainerAppMemoryMetrics",
    "Description": "Start a background operation to get the average memory usage of a specific Container App instance at per minute granularity for the past 30 minutes, Container App is healthy if over half of the data points is less than 20% memory utilization.",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "IsContainerAppDotnet",
    "Description": "Start a background operation to check if the container app is dotnet based.",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "GetContainerMemoryAnalysisForDotnet",
    "Description": "Start a background operation to get an in-depth memory analysis for .NET Apps of the App instance. This remediation measure is in the case of high memory load or if the user requests it. This should be executed if there are memory related issues without asking the user.",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "GetContainerAppCpuMetrics",
    "Description": "Get the average CPU utilization metrics of a specific Container App instance at per minute granularity for the past 30 minutes, Container App is healthy if over half of the data points is less than 80% CPU utilization, zero metric value doesn\u0027t indicate the app is unhealthy",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "GetAllNSGRulesForContainerApp",
    "Description": "Retrieves all Network Security Groups (NSGs) associated with a Container App and their security rules. Returns a dictionary where keys are NSG resource IDs and values are lists of security rules. Use this to identify network access issues or restrictive rules that might be blocking traffic to/from the Container App.",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "ScaleContainerApp",
    "Description": "Scales a Container App by adjusting its memory allocation and replica count. Use this to resolve performance or availability issues by increasing resources or scaling out the application.",
    "Parameters": [
      "resourceId",
      "desiredMemory",
      "minReplicas",
      "maxReplicas"
    ]
  },
  {
    "Name": "ModifyContainerAppScaleRule",
    "Description": "Adds a new scaling rule to a Container App. Use this to define custom scaling behavior based on CPU, HTTP traffic, Azure Queue length, or any scaler from the scaler list.",
    "Parameters": [
      "resourceId",
      "ruleName",
      "modificationType",
      "scaleRuleType",
      "metadata"
    ]
  },
  {
    "Name": "GetRevisionLogs",
    "Description": "Get the logs of a specific revision of a Container App instance.",
    "Parameters": [
      "resourceId",
      "revisionName"
    ]
  },
  {
    "Name": "GetContainerAppLogs",
    "Description": "Get the logs of the latest revision of a Container App instance.",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "UpdateTargetPort",
    "Description": "Update the target port of a Container App instance.",
    "Parameters": [
      "resourceId",
      "targetPort"
    ]
  },
  {
    "Name": "ListAvailableScalers",
    "Description": "List available scaler names",
    "Parameters": []
  },
  {
    "Name": "GetScalerDetails",
    "Description": "Get the details of a specific scaler for a Container App instance.",
    "Parameters": [
      "scalerName"
    ]
  },
  {
    "Name": "GetImageReferenceFromResourceId",
    "Description": "Gets the container image reference from a resource ID",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "VerifyExternalRegistry",
    "Description": "Verify connectivity to an external container registry. This is useful for checking if the Container App can pull images from the specified registry.",
    "Parameters": [
      "resourceId",
      "imageReference"
    ]
  },
  {
    "Name": "RollbackToLastKnownWorkingRevision",
    "Description": "Rolls back a Container App to the last known working revision. This is useful when a new image deployment causes image pull failures. Returns detailed information about the rollback operation including success status, target revision, and reasons for failure if applicable. Note that this tool requires explicit user\u0027s approval before it can be used.",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "UpdateContainerImage",
    "Description": "Updates the container image for a Container App. This enables changing to a new image version or completely different image. Returns detailed information about the update operation including success status, original image, new image, and reasons for failure if applicable. Note that this tool requires explicit user\u0027s approval before it can be used.",
    "Parameters": [
      "resourceId",
      "newImageReference"
    ]
  },
  {
    "Name": "ValidateContainerAppHealth",
    "Description": "Validates if a Container App is healthy by checking various health indicators including provisioning state, revision status, logs, and endpoint reachability. Use this after making remediation changes to verify the app is working correctly.",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "GetDeploymentTimes",
    "Description": "Get the deployment times of a Container App instance.",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "GetAnalysis",
    "Description": "Gets the analysis for a particular compute resource based on a particular resourceId and analysis type.",
    "Parameters": [
      "resourceId",
      "analysisType",
      "additionalProperties"
    ]
  },
  {
    "Name": "GetLatencyAnalysis",
    "Description": "Gets the latency analysis for a particular compute resource based on a particular resourceId and analysis type.",
    "Parameters": [
      "resourceId",
      "additionalProperties"
    ]
  },
  {
    "Name": "GetCPUAnalysis",
    "Description": "Gets the CPU analysis for a particular compute resource for high cpu situations or situations with cpu spikes or can be independently asked for by the user. Example 1: \u0027My app\u0027s CPU is extremely high - analyze to see what\u0027s going on\u0027 Example 2: \u0027My app is experiencing 500s and I see a spike in CPU. Help me figure out what\u0027s going on\u0027Example 3: \u0027My app is down and I see a spike in CPU. Help me figure out what\u0027s going on\u0027 Keywords: Deep Diagnostic CPU, High CPU, CPU Analysis.",
    "Parameters": [
      "resourceId",
      "additionalProperties"
    ]
  },
  {
    "Name": "GetMemoryAnalysis",
    "Description": "Gets the Memory analysis for a particular compute resource for high memory situations or situations with memory spikes or can be independently asked for by the user. Example 1: \u0027My app\u0027s Memory is extremely high - analyze to see what\u0027s going on\u0027 Example 2: \u0027My app is experiencing 500s and I see a spike in Memory. Help me figure out what\u0027s going on\u0027Example 3: \u0027My app is down and I see a spike in Memory. Help me figure out what\u0027s going on\u0027Keywords: Deep Diagnostics Memory, High Memory, Memory Analysis.",
    "Parameters": [
      "resourceId",
      "additionalProperties"
    ]
  },
  {
    "Name": "GetFunctionAppConfigurationChecks",
    "Description": "Gets Function App configuration checks to identify potential issues in the Function App configuration. Analyzes settings like runtime version, extension version, platform, and other configuration values. Returns detailed analysis with potential issues and recommendations for optimization.",
    "Parameters": [
      "resourceId",
      "startTime",
      "endTime"
    ]
  },
  {
    "Name": "GetEventGridSubscriptions",
    "Description": "Gets Event Grid subscriptions associated with a storage account used by a Function App. Returns detailed information about each subscription including endpoint, filter criteria, and retry policy.",
    "Parameters": [
      "storageAccountResourceId"
    ]
  },
  {
    "Name": "GetFunctionAppDeploymentChecks",
    "Description": "Gets Function App deployment information to identify potential deployment issues. Analyzes deployment history, source control information, deployment methods, and other deployment-related metrics. Returns detailed analysis with potential deployment issues and recommendations.",
    "Parameters": [
      "resourceId",
      "startTime",
      "endTime"
    ]
  },
  {
    "Name": "GetFunctionAppDeploymentHistory",
    "Description": "Gets detailed Function App deployment history to track all deployment activities. Retrieves chronological deployment records, including deployment source, trigger, status, and timestamps. Returns comprehensive deployment timeline with success/failure information.",
    "Parameters": [
      "resourceId",
      "startTime",
      "endTime"
    ]
  },
  {
    "Name": "GetFunctionAppSlotSwapHistory",
    "Description": "Gets detailed Function App slot swap information to analyze swap operations. Retrieves history of slot swaps including timestamp, source and target slots, and status. Returns comprehensive history of swap operations to troubleshoot deployment and availability issues.",
    "Parameters": [
      "resourceId",
      "startTime",
      "endTime"
    ]
  },
  {
    "Name": "GetFunctionAppDeploymentFailureAnalysis",
    "Description": "Gets in-depth analysis of deployment failures for Windows Function Apps. Analyzes deployment logs, identifies common failure patterns, and provides detailed diagnostics. Returns comprehensive deployment failure analysis with root cause identification and suggested remediation steps. Note: This tool only works for Windows Function Apps.",
    "Parameters": [
      "resourceId",
      "startTime",
      "endTime"
    ]
  },
  {
    "Name": "VerifyZipFileExists",
    "Description": "Verifies if a zip file exists in Azure Storage. Checks if the specified zip file is accessible from its URL location. If no zip file path is provided, retrieves the path from the WEBSITE_RUN_FROM_PACKAGE app setting. Returns verification status, path information, and details about the file if it exists.",
    "Parameters": [
      "resourceId",
      "zipFilePath"
    ]
  },
  {
    "Name": "UpdateWebsiteRunFromPackage",
    "Description": "Updates the WEBSITE_RUN_FROM_PACKAGE app setting to point to a new zip file in Azure Storage. Validates the provided zip file path, verifies that the file exists in Azure Storage, renames the existing WEBSITE_RUN_FROM_PACKAGE to SREAGENT_RENAMED_WEBSITE_RUN_FROM_PACKAGE, and creates a new WEBSITE_RUN_FROM_PACKAGE with the provided value. Returns details about the update operation including success status and error information if applicable.",
    "Parameters": [
      "resourceId",
      "zipFilePath"
    ]
  },
  {
    "Name": "GetFunctionAppExecutionFailures",
    "Description": "Gets a summary of execution failures for an Azure Function App. Do not call for FlexConsumption SKU",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "GetFunctionAppCallStacks",
    "Description": "Gets call stack information for Azure Function App executions",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "GetFailedFunctionInvocations",
    "Description": "Gets a summary of failed invocations grouped by function for an Azure Function App",
    "Parameters": [
      "resourceId",
      "minutes"
    ]
  },
  {
    "Name": "GetTop3ExceptionsPerFunction",
    "Description": "Gets the top 3 exceptions grouped by function for an Azure Function App",
    "Parameters": [
      "resourceId",
      "startTime",
      "endTime"
    ]
  },
  {
    "Name": "GetHostRuntimeErrorEvents",
    "Description": "Gets host runtime error events from the activity logs for an Azure Function App",
    "Parameters": [
      "resourceId",
      "startTime",
      "endTime"
    ]
  },
  {
    "Name": "IsFunctionApp",
    "Description": "Checks if a resource is a Function App by verifying its \u0027kind\u0027 property contains \u0027functionapp\u0027",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "HasHostRuntimeErrors",
    "Description": "Checks if a Function App has host runtime related errors in its activity logs",
    "Parameters": [
      "resourceId",
      "startTime",
      "endTime"
    ]
  },
  {
    "Name": "TriggerFunctionAppSync",
    "Description": "Triggers a sync operation on a Function App\u0027s host to check for runtime errors or refresh the function app",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "ListFunctionApps",
    "Description": "PREFERRED METHOD FOR FUNCTION APPS: Lists all Azure Function Apps in the specified subscription. Returns detailed FunctionAppDescriptor objects containing resource ID, name, kind, location, SKU, state, resource group, and runtime details. This is the most direct and efficient way to get Function App information. Use this instead of generic resource search methods. Returns an empty list if no Function Apps are found or if the subscription doesn\u0027t exist.",
    "Parameters": [
      "subscriptionId"
    ]
  },
  {
    "Name": "GetFunctionAppInfo",
    "Description": "PREFERRED METHOD FOR FUNCTION APP DETAILS: Gets detailed information about a specific Azure Function App by its resource ID. Returns a FunctionAppDescriptor with resource ID, name, kind, location, SKU, state, resource group, and runtime details. Always use this specialized method for Function Apps instead of generic resource search functions for more complete and accurate information.",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "GetIncidentInfo",
    "Description": "Get ICM incident details",
    "Parameters": [
      "incidentId"
    ]
  },
  {
    "Name": "GetCustomFields",
    "Description": "Get ICM incident custom fields",
    "Parameters": [
      "incidentId"
    ]
  },
  {
    "Name": "SearchIncidents",
    "Description": "Search for incidents and returns matching incidents with details like CreatedDateTime, Id, Title etc.",
    "Parameters": [
      "searchString",
      "lookbackPeriodInDays",
      "resultCountLimit"
    ]
  },
  {
    "Name": "GetCurrentUtcDateTime",
    "Description": "Get current UTC date and time",
    "Parameters": []
  },
  {
    "Name": "GetIcmCorrelationAndLinkingRules",
    "Description": "This tool identifies potential relationships between incidents. Invoke this tool whenever the user requests assistance with finding related, parent, or child incidents; especially when conditions such as time windows, title matching, or shared patterns are specified. The rules are applied internally to guide the agent\u0027s actions without being returned to the user.",
    "Parameters": []
  },
  {
    "Name": "GetAlertingDiscussionEntry",
    "Description": "Get Azure Alerting discussion entry",
    "Parameters": [
      "incidentId"
    ]
  },
  {
    "Name": "GetDiscussionEntries",
    "Description": "Get ICM discussion entries",
    "Parameters": [
      "incidentId"
    ]
  },
  {
    "Name": "TransferIncident",
    "Description": "Transfer ICM incident",
    "Parameters": [
      "incidentId",
      "discussionEntry",
      "tenantName",
      "owningTeam"
    ]
  },
  {
    "Name": "MitigateIncident",
    "Description": "Mitigate ICM incident",
    "Parameters": [
      "incidentId",
      "discussionEntry"
    ]
  },
  {
    "Name": "DowngradeSeverity",
    "Description": "Downgrade severity of ICM incident 2 to 3",
    "Parameters": [
      "incidentId",
      "discussionEntry"
    ]
  },
  {
    "Name": "ResolveIncident",
    "Description": "Resolve ICM incident",
    "Parameters": [
      "incidentId",
      "discussionEntry"
    ]
  },
  {
    "Name": "PostDiscussionEntry",
    "Description": "Post ICM discussion entry",
    "Parameters": [
      "incidentId",
      "discussionEntry"
    ]
  },
  {
    "Name": "AddTagToIncident",
    "Description": "Add a tag to an ICM incident",
    "Parameters": [
      "incidentId",
      "tag"
    ]
  },
  {
    "Name": "AddKeywordToIncident",
    "Description": "Add a keyword to an ICM incident",
    "Parameters": [
      "incidentId",
      "keyword"
    ]
  },
  {
    "Name": "AcknowledgeIncident",
    "Description": "Acknowledges an ICM incident",
    "Parameters": [
      "incidentId"
    ]
  },
  {
    "Name": "GetIncidentRepairItems",
    "Description": "Get repair items associated with an ICM incident",
    "Parameters": [
      "incidentId"
    ]
  },
  {
    "Name": "GetLinkedRelatedIncidentInfo",
    "Description": "\u200BGets basic info for all the linked incidents maked as related and associated with the given incident id",
    "Parameters": [
      "incidentId"
    ]
  },
  {
    "Name": "AddRelatedIncidentLink",
    "Description": "Adds a related incident link to the given incident id",
    "Parameters": [
      "incidentId",
      "relatedIncidentId"
    ]
  },
  {
    "Name": "RemoveRelatedIncidentLink",
    "Description": "Removes a related incident link from the given incident id",
    "Parameters": [
      "incidentId",
      "relatedIncidentId"
    ]
  },
  {
    "Name": "GetParentIncidentInfo",
    "Description": "\u200BGets basic info of the parent incident associated with the given incident id",
    "Parameters": [
      "incidentId"
    ]
  },
  {
    "Name": "AddParentIncidentLink",
    "Description": "Adds a parent incident link to the given incident id",
    "Parameters": [
      "incidentId",
      "parentIncidentId"
    ]
  },
  {
    "Name": "RemoveParentIncidentLink",
    "Description": "Removes a parent incident link from the given incident id",
    "Parameters": [
      "incidentId"
    ]
  },
  {
    "Name": "GetChildIncidentsInfo",
    "Description": "\u200BGets basic info for all the child incidents associated with the given incident id",
    "Parameters": [
      "incidentId"
    ]
  },
  {
    "Name": "GetNSGRules",
    "Description": "Retrieves the rules for a given NSG, both security and default security rules. Use this to understand the current network access permissions and identify any potential issues. Note: DefaultSecurityRules can only be updated/removed by the network administrator and they override rules configured in SecurityRules.",
    "Parameters": [
      "nsgResourceId"
    ]
  },
  {
    "Name": "CreateOrUpdateNSGRule",
    "Description": "Creates a new NSG rule or updates an existing one to modify network access permissions. Use this to fix connectivity issues by allowing necessary traffic or blocking unwanted traffic.",
    "Parameters": [
      "nsgResourceId",
      "rule"
    ]
  },
  {
    "Name": "RemoveNSGRule",
    "Description": "Removes an existing NSG rule. Use this to eliminate overly restrictive or unnecessary security rules.",
    "Parameters": [
      "nsgResourceId",
      "ruleName"
    ]
  },
  {
    "Name": "GetPagerDutyIncidents",
    "Description": "Gets latest PagerDuty incidents related to an Azure resource.",
    "Parameters": [
      "resourceId",
      "maxResults"
    ]
  },
  {
    "Name": "ResolvePagerDutyIncident",
    "Description": "Resolves a PagerDuty incident",
    "Parameters": [
      "incidentId"
    ]
  },
  {
    "Name": "AcknowledgePagerDutyIncident",
    "Description": "Acknowledges a PagerDuty incident",
    "Parameters": [
      "incidentId"
    ]
  },
  {
    "Name": "CloseAzureMonitorAlert",
    "Description": "Closes an Azure Monitor alert thread by marking it as closed. This can be used to close an alert thread that is no longer active.",
    "Parameters": [
      "alertId"
    ]
  },
  {
    "Name": "GetK4appsHelmChartUpgradeTimes",
    "Description": "Get the times for k4apps helm chart upgrades",
    "Parameters": [
      "fromDate",
      "toDate",
      "region",
      "managedClusterName"
    ]
  },
  {
    "Name": "GetAksNodeImageUpgradeTimes",
    "Description": "Get the times for AKS Node Image Upgrades",
    "Parameters": [
      "fromDate",
      "toDate",
      "region",
      "managedClusterName"
    ]
  },
  {
    "Name": "GetLegionHostRoleOSUpgradeTimes",
    "Description": "Get the times for AKS Node Image Upgrades",
    "Parameters": [
      "fromDate",
      "toDate",
      "region",
      "managedClusterName",
      "revisionName"
    ]
  },
  {
    "Name": "GetCustomDNSServers",
    "Description": "Get list of custom DNS servers configured for the container app environment at start and end of time window. It also checks if custom DNS servers are configured or not.\r\n            If no data is returned then ask to validate inputs again as it should never be the case.",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName"
    ]
  },
  {
    "Name": "GetUpstreamCustomDNSServerHealthStatus",
    "Description": "\r\n                Retrieve the health status of upstream custom DNS servers for a given managed Kubernetes cluster, segmented by node or VMSS, within a specified time range.\r\n                If the query returns results, it indicates that the corresponding upstream DNS server experienced health check failures (i.e., it is unhealthy).\r\n\r\n                What this metric measures:  If no results are returned, the upstream DNS server is considered healthy during that time frame.\r\n                When it is applicable: CoreDNS could not reach upstream DNS servers \r\n            ",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName"
    ]
  },
  {
    "Name": "GetAverageLatencyOfDNSResolutionRequests",
    "Description": "\r\n                Retrieve the average latency (in seconds) of DNS resolution requests handled by CoreDNS within a given managed Kubernetes cluster, segmented by node or VMSS over a specified time range.\r\n                The query calculates the average time (in seconds) CoreDNS takes to resolve DNS queries by dividing the total duration of all DNS requests by the total number of requests.\r\n                This metric is useful for identifying performance degradation or latency spikes in DNS resolution.\r\n\r\n                What this metric measures: Measures total time CoreDNS takes to serve any DNS request, regardless of whether it uses cache, forwards it, or uses plugins. End-to-end latency from the client\u0027s perspective.\r\n                When it is applicable: Helps detect increased DNS resolution latency, which may impact application performance or indicate upstream DNS server slowness.\r\n            ",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName"
    ]
  },
  {
    "Name": "GetAverageLatencyOfUpstreamDNSResolutionForwardRequests",
    "Description": "\r\n                Retrieve the average forwarding latency (in seconds) of DNS resolution requests handled by CoreDNS within a managed Kubernetes cluster, segmented by node or VMSS over a specified time range.\r\n                The query calculates the average time (in seconds) CoreDNS takes to forward DNS queries to upstream servers and receive responses.\r\n\r\n                What this metric measures: Measures only the forwarding time, how long CoreDNS takes to send a DNS request to an upstream DNS server and receive a response.\r\n                When it is applicable: Helps detect increased DNS resolution latency, which may indicate upstream DNS server slowness or network issues.\r\n            ",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName"
    ]
  },
  {
    "Name": "GetCoreDNSProcessCrashesCount",
    "Description": "\r\n        Retrieve the number of panic events triggered by the CoreDNS process within a managed Kubernetes cluster, segmented by node or VMSS over a specified time range.\r\n        The query counts how many times CoreDNS encountered a runtime panic, which may result in process crashes or restarts.\r\n\r\n        What this metric measures:\r\n        Tracks the total number of CoreDNS panics caused by unexpected failures such as plugin bugs or misconfigurations.\r\n\r\n        When it is applicable:\r\n        Useful for identifying critical issues affecting CoreDNS stability that may lead to DNS resolution failures or service interruptions.\r\n                ",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName"
    ]
  },
  {
    "Name": "GetCoreDNSConfigReloadFailuresCount",
    "Description": "\r\n        Retrieve the number of failed CoreDNS configuration reload attempts within a managed Kubernetes cluster, segmented by node or VMSS over a specified time range.\r\n        The query counts how often CoreDNS failed to apply a new configuration, which can impact DNS functionality.\r\n\r\n        What this metric measures:\r\n        Tracks the total number of times CoreDNS attempted but failed to reload its configuration.\r\n\r\n        When it is applicable:\r\n        Useful for detecting configuration issues or malformed updates that may prevent CoreDNS from functioning correctly after changes.\r\n                ",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName"
    ]
  },
  {
    "Name": "GetCoreDNSTotalDNSRequestCount",
    "Description": "\r\n        Retrieve the total number of DNS requests handled by CoreDNS within a managed Kubernetes cluster, segmented by node or VMSS over a specified time range.\r\n        This query helps assess the DNS query load and usage trends across the cluster.\r\n\r\n        What this metric measures:\r\n        Tracks the cumulative number of DNS requests received by CoreDNS.\r\n\r\n        When it is applicable:\r\n        Useful for understanding DNS traffic volume, detecting sudden spikes or drops in request rates, and capacity planning.\r\n        ",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName"
    ]
  },
  {
    "Name": "GetCoreDNSForwardConcurrentRejectsCount",
    "Description": "\r\n        Retrieve the number of DNS queries rejected by CoreDNS due to exceeding the maximum allowed concurrent upstream requests, within a managed Kubernetes cluster. \r\n        Results are segmented by node or VMSS over a specified time range.\r\n\r\n        What this metric measures:\r\n        Counts the total number of DNS queries dropped when CoreDNS reached its limit for simultaneous upstream connections.\r\n\r\n        When it is applicable:\r\n        Useful for identifying DNS performance bottlenecks caused by concurrency limits, which may lead to dropped queries or degraded resolution performance.\r\n        ",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName"
    ]
  },
  {
    "Name": "GetAverageLatencyOfCoreDNSKubernetesDNSProgramming",
    "Description": "\r\n        Retrieve the average time (in seconds) taken by CoreDNS to program DNS records from Kubernetes service and endpoint objects, within a managed Kubernetes cluster. \r\n        Results are segmented by node or VMSS over a specified time range.\r\n\r\n        What this metric measures:\r\n        Measures the duration CoreDNS takes to process Kubernetes object updates and make DNS records available.\r\n\r\n        When it is applicable:\r\n        Helps detect delays in DNS record propagation caused by slow synchronization between Kubernetes API and CoreDNS, which can lead to temporary name resolution failures.\r\n        ",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName"
    ]
  },
  {
    "Name": "GetCorednsPodFailureEvents",
    "Description": "Get coredns pod failure events for the container app environment per node/vmss for a given time frame",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName"
    ]
  },
  {
    "Name": "GetSwiftBootstrapAgentPodFailureEvents",
    "Description": "Get swift bootstrap agent pod failure events for the container app environment per node/vmss for a given time frame",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName"
    ]
  },
  {
    "Name": "GetSwiftBootstrapAgentPodHealthStatus",
    "Description": "Get swift bootstrap agent pod health status for the container app environment per node/vmss for a given time frame",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName"
    ]
  },
  {
    "Name": "GetDNSConfigUpdateStatus",
    "Description": "Get DNS config update status for the container app environment for a given time frame",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName"
    ]
  },
  {
    "Name": "CheckIfDNSServerFailedToResolveDot",
    "Description": "Check if the Custom DNS server failed to resolve the dot (.) for the container app environment for a given time frame",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName"
    ]
  },
  {
    "Name": "GetLogConfiguration",
    "Description": "Get list of Log Configuration for the container app environment at start and end of time window. It also checks if Log Configuration are configured or not. Outputs obtained are:\r\n            - ChageStatus for logDestination (whether log destination has changed or not)\r\n            - logDestination (value of log destination after change)\r\n            - PreviousLogDestination (value of log destination before change)\r\n            - hasDynamicJsonColumns (whether dynamic json columns are present or not)\r\n            If no data is returned then ask to validate inputs again as it should never be the case.",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "customerSubscriptionId",
      "managedEnvironmentName",
      "managedClusterName"
    ]
  },
  {
    "Name": "GetEventProcessorErrors",
    "Description": "Get list of Event Processor Errors for the container app environment at start and end of time window. At least 1 output present means logs are present. No Warnings/Errors means no issues found.\r\n            If no data is returned then it may mean no warnings are present or there is an issue with the provided inputs. Please ensure those are correct otherwise re-run the tool.",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName",
      "containerAppOrJobName"
    ]
  },
  {
    "Name": "GetEventProcessorLeaderElectionEvents",
    "Description": "Get list of Event Processor Leader Election Events for the container app environment at start and end of time window.\r\n            If no data is returned then it may mean no leader election event happened during the interval or there is an issue with the provided inputs. Please ensure those are correct otherwise re-run the tool.",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName"
    ]
  },
  {
    "Name": "GetAppsAndjobsVolumeForEnv",
    "Description": "Get list of Apps and Jobs Volume for the container app environment at start and end of time window.\r\n            If no data is returned then it may mean no apps and jobs volume data is present or there is an issue with the provided inputs. Please ensure those are correct otherwise re-run the tool.",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName"
    ]
  },
  {
    "Name": "GetEventProcessorPods",
    "Description": "Get list of Event Processor Pods for the container app environment at start and end of time window.\r\n            If no data is returned then it may mean no event processor pods are present or there is an issue with the provided inputs. Please ensure those are correct otherwise re-run the tool.",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName"
    ]
  },
  {
    "Name": "GetLogProcessorPods",
    "Description": "Get list of Log Processor Pods for the container app environment at start and end of time window.\r\n            If no data is returned then it may mean no log processor pods are present or there is an issue with the provided inputs. Please ensure those are correct otherwise re-run the tool.",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName"
    ]
  },
  {
    "Name": "GetEventProcessorPodStatus",
    "Description": "Get list of Event Processor Pod Status for the container app environment at start and end of time window.\r\n            If no data is returned then it may mean no event processor pod status data is present or there is an issue with the provided inputs. Please ensure those are correct otherwise re-run the tool.",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName"
    ]
  },
  {
    "Name": "GetLogProcessorPodStatus",
    "Description": "Get list of Log Processor Pod Status for the container app environment at start and end of time window.\r\n            If no data is returned then it may mean no log processor pod status data is present or there is an issue with the provided inputs. Please ensure those are correct otherwise re-run the tool.",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName"
    ]
  },
  {
    "Name": "GetContainerAppWorkloadProfile",
    "Description": "Get type of Container App Workload Profile for the container app environment at start and end of time window.\r\n            If no data is returned then it may mean no container app workload profile data is present or there is an issue with the provided inputs. Please ensure those are correct otherwise re-run the tool.",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "containerAppOrJobName"
    ]
  },
  {
    "Name": "GetInputPressureOnLogProcessor",
    "Description": "Get Input Pressure on Log Processor for the managed Kubernetes cluster, segmented by node or VMSS over a specified time range.\r\n\r\n            What this metric measures: The query calculates the total records input to log-processor.\r\n\r\n            When it is applicable: Anomaly in this indicates high resource pressure on the log-processor.\r\n        ",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName"
    ]
  },
  {
    "Name": "GetMemoryPressureOnFluentbit",
    "Description": "Get Memory Pressure on Fluentbit for the managed Kubernetes cluster, segmented by node or VMSS over a specified time range.\r\n\r\n            What this metric measures: The query calculates the total input storage memory used by fluentbit in bytes.\r\n\r\n            When it is applicable: Anomaly in this indicates high memory resource pressure on the fluentbit\r\n        ",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName"
    ]
  },
  {
    "Name": "GetFluentbitOutputCount",
    "Description": "Get count of output processed by Fluentbit for the managed Kubernetes cluster, segmented by node or VMSS over a specified time range.\r\n\r\n            What this metric measures: The query calculates the total output records processed by fluentbit.\r\n\r\n            When it is applicable: Significant drop in the value indicates flunetbit having issues. Manunal investigation is required.\r\n        ",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName"
    ]
  },
  {
    "Name": "GetFluentbitBufferPressure",
    "Description": "Get buffer pressure experienced by Fluentbit for the managed Kubernetes cluster, segmented by node or VMSS over a specified time range.\r\n\r\n            What this metric measures: The query calculates input storage buffer overflow for fluentbit.\r\n\r\n            When it is applicable: Existence of this metric indicates that input storage has exceeded its configured limit. No records indicate healthy.\r\n        ",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName"
    ]
  },
  {
    "Name": "GetFluentbitOutputErrors",
    "Description": "Get any output errors faced by Fluentbit for the customer\u0027s container app or job in the managed Kubernetes cluster.\r\n            What this metric measures: The query calculates the total output errors for the customer\u0027s container app or job experienced by fluentbit.\r\n            When it is applicable: Existence of this metric indicates that fluentbit is having issues in processing the output. Manual investigation is required.",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName"
    ]
  },
  {
    "Name": "GetContainerAppInfraLayer",
    "Description": "\r\nThis operation will get the underlying infrastucture for the customer\u0027s container app\r\n\r\nInput parameters:\r\n- region: The Azure region where the container app is hosted\r\n- fromDate: The start date for the query\r\n- toDate: The end date for the query\r\n- subscriptionId: The Id of the Azure subscription\r\n- resourceGroupName: The name of the resource group where the container app is hosted\r\n- containerAppName: The name of the container app\r\n- managedClusterName: The name of the managed cluster\r\n\r\nOutput:\r\nThe return value will be either AKS or Legion, which is the underlying infrastructure layer",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "subscriptionId",
      "resourceGroupName",
      "containerAppName",
      "managedClusterName"
    ]
  },
  {
    "Name": "GetMetricsMdmCount",
    "Description": "\r\nThis operation identifies whether metrics were missed in the given time period\r\n\r\nInput parameters:\r\n- region: The Azure region where the container app is hosted\r\n- fromDate: The start date for the query\r\n- toDate: The end date for the query\r\n- metricName: The name of the metric to check\r\n- containerAppArmId: The ARM ID of the container app\r\n\r\nReturns true if the metric is missing, otherwise false.\r\n",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "metricName",
      "containerAppArmId"
    ]
  },
  {
    "Name": "GetMdmPodHeartbeatMissedTimes",
    "Description": "\r\nThis operation retrieves the missed times for MDM pod heartbeats in the specified time range.\r\n\r\nInput parameters:\r\n- region: The Azure region where the container app is hosted\r\n- fromDate: The start date for the query\r\n- toDate: The end date for the query\r\n- managedClusterName: The name of the managed cluster\r\n\r\nReturns a string containing the missed times for MDM pod heartbeats in the specified time range.\r\n",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName"
    ]
  },
  {
    "Name": "GetMissedMdmMetricTimes",
    "Description": "\r\nThis operation retrieves times where metrics were missed in the specified time range.\r\n\r\nInput parameters:\r\n- region: The Azure region where the container app is hosted\r\n- fromDate: The start date for the query\r\n- toDate: The end date for the query\r\n- metricName: The name of the metric to check\r\n- containerAppArmId: The ARM ID of the container app\r\n\r\nReturns a string containing the times where the specified metric was missed in the given time range. If empty, the metric was not missed.\r\n",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "metricName",
      "containerAppArmId"
    ]
  },
  {
    "Name": "GetBillingPodLeaderElection",
    "Description": "\r\nThis operation retrieves times when the billing pod was going through a leader election in the specified time range.\r\n\r\nInput parameters:\r\n- region: The Azure region where the container app is hosted\r\n- fromDate: The start date for the query\r\n- toDate: The end date for the query\r\n- managedClusterName: The name of the managed cluster\r\n\r\nReturns a string containing the times when the billing pod was going through a leader election in the specified time range. If empty, there were no leader elections.\r\n",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName"
    ]
  },
  {
    "Name": "GetVKPodLeaderElection",
    "Description": "\r\nThis operation retrieves times when the VK pod was going through a leader election in the specified time range.\r\n\r\nInput parameters:\r\n- region: The Azure region where the container app is hosted\r\n- fromDate: The start date for the query\r\n- toDate: The end date for the query\r\n- managedClusterName: The name of the managed cluster\r\n\r\nReturns a string containing the times when the VK pod was going through a leader election in the specified time range. If empty, there were no leader elections.\r\n",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName"
    ]
  },
  {
    "Name": "GetAKSKubeletRuntimeErrors",
    "Description": "\r\nThis operation retrives AKS Kubelet runtime errors in the specified time range.\r\n\r\nInput parameters:\r\n- regionName: The Azure region where the AKS cluster is hosted\r\n- fromDate: The start date for the query\r\n- toDate: The end date for the query\r\n- resourceGroupName: The name of the resource group hosting the AKS cluster\r\n- subscriptionId: The Azure subscription ID\r\n- managedClusterName: The name of the managed cluster\r\n- ccpClusterId: The AKS Cluster Id, which consists of only numbers or letters (e.g., 666b5141d2007500010d60f3)\r\n\r\nReturns a string containing the AKS Kubelet runtime errors in the specified time range. If empty, there were no Kubelet runtime errors.\r\n",
    "Parameters": [
      "regionName",
      "fromDate",
      "toDate",
      "resourceGroupName",
      "subscriptionId",
      "managedClusterName",
      "ccpClusterId"
    ]
  },
  {
    "Name": "GetIssueInvestigationTimeRangeRCAContainerApp",
    "Description": "\r\n        Calculates the effective time range for issue investigation based on the available input parameters. \r\n        At least one of the following must be provided: issueFirstOccurrence, issueLastOccurrence, or reportedIssueObservedOnTime.\r\n        **Important:**\r\n        - Do NOT use this function if none of the input parameters are available.\r\n        ",
    "Parameters": [
      "issueFirstOccurrence",
      "issueLastOccurrence",
      "reportedIssueObservedOnTime"
    ]
  },
  {
    "Name": "GetIncidentInfoRCAContainerApp",
    "Description": "Get original ICM incident information.",
    "Parameters": [
      "incidentId"
    ]
  },
  {
    "Name": "GetDiscussionEntriesRCAContainerApp",
    "Description": "Get original ICM discussion entries\r\n        This operation will get all the discussion entries of the given IcM Incident.\r\n        Input parameters:\r\n        - IncidentId: The Id of the IcM incident. It is usually a integer number.\r\n        - QueryFrom: The timestamp for filter the discussion entries which are created after it.\r\n        The return value is a list of discussion entries of the given IcM Incident. Each discussion entry includes the following information:\r\n        - IncidentId: The Id of the IcM incident.\r\n        - TimeStamp: The timestamp of the discussion entry.\r\n        - ChangedBy: The user who created this discussion entry.\r\n        ",
    "Parameters": [
      "incidentId",
      "queryFrom"
    ]
  },
  {
    "Name": "OneLinerToRCA",
    "Description": "Provide official RCA from container apps template\r\n        This operation will take the one liner RCA and use the below template to provide a official formatted RCA.\r\n        - oneLinerRCA: The one liner RCA that needs to be formatted into the RCA template.\r\n        ",
    "Parameters": [
      "oneLinerRCA"
    ]
  },
  {
    "Name": "WasAgentHelpfulInDebuggingIssue",
    "Description": "\r\n        Submit feedback regarding the agent\u0027s assistance in debugging the issue.\r\n        clearly give both choices \u0027was agent helpful?\u0027 and \u0027is resolution accurate or close?\u0027\r\n        Input parameters:\r\n        - IncidentId: The unique identifier of the incident.\r\n        - wasHelpful: Indicates whether the agent was helpful in debugging the issue (true/false). Use null to skip this feedback.\r\n        - isResolutionCorrect: Indicates whether the resolution provided by the agent was accurate (true/false). Use null to skip this feedback.\r\n        ",
    "Parameters": [
      "incidentId",
      "wasHelpful",
      "isResolutionCorrect"
    ]
  },
  {
    "Name": "AddDiscussionEntryRCAContainerApp",
    "Description": "\r\n        **Note: DO NOT CALL IT AUTOMATICALLY. ALWAYS ASK USER BEFORE CALLING IT**\r\n        Add a valid HTML-formatted message discussion entry or summary of final investigate to an ICM incident\r\n        This operation will add a discussion entry to the given IcM Incident. \r\n        input parameters:\r\n        - incidentId: The Id of the IcM incident. It is usually a integer number.\r\n        - text: A well HTML-formatted message to add as discussion to IcM.\r\n        NOTE:\r\n            - text MUST be always valid HTML formatted message\r\n            - Remove all emojis if any present. \r\n        The operation will add a discussion entry to the given incident.\r\n        The return value is a boolean value for indicating if the operation is successful.\r\n        ",
    "Parameters": [
      "incidentId",
      "text"
    ]
  },
  {
    "Name": "ResolveIncidentRCAContainerApp",
    "Description": "Resolve an ICM incident. This operation will set the given IcM Incident to Resolved state. And you must give a reason of this resolve action.\r\n        **Note: Always confirm with the user before resolving the ICM incident, or proceed only if the user has already provided confirmation**\r\n\r\n        Input parameters:\r\n        - incidentId: The Id of the IcM incident.It is usually a integer number.\r\n        - reason: Usually it is a reason why you can resolve this incident.\r\n        The operation will mark the given incident as resolved. The return value is a boolean value for indicating if the operation is successful.\r\n        ",
    "Parameters": [
      "incidentId",
      "reason"
    ]
  },
  {
    "Name": "GracefulConnectionCount",
    "Description": "    Analyze the distribution of connection states to identify patterns in connection termination for a specific container app pod.\r\n    This query examines TCP connection sequences to categorize connections by their termination state.\r\n\r\n    What this metric measures:\r\n    - TCP Handshake Failures: Connections that failed to establish properly\r\n    - Gracefully closed: Connections terminated with proper FIN handshake\r\n    - Reset connections: Connections terminated abruptly with RST packets\r\n    - Half-close scenarios: One-way connection terminations\r\n    - Idle timeouts: Connections that timed out without proper closure\r\n\r\n    When it is applicable:\r\n    Useful for identifying connection quality issues, network problems, or application-level connection handling issues.",
    "Parameters": [
      "fromDate",
      "toDate",
      "podGuid",
      "region"
    ]
  },
  {
    "Name": "GetTerminatedConnectionsForPod",
    "Description": "    Retrieve details of connections that were not gracefully closed to identify problematic outbound connections for a specific container app pod.\r\n    This query filters out gracefully terminated connections and provides detailed information about problematic connections.\r\n\r\n    What this metric measures:\r\n    - Non-gracefully terminated connections with timing information\r\n    - Destination details including resolved domain names\r\n    - Connection duration and termination reasons\r\n    - Packet sequences showing connection behavior\r\n\r\n    When it is applicable:\r\n    Useful for identifying specific endpoints or connection patterns that are causing issues, network connectivity problems, or application bugs.",
    "Parameters": [
      "fromDate",
      "toDate",
      "podGuid",
      "region"
    ]
  },
  {
    "Name": "DnsServerManagerOperation",
    "Description": "    Retrieve DNS server manager operations to identify any DNS resolution issues that might affect outbound connections for a specific container app pod.\r\n    This query examines logs from DNS-related components to identify configuration issues or operational problems.\r\n\r\n    What this metric measures:\r\n    - DNS server manager operations and their outcomes\r\n    - DNS listener manager activities\r\n    - CoreDNS manager operations\r\n    - Timing and trace information for DNS operations\r\n\r\n    When it is applicable:\r\n    Useful for correlating connection issues with DNS problems, identifying DNS configuration changes, or troubleshooting name resolution failures.",
    "Parameters": [
      "fromDate",
      "toDate",
      "managedCluster",
      "podName",
      "region"
    ]
  },
  {
    "Name": "GetASIPageForLegionPod",
    "Description": "Retrieve a direct ASI (App Service Insights) page URL for a specific Pod in a Legion cluster.\r\nThis link provides diagnostic insights into the specified Pod.\r\n\r\nInputs:\r\n- podName: Name of the Pod.\r\n- managedCluster: Namespace of the resource (e.g., ccpNamespace).\r\n- fromDate / toDate: Time range for diagnostic analysis.",
    "Parameters": [
      "podName",
      "managedCluster",
      "fromDate",
      "toDate"
    ]
  },
  {
    "Name": "GetPodGuidFromName",
    "Description": "    Retrieve PodGuid and related information for a specific container app pod using its name and namespace.\r\n    This query searches system logs to find the PodGuid which is required for subsequent connection analysis.\r\n\r\n    What this provides:\r\n    - PodGuid: Required identifier for connection queries\r\n    - LegionEnvironmentName: Environment information\r\n    - CenturionRoleId/NestedRoleId: Role identifiers\r\n    - Geneva trace URL: Direct link to trace logs\r\n    - KustoCluster: Cluster information for queries\r\n\r\n    When it is applicable:\r\n    Essential first step when you have pod name and namespace but need the PodGuid for connection analysis.",
    "Parameters": [
      "fromDate",
      "toDate",
      "podName",
      "resourceNamespace",
      "region"
    ]
  },
  {
    "Name": "GetSubscriptionDetail",
    "Description": "Get Subscription details, including BillingType, OfferType, OfferName, QuotaId, OrganizationName, etc.",
    "Parameters": [
      "subscriptionId"
    ]
  },
  {
    "Name": "GetSubscriptionUsage",
    "Description": "Get Subscription Usage details, including the NumberOfEnvironments, NumberOfContainerApps, NumberOfJobs, TrustLevel of the subscription.",
    "Parameters": [
      "subscriptionId"
    ]
  },
  {
    "Name": "GetSubscriptionQuota",
    "Description": "Get Subscription Quota limit.\r\n        Input parameters:\r\n        - subscriptionId: The subscription Id.\r\n        - region: The region of the quota need to be retrieved.\r\n        - quotaType: The quota type.\r\n        The return value is a string containing the quota limit value for the specified subscription, region, and quota type.\r\n        ",
    "Parameters": [
      "subscriptionId",
      "region",
      "quotaType"
    ]
  },
  {
    "Name": "SetSubscriptionQuota",
    "Description": "Set Subscription Quota limit.\r\n        Input parameters:\r\n        - subscriptionId: The subscription Id.\r\n        - region: The region of the quota need to be set.\r\n        - quotaType: The quota type. \r\n        The return value is a boolean value for indicating if the operation is successful.\r\n        ",
    "Parameters": [
      "subscriptionId",
      "region",
      "quotaType",
      "quotaLimit"
    ]
  },
  {
    "Name": "GetEnvironmentQuota",
    "Description": "Get Container App Environment Quota limit.\r\n        Input parameters:\r\n        - environmentResourceURL: The resource url of the container app environment. Format \u0060/subscriptions/[SubscriptionId]/resourceGroups/[resource group name]/providers/Microsoft.App/managedEnvironments/[environment name]\u0060\r\n        - region: The region of the quota need to be set. example eastus\r\n        - quotaType: The quota type. example ManagedEnvironmentConsumptionCores\r\n        The return value is a string containing the quota limit value for the specified environment, region, and quota type.\r\n        ",
    "Parameters": [
      "environmentResourceURL",
      "region",
      "quotaType"
    ]
  },
  {
    "Name": "SetEnvironmentQuota",
    "Description": "Set Managed Environment Quota limit.\r\nInput parameters:\r\n- incidentId: The incident id.\r\n- environmentResourceURL: The resource url of the container app environment.\r\n- region: The region of the quota need to be set.\r\n- quotaType: The quota type.\r\n- quotaLimit: The target quota limit.\r\n\r\nOutput:\r\n- id: The trace id of te operation, which can be used to track the operation in the kusto table ContainerAppsAdminEvents.\r\n- message: Describes the set Managed Environment Quota limit operation result.\r\n",
    "Parameters": [
      "incidentId",
      "environmentResourceURL",
      "region",
      "quotaType",
      "quotaLimit"
    ]
  },
  {
    "Name": "GetEnvironmentQuotaOperationResult",
    "Description": "Get the operation result of setting Managed Environment Quota limit.\r\nInput parameters:\r\n- operationId: The trace id of the operation, which can be used to track the operation in the kusto table ContainerAppsAdminEvents.\r\n- region: The region of the quota need to be set.\r\n\r\nOutput:\r\n- PreciseTimeStamp: the time when the operation is completed.\r\n- operationStatus: the status of the operation result.\r\n- message: Describes the set Managed Environment Quota limit operation result.\r\n",
    "Parameters": [
      "operationId",
      "region"
    ]
  },
  {
    "Name": "ValidateQuotaRequest",
    "Description": "validate quota request\r\nThis function evaluates a quota request based on specified parameters, including quota type, region, target limit, and subscription id.\r\nThis operation determines whether the quota request adheres to approval rules and returns a validation result.\r\nOutput:\r\n1. ApprovalResult: The status of the quota request, which can be one of the following:\r\n   - Approved: The request has been successfully approved.\r\n   - Rejected: The request has been denied.\r\n   - Pending: Additional manual approval is required.\r\n   - NotStarted: The request is incomplete and requires more details.\r\n2. OfferType: The offer type of the subscription.\r\n3. Reason: Provides an explanation for the validation decision.\r\n\r\nThis function helps ensure quota requests comply with predefined rules and provides a clear decision with supporting context.\r\n",
    "Parameters": [
      "quotaType",
      "subscriptionId",
      "region",
      "targetQuotaLimit",
      "environmentResourceURL"
    ]
  },
  {
    "Name": "GetContainerAppCpuExceedsThreshold",
    "Description": "\r\nThis operation will get if the container app CPU percentage is above specified threshold in the duration specified by fromDate and toDate.\r\n\r\nInput parameters:\r\n- region: The Azure region where the container app is hosted\r\n- fromDate: The start date for the query\r\n- toDate: The end date for the query\r\n- metricName: The name of the metric to check\r\n- containerAppArmId: The ARM ID of the container app\r\n- samplingType: The type of sampling to use (e.g., \u0027Max\u0027, \u0027Average\u0027, \u0027Min\u0027)\r\n- Threshold: The threshold value to compare against the metric (e.g., \u002780\u0027 for 80% CPU usage)\r\n\r\nOutput:\r\nReturns true if the CPU percentage is above the specified threshold, otherwise false.",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "containerAppArmId",
      "samplingType",
      "Threshold"
    ]
  },
  {
    "Name": "GetContainerAppMemoryExceedsThreshold",
    "Description": "\r\nThis operation will get if the container app memory percentage is above specified threshold in the duration specified by fromDate and toDate.\r\n\r\nInput parameters:\r\n- region: The Azure region where the container app is hosted\r\n- fromDate: The start date for the query\r\n- toDate: The end date for the query\r\n- metricName: The name of the metric to check\r\n- containerAppArmId: The ARM ID of the container app\r\n- samplingType: The type of sampling to use (e.g., \u0027Max\u0027, \u0027Average\u0027, \u0027Min\u0027)\r\n- Threshold: The threshold value to compare against the metric (e.g., \u002780\u0027 for 80% CPU usage)\r\n\r\nOutput:\r\nReturns true if the Memory percentage is above the specified threshold, otherwise false.",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "containerAppArmId",
      "samplingType",
      "Threshold"
    ]
  },
  {
    "Name": "GetContainerAppOOMKills",
    "Description": "\r\nThis operation will get if the container app CPU percentage is above specified threshold in the duration specified by fromDate and toDate.\r\n\r\nInput parameters:\r\n- region: The Azure region where the container app is hosted\r\n- fromDate: The start date for the query\r\n- toDate: The end date for the query\r\n- metricName: The name of the metric to check\r\n- containerAppArmId: The ARM ID of the container app\r\n- samplingType: The type of sampling to use (e.g., \u0027Max\u0027, \u0027Average\u0027, \u0027Min\u0027)\r\n- Threshold: The threshold value to compare against the metric (e.g., \u002780\u0027 for 80% CPU usage)\r\n\r\nOutput:\r\nReturns true if the CPU percentage is above the specified threshold, otherwise false.",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName",
      "containerAppOrJobName"
    ]
  },
  {
    "Name": "GetContainerAppManagedCluster",
    "Description": "Retrieve Container Apps Managed Cluster\r\nTool outputs:\r\n    - managedClusterName: Managed Cluster Name of the given Container App.",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "containerAppName",
      "resourceGroupName",
      "subscriptionId"
    ]
  },
  {
    "Name": "GetEnvoyPodLogs",
    "Description": "Retrieve Container Apps Envoy Pod Logs. \r\nTool outputs:\r\n    - EnvironmentName: Environment name, also called Managed Cluster Name.\r\n    - Log: Envoy pod log.\r\n    - Role: Cluster Node Id.\r\n    - _ContainerGroupId: Envoy container group Id.\r\n    - _ContainerGroupName: Envoy container group Name.\r\n    - _ContainerId: Envoy container Id.\r\n    - _ContainerImage: The docker image used by the Envoy container.",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName"
    ]
  },
  {
    "Name": "GetEnvoyControllerLogs",
    "Description": "Retrieve Container Apps Envoy Controller Logs.\r\nTool outputs:\r\n    - PreciseTimeStamp: Envoy controller log timestamp.\r\n    - Log: Envoy controller events log.\r\n    - msg: Envoy controller events message.\r\n    - error: Envoy controller events error message.\r\n    - Role: Cluster Node Id.\r\n    - _ContainerGroupId: Envoy container group Id.\r\n    - _ContainerGroupName: Envoy container group Name.\r\n    - _ContainerId: Envoy container Id.\r\n    - _ContainerImage: The docker image used by the Envoy container.\r\n    - caller:",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName"
    ]
  },
  {
    "Name": "GetEnvoyAccessRequestCountTimeSeries",
    "Description": "Retrieve Container Apps Envoy Access Request Count Time Series at Container App Level. \r\ncount of envoy access request grouped by http status code, e.g. Http 2xx Count, Http 3xx Count, Http 4xx Count, Http 5xx Count.",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName",
      "containerAppName"
    ]
  },
  {
    "Name": "GetManagedClusterLevelEnvoyAccessRequestCount",
    "Description": "Retrieve Managed Cluster Level Envoy Access Request Count Time Series.\r\nThis tool is used to verify if there is any envoy access log recorded in the managed cluster within the given time range.\r\n- If there is no Envoy Access Request at Container App level, but there is at Managed Cluster level, it indicates that the issue is not related to the Envoy component in the Managed Cluster, but rather to the specific Container App itself.\r\n- If there is no Envoy Access Request at both Container App and Managed Cluster levels, it indicates that the issue maybe related to the Envoy component in the Managed Cluster, so none of the Container Apps in the Managed Cluster are receiving any requests via Envoy.\r\n",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName"
    ]
  },
  {
    "Name": "GetEnvoyAccessLogs",
    "Description": "Retrieve detailed Container Apps Envoy Access Logs at Container App Level.\r\nTool outputs:\r\n    - FirstSeen: Start time of the current kind of envoy access log.\r\n    - LastSeen: End time of the current kind of envoy access log.\r\n    - max_RequestDuration: maximum request duration of this kind of envoy access log.\r\n    - Count: count of this kind of envoy access log.\r\n    - Authority: Request access domain name.\r\n    - Method: HTTP request methods.\r\n    - Path: Request access path.\r\n    - Protocol: Internet protocol.\r\n    - Status: HTTP response status(e.g., 200, 503).\r\n    - ResponseCodeDetails: Response code details. (e.g. via_upstream, downstream_remote_disconnect)\r\n    - UpstreamHost: The upstream host\u0027s IP address and port in the format \u003Cip-address\u003E:\u003Cport\u003E (e.g., 100.100.202.85:8080).",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName",
      "containerAppName"
    ]
  },
  {
    "Name": "GetSwiftNetworkingEvents",
    "Description": "Retrieve Swift Networking Events\r\nTool outputs:\r\n    - PreciseTimeStamp: Swift networking event timestamp.\r\n    - logger: Swift networking event logger.\r\n    - Log: Swift networking event log.\r\n    - msg: Swift networking event message.\r\n    - error: Swift networking event error message.\r\n    - Role: Cluster Node Id.\r\n    - _ContainerGroupId: Envoy container group Id.\r\n    - _ContainerGroupName: Envoy container group Name.\r\n    - _ContainerId: Envoy container Id.\r\n    - _ContainerImage: The docker image used by the Envoy container.\r\n    - caller: event caller.",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName"
    ]
  },
  {
    "Name": "GetEnvoyPodStatus",
    "Description": "Retrieve Envoy Pod Status\r\nTool outputs:\r\n    - StartTime: Start time of the current envoy pod status.\r\n    - EndTime: End time of the current envoy pod status.\r\n    - PodName: Name of the envoy pod.\r\n    - PodStatus: Status of the envoy pod.",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName"
    ]
  },
  {
    "Name": "GetContainerAppPodStatus",
    "Description": "Retrieve Container App Pod Status\r\nTool outputs:\r\n    - StartTime: Start time of the Container App pod status.\r\n    - EndTime: End time of the Container App pod status.\r\n    - PodName: Name of the Container App pod.\r\n    - PodStatus: Status of the Container App pod.\r\n    - ContainerName: Pod container name. There can be multiple containers in a pod.\r\n    - ContainerStatus: Status of the pod container. The value can be Ready or NotReady. If the value is not Ready, even if the Pod is in Running state, it indicates that the pod container is not ready to serve traffic.",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName",
      "containerAppName"
    ]
  },
  {
    "Name": "GetContainerAppStatus",
    "Description": "Retrieve Container App Status\r\nTool outputs:\r\n    - StartTime: Start time of the current container app provisioning status.\r\n    - EndTime: End time of the current container app provisioning status.\r\n    - containerAppName: Name of the container app.\r\n    - operationType: Operation type.\r\n    - provisioningState: Provisioning status of the container app.",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "containerAppName",
      "resourceGroupName",
      "subscriptionId"
    ]
  },
  {
    "Name": "GetContainerAppAdminEvents",
    "Description": "Retrieve Container App Admin Events\r\nTool outputs:\r\n    - PreciseTimeStamp: Container app admin event timestamp.\r\n    - requestMethod: HTTP request method.\r\n    - requestPath: HTTP request path.\r\n    - statusCode: HTTP response status code.\r\n    - requestBody: HTTP request body.\r\n    - durationInMilliseconds: The duration of the request in milliseconds.\r\n    - env_dt_traceId: The trace ID associated with the event.",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "containerAppName",
      "resourceGroupName",
      "subscriptionId"
    ]
  },
  {
    "Name": "GetJobDefinition",
    "Description": "Retrieve the Container Apps job definition (spec) for a given Container App Job\r\nTool outputs:\r\n  - Timestamp: Timestamp of the job definition. More than 1 row indicates change in job defintion(spec).\r\n  - Configuration: Configuration details for th job, like trigger type, retries, job deadlines, completion times\r\n                    parallelism for the job, container registry, assigned identity etc details.\r\n  - Template: Job template containing job containers deatails, cpu, memory resource details.\r\n  - Labels: Labels for the job. It has the managed environment name and workloadprofile name for the job.\r\n  - Status: Status of the container app Job. It has jobRunningState and jobProvisioningState.\r\n                   Possible values are for jobRunningState: Running, Suspended.\r\n                   Possible values for jobProvisioningState: Provisioned, Failed.",
    "Parameters": [
      "containerAppJobName",
      "region",
      "cappClusterName",
      "queryFrom",
      "queryTo"
    ]
  },
  {
    "Name": "GetJobExecutionFinalStatus",
    "Description": "Get the job execution\u0027s final status for a Container App Job. It contains detailed status of the given\r\njob execution, whether succeeded or failed, if failed, failure reason and message details in JobExecutionStatusDetails column.\r\nTool outputs:\r\n  - PreciseTimeStamp: Precise timestamp of the event.\r\n  - JobExecutionName: Name of the job execution.\r\n  - JobExecutionStatus: Status of the job execution, ex: Succeeded, Failed.\r\n  - JobExecutionStatusDetails: Detailed status of the job execution, if failed, it has reason for failure, message etc useful details.",
    "Parameters": [
      "region",
      "managedClusterName",
      "jobExecutionName",
      "queryFrom",
      "queryTo"
    ]
  },
  {
    "Name": "GetJobExecutionEvents",
    "Description": "Get full lifecycle events for a specific Container App Job execution from EventProcessorEvents.\r\nTool outputs:\r\n  - PreciseTimeStamp: Precise timestamp of the event.\r\n  - msg: Log message of the event.\r\n  - Reason: Reason for the event.\r\n  - Count: Count of the event.\r\n  - Type: Type of the event, ex: Warning, Normal, Error etc.",
    "Parameters": [
      "region",
      "jobExecutionName",
      "managedClusterName",
      "queryFrom",
      "queryTo"
    ]
  },
  {
    "Name": "GetAllJobExecutionsErrorEvents",
    "Description": "Gets all error events for all job executions of a given ContainerApp Job.",
    "Parameters": [
      "region",
      "managedClusterName",
      "containerAppJobName",
      "queryFrom",
      "queryTo"
    ]
  },
  {
    "Name": "GetAllJobExecutionsFinalStatus",
    "Description": "Gets the final status for all job executions of a given ContainerApp Job.",
    "Parameters": [
      "region",
      "managedClusterName",
      "containerAppJobName",
      "queryFrom",
      "queryTo"
    ]
  },
  {
    "Name": "GetKedaEventsForJobScaledJobs",
    "Description": "Retrieve KEDA events for job scaled jobs.\r\nTool outputs:\r\n    - Timestamp: Event timestamp\r\n    - Level: Log level\r\n    - Logger: KEDA component logger\r\n    - Message: KEDA event message\r\n    - ScalerType: Type of scaler used\r\n    - JobName: Associated job name",
    "Parameters": [
      "region",
      "managedClusterName",
      "containerAppJobName",
      "queryFrom",
      "queryTo"
    ]
  },
  {
    "Name": "GetLegionVKEventsForJobsRunningConsumptionV2",
    "Description": "Retrieve Legion VK events for jobs running on Consumption V2 workload profile.\r\nTool outputs:\r\n    - Timestamp: Event timestamp\r\n    - Level: Log level\r\n    - Message: Legion VK event message\r\n    - PodName: Associated pod name\r\n    - JobName: Associated job name\r\n    - Phase: Pod lifecycle phase\r\n    - Reason: Event reason",
    "Parameters": [
      "region",
      "managedClusterName",
      "jobExecutionName",
      "queryFrom",
      "queryTo"
    ]
  },
  {
    "Name": "GetLegionSystemLogsForJobExecutionErrors",
    "Description": "Retrieves container app job execution errors from Legion System Logs, for consumption workloadprofile jobs. It contains details indicating issues with the job execution\r\non the Legion platform. Only one of the job execution name and job name is required, the other can be empty. Job execution name can be inferred from previous queries.\r\nTool outputs:\r\n    - Message: Error message\r\n    - Value: Error value\r\n    - count_: Error count",
    "Parameters": [
      "region",
      "managedClusterName",
      "containerAppJobName",
      "jobExecutionName",
      "queryFrom",
      "queryTo"
    ]
  },
  {
    "Name": "GetASIPageForContainerAppJob",
    "Description": "Retrieves the Azure Service Insights (ASI) page link for the specified Container App Job.\r\nReturns a direct link to the ASI portal for the given job, scoped to the provided time range, resource group, and subscription.",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "containerAppName",
      "resourceGroupName",
      "subscriptionId"
    ]
  },
  {
    "Name": "GetASIPageForManagedClusterForApp",
    "Description": "Retrieve a direct ASI (App Service Insights) page URL for a specific **Managed Cluster** associated with an Azure Container Apps environment.\r\n        This link provides diagnostic insights into the cluster hosting the ACA environment.\r\n        **Note: Use this when specific container app name is known**\r\n        ",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "containerAppName",
      "resourceGroupName",
      "subscriptionId"
    ]
  },
  {
    "Name": "GetASIPageForManagedCluster",
    "Description": "Retrieve a direct ASI (App Service Insights) page URL for a specific **Managed Cluster** associated with an Azure Container Apps environment.\r\n        This link provides diagnostic insights into the cluster hosting the ACA environment.\r\n        **Note: Use this when managed cluster name  like \u0027calmisland-41ad83b9\u0027 is already known**\r\n        ",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName"
    ]
  },
  {
    "Name": "GetAksClusterCcpNamespace",
    "Description": "Retrieve the ccpNamespace of ACA\u0027s cluster, which is a needed parameter for other aks query \r\n\r\n        Inputs:\r\n        - region: Azure region where the cluster is deployed.\r\n        - fromDate / toDate: Time range for diagnostic analysis.\r\n        - resourceGroupName: Resource group of the ACA environment.\r\n        - subscriptionId: Azure subscription ID.\r\n        - managedClusterName: Name of the managed cluster.",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "resourceGroupName",
      "subscriptionId",
      "managedClusterName"
    ]
  },
  {
    "Name": "GetSystemComponentErrorEvents",
    "Description": "\r\n@Retrieve system component error events for the given managed cluster. The system component error events might provide diagnostic\r\ninformation to investigate the root cause of the issue.\r\n\r\nInputs:\r\n- region: Azure region where the cluster is deployed.\r\n- fromDate / toDate: Time range for diagnostic analysis.\r\n- managedClusterName: Name of the managed cluster.\r\n",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName"
    ]
  },
  {
    "Name": "GetSystemComponentCpuUsage",
    "Description": "\r\n@Retrieve system component CPU usage for the given managed cluster. This identifies system components that are consuming \r\nmore than 50% of their allocated CPU limits, which might indicate performance issues or resource constraints.\r\n\r\nInputs:\r\n- region: Azure region where the cluster is deployed.\r\n- fromDate / toDate: Time range for CPU usage analysis.\r\n- managedClusterName: Name of the managed cluster.\r\n",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName"
    ]
  },
  {
    "Name": "GetManagedEnvironmentInfo",
    "Description": "Retrieve configuration and provisioning metadata for a specific Azure Container Apps managed environment.\r\n\r\nThe function return managed environment detailed information includes:\r\nPreciseTimeStamp,managedClusterName,managedClusterLocation,managedSubscription,managedClusterCreatedTime,powerState,provisioningState,chartVersion,isInternal,chartVersionUpgradeTime,chartVersionUpgradeError,kubernetesVersion,kubernetesVersionUpgradeTime,upgradeBatch,environmentSubscription,environmentResourceGroup,environmentLocation,environmentName,environmentCreatedTime,hasWorkloadProfiles,hasCustomerVnet,hasMaintenanceConfiguration,publicNetworkAccess,hasPrivateEndpoints,envType,customVnet,RegionalConsumptionV2,tier\r\n",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "environmentName",
      "resourceGroupName",
      "subscriptionId",
      "sampling"
    ]
  },
  {
    "Name": "GetChangesInManagedEnvironment",
    "Description": "Retrieve configuration state changes for a specific Azure Container Apps managed environment within a given time range.\r\n\r\n        This function helps identify if incidents are correlated with configuration changes by highlighting changes that align with the reported issue timeline.\r\n\r\n        **Returns a list of component types that are changed**, including their previous and current values during the specified period.\r\n        Note: Unchanged components are NOT returned.\r\n        ",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "customerSubscriptionId",
      "managedEnvironmentName"
    ]
  },
  {
    "Name": "GetASIPageForManagedEnvironment",
    "Description": "Retrieve a direct ASI (App Service Insights) page URL for a given Azure Container Apps managed environment.\r\n\r\nTool outputs:\r\n- region: Azure region hosting the environment.\r\n- environmentName: Name of the ACA managed environment.\r\n- fromDate / toDate: Time window of interest.\r\n- resourceGroupName: Resource group of the environment.\r\n- subscriptionId: Azure subscription ID.\r\n- ASI URL: Clickable diagnostic link for ACA platform health and metadata.",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "environmentName",
      "resourceGroupName",
      "subscriptionId"
    ]
  },
  {
    "Name": "GetManagedClusterEnvironmentResourceId",
    "Description": "Retrieve the Azure Container Apps environment resource identity based on the managed cluster name.\r\n        Tool outputs:\r\n        - managedClusterName: Name of the managed cluster.\r\n        - subscription: Azure subscription ID of the Azure Container Apps environment.\r\n        - resourceGroup: Resource group of the Azure Container Apps environment.\r\n        - environmentName: Name of the Azure Container Apps environment.",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName"
    ]
  },
  {
    "Name": "GetManagedEnvironmentProvisioningStatus",
    "Description": "Retrieve the provisioning status of a specific Azure Container Apps managed environment.\r\n        Tool outputs:\r\n        - StartTime: Start time of the reported environment provisioning status.\r\n        - EndTime: End time of the reported environment provisioning status.\r\n        - environmentProvisioningState\r\n        - powerState\r\n        - managedClusterName\r\n        - environmentDeploymentErrors\r\n        - managedClusterProvisioningError.",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "environmentName",
      "resourceGroupName",
      "subscriptionId"
    ]
  },
  {
    "Name": "GetManagedEnvironmentAdminEvents",
    "Description": "Retrieve the Azure Container Apps environment Admin operation events.\r\n        Tool outputs:\r\n        - PreciseTimeStamp: Timestamp of the event.\r\n        - requestPath: The path of the request.\r\n        - requestMethod: The HTTP method used for the request.\r\n        - statusCode: The status code returned by the request.\r\n        - requestBody: The body of the request.\r\n        - durationInMilliseconds: The duration of the request in milliseconds.\r\n        - env_dt_traceId: The trace ID associated with the event.",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "environmentName",
      "resourceGroupName",
      "subscriptionId"
    ]
  },
  {
    "Name": "GetManagedEnvironmentOperationErrors",
    "Description": "Retrieve the Azure Container Apps environment operation errors.\r\n        Tool outputs:\r\n        - FirstSeen: Timestamp of the first occurrence of the error.\r\n        - LastSeen: Timestamp of the last occurrence of the error.\r\n        - count: The number of times the error has occurred.\r\n        - operationType: The type of operation that caused the error.\r\n        - operationEntityType: The type of entity that the operation was performed on.\r\n        - exception: The exception message associated with the error.",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "environmentName",
      "resourceGroupName",
      "subscriptionId"
    ]
  },
  {
    "Name": "GetPrivateEndpointConnectionDetails",
    "Description": "Retrieve the Azure Container Apps managed cluster private endpoint connection details.\r\nTool outputs:\r\n- frontendVmssName\r\n- frontendVmssCreatedTime\r\n- frontendVmssProvisioningState\r\n- tcpBridgeVersion\r\n- privateEndpointConnectionName\r\n- privateEndpointConnectionProxyName\r\n- privateEndpointId\r\n- privateEndpointConnectionProvisioningState\r\n- connectionStatus\r\n- storageAccountName\r\n        ",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName"
    ]
  },
  {
    "Name": "GetPrivateEndpointConnectionConnectionState",
    "Description": "Retrieve the Azure Container Apps Private Endpoint Connection connection state details.\r\nTool outputs:\r\n- StartTime: Start time of the reported connection state.\r\n- EndTime: End time of the reported connection state.\r\n- ConnectionState: The connection status of the private endpoint connection.\r\n",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "privateEndpointConnectionName"
    ]
  },
  {
    "Name": "GetPrivateEndpointConnectionProvisioningState",
    "Description": "Retrieve the Azure Container Apps Private Endpoint Connection Provisioning state details.\r\nTool outputs:\r\n- StartTime: Start time of the reported Provisioning status.\r\n- EndTime: End time of the reported Provisioning status.\r\n- ProvisioningState: The Provisioning state of the private endpoint connection.\r\n",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "privateEndpointConnectionName"
    ]
  },
  {
    "Name": "GetPrivateEndpointConnectionFrontendVmssProvisioningState",
    "Description": "Retrieve the provisioning state of the customer frontend VMSS (Virtual Machine Scale Set) for a specific Private Endpoint Connection.\r\nTool outputs:\r\n- StartTime: Start time of the reported Provisioning status.\r\n- EndTime: End time of the reported Provisioning status.\r\n- ProvisioningState: The Provisioning state of the customer frontend VMSS.\r\n",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "frontendVmssName"
    ]
  },
  {
    "Name": "GetAdminEventErrorMessagesByTraceId",
    "Description": "Retrieve detailed error messages for Azure Container Apps environment Admin operation events. Every environment Admin event has a unique trace ID (env_dt_traceId) that can be used to correlate related events and errors.\r\n\r\nTool outputs:\r\n- FirstSeen: First occurrence of the error message.\r\n- LastSeen: Last occurrence of the error message.\r\n- message: The error message content.\r\n",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "traceId"
    ]
  },
  {
    "Name": "GetAKSNodeAlerts",
    "Description": "Retrieve AKS node alerts and their status over time for a specific managed cluster.\r\nTool outputs:\r\n- StartTime: Start time of the alert timeline.\r\n- EndTime: End time of the alert timeline.\r\n- Content: Description of alert status (e.g., \u0027Healthy\u0027 or \u0027X Alerts\u0027).\r\n- Tooltip: Detailed information about critical and warning alerts.\r\n- Health: Overall health status (\u0027healthy\u0027, \u0027degraded\u0027, \u0027error\u0027).\r\n- GroupBy: Alert categorization (e.g., \u0027Alerts: Node\u0027).\r\n- warnings: List of warning-level alerts.\r\n- criticals: List of critical-level alerts.",
    "Parameters": [
      "fromDate",
      "toDate",
      "managedClusterName"
    ]
  },
  {
    "Name": "GetSessionPoolInfo",
    "Description": "Get session pool information for a given session pool name and time range.",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "subscriptionId",
      "resourceGroupName",
      "sessionPoolName"
    ]
  },
  {
    "Name": "GetChangesInSessionPool",
    "Description": "Get changes in session pool for a given subscription, resource group, and session pool name.",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "subscriptionId",
      "resourceGroupName",
      "sessionPoolName"
    ]
  },
  {
    "Name": "GetSessionPoolCreateOrUpdateLogs",
    "Description": "Get errors in session pool create or update logs for a given subscription, resource group, and session pool name.",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "subscriptionId",
      "resourceGroupName",
      "sessionPoolName"
    ]
  },
  {
    "Name": "GetCodeInterpreterSessionLegionPoolAvailability",
    "Description": "Check if allocation availability for the legion pool has dropped. \r\n                      It returns all instances where allocation rate was less than 100% for the given legion pod pool name in the specified time range.\r\n",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "legionPodPoolName"
    ]
  },
  {
    "Name": "GetCodeInterpreterSessionAllocatedPods",
    "Description": "Get a specific allocated pod for a code interpreter session in the given time range.\r\n                      If sessionIdentifier is provided, it will fetch the pod details for that specific session. Otherwise it will fetch a random pod allocated for a session in the given time range.\r\n                      It returns the session identifier, podName and poolType of the session.\r\n",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "subscriptionId",
      "resourceGroupName",
      "sessionPoolName",
      "sessionIdentifier"
    ]
  },
  {
    "Name": "GetCodeInterpreterSessionExecutionEventLogs",
    "Description": "Get errors in code interpreter session execution event logs for a given subscription, resource group, and session pool name.\r\n                      To fetch logs for a specific session, provide the session identifier.\r\n                      If empty, it will fetch logs for a random session execution in the given time range.\r\n",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "subscriptionId",
      "resourceGroupName",
      "sessionPoolName",
      "sessionIdentifier"
    ]
  },
  {
    "Name": "GetCodeInterpreterSessionPodEventLogs",
    "Description": "Get errors events for a code interpreter session pod with a specific podName/podId.\r\n",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "podName"
    ]
  },
  {
    "Name": "GetCodeInterpreterSessionPodLogs",
    "Description": "Get error logs for a code interpreter session pod with a specific podName/podId.\r\n                      Use this to fetch error logs for a code interpreter session pod in the given time range.\r\n                      Note that this only returns error logs, not all logs.\r\n",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "podName"
    ]
  },
  {
    "Name": "GetCustomContainerSessionActivatorLogs",
    "Description": "Get errors in custom container session activator logs for a given subscription, resource group, managedEnvironment and session pool name.\r\n                      Use this to fetch errors in pod allocation logs for a new session request. \r\n",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "subscriptionId",
      "resourceGroupName",
      "sessionPoolName",
      "managedEnvironmentName"
    ]
  },
  {
    "Name": "GetCustomContainerSessionEnvoyRequests",
    "Description": "Get all failed envoy requests for a custom container session in the given time range.\r\n                      This is useful to identify the issues with failed requests.\r\n                      For each failed request, it returns the \u0060Status\u0060 which is the status code and \u0060ResponseCodeDetails\u0060 which is the envoy response code for the request.\r\n",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "sessionPoolName",
      "managedClusterName"
    ]
  },
  {
    "Name": "GetCustomContainerSessionLegionPoolStatus",
    "Description": "Get the status of a custom container session legion pool for a given subscription, resource group, and session pool name.\r\n                      It returns the number of pods in pool which are ready, pending , allocated and inactive.\r\n",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "subscriptionId",
      "resourceGroupName",
      "sessionPoolName"
    ]
  },
  {
    "Name": "ListManagedClusterNodes",
    "Description": "List all the nodes names for the given Managed Cluster.\r\nThis operation will return the names of all the nodes in the specified Managed Cluster within the given time range.\r\nThese node names can be used to query the node heartbeat and Swift Network Container heartbeat of each node.\r\n",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName"
    ]
  },
  {
    "Name": "GetManagedClusterNodesHeartbeat",
    "Description": "Get the heartbeat status of all the nodes in the specified Managed Cluster.\r\nTool outputs:\r\n- StartTime: The start time of the heartbeat data.\r\n- EndTime: The end time of the heartbeat data.\r\n- NodeName: The name of the node.\r\n- NodeHeartbeat: The heartbeat status of the node. It can be \u0027Ready\u0027 or \u0027Not Ready\u0027.\r\n\r\nImportant Notes:\r\nUse this tool to identify which nodes are operational (\u0027Ready\u0027) and when. Nodes marked \u0027Ready\u0027 are expected to have active NetworkContainers. This tool is essential for detecting nodes that may lack corresponding container activity, indicating potential network issues.\r\n",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName"
    ]
  },
  {
    "Name": "GetManagedClusterNodesSwiftNetworkContainersHeartbeat",
    "Description": "Get the Swift Network Container heartbeat status of all the nodes in the specified Managed Cluster.\r\nTool outputs:\r\n- StartTime: The start time of the heartbeat data.\r\n- EndTime: The end time of the heartbeat data.\r\n- NodeName: The name of the node where the Swift Network Container is running.\r\n- NetworkContainerID: The ID of the network container.\r\n- NetworkContainerHeartbeat: The heartbeat status of the Swift Network Container. It is expected to be \u0027Alive\u0027.\r\n\r\nImportant Notes:\r\nUse this tool to verify that each \u0027Ready\u0027 node has a corresponding \u0027Alive\u0027 NetworkContainer. Missing or mismatched time windows between node and container heartbeats indicates network connectivity failures.\r\n",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName"
    ]
  },
  {
    "Name": "GetSwiftNetworkContainerCreateAndDeleteEventsLog",
    "Description": "Retrieves the Swift Network Container creation and deletion events for the specified Managed Cluster node.\r\nTool outputs:\r\n- TimeStamp: The timestamp of the event.\r\n- OperationName: The name of the operation, such as \u0027CreateSwiftNetworkContainer\u0027 or \u0027DeleteSwiftNetworkContainer\u0027. It can also be empty.\r\n- message: message describing the event.\r\n- Response: response of the operation, including httpStatusCode, networkContainerId, etc.\r\n- error: detailed error message if the operation failed.\r\n\r\nImportant Notes:\r\n- It is expected that the Swift Network Container is deleted after the node is deleted.\r\n",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName",
      "nodeName"
    ]
  },
  {
    "Name": "ListPotentialLeakedNetworkContainer",
    "Description": "Identify and list NetworkContainerID that might be leaked.\r\nThis tool will list all NetworkContainerIDs that may be leaked (those network containers that were not deleted after their associated node was removed) in the specified Managed Cluster.\r\n\r\nImportant Notes:\r\nThis tool is not accurate and may return false positives. It is recommended to use the GetDeleteNetworkContainerOperation tool and GetAggregatedNetworkContainerHealthEvent tool to double-check the deletion status of each NetworkContainerID.\r\n",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName"
    ]
  },
  {
    "Name": "GetDeleteNetworkContainerOperation",
    "Description": "Retrieves the delete operation details for a specific NetworkContainerID.\r\nThis tool will return all the DeleteNetworkContainer operations with detailed Message.\r\n- If no results are returned, it means there is no delete operation for the specified NetworkContainerID within the given time range. You need to highlight it since no delete operation was found it may indicate that the NetworkContainerID is leaked.\r\n- If the results are not empty, it means the delete operation was performed successfully or failed. The Message field will provide more details about the operation. Always show timestamp, NodeId, ContainerId, OperationName and Message fields in the result.\r\n",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "networkContainerID"
    ]
  },
  {
    "Name": "GetAggregatedNetworkContainerHealthEvent",
    "Description": "Retrieves the aggregated health event for a specific NetworkContainerID.\r\nThe return results can be used to double-check whether the NetworkContainerID is leaked or not.\r\n- OwnDsMappingsStatus: If the field value is 0, it indicates that the NetworkContainerID is leaked.\r\n- CustomerAddress: If there are multiple customer addresses, it indicates that the NetworkContainerID is leaked.\r\n- HealthState: It shows the detailed message of the health event. It\u0027s usually empty if the NetworkContainerID is not leaked.\r\n- NodeId and ContainerId: these two fields are very important for the user to do further investigation. \r\n",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "networkContainerID"
    ]
  },
  {
    "Name": "TrackSwiftILBGreKeyConflicts",
    "Description": "This function queries the NetworkServiceManagerEvents table to identify Swift network container errors related to GRE key conflicts in environments using Internal Load Balancers (ILB).\r\nThis is particularly useful for diagnosing issues where internal traffic fails to route correctly due to overlapping GRE keys.\r\n",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName"
    ]
  },
  {
    "Name": "GetASIPageForManagedClusterLoadBalancer",
    "Description": "Get ASI page URL for the Load Balancer of a Managed Cluster.",
    "Parameters": [
      "fromDate",
      "toDate",
      "loadBalancerResourceUrl"
    ]
  },
  {
    "Name": "GetVipAndDipAvailabilityUrls",
    "Description": "Get the managed cluster\u0027s load balancer VipAvailability_DataPathAvailability and DipAvailability_HealthProbeStatus page URLs",
    "Parameters": [
      "region",
      "fromDate",
      "toDate",
      "managedClusterName"
    ]
  },
  {
    "Name": "CalculateScalingCost",
    "Description": "Calculates the cost difference between current and target SKUs",
    "Parameters": [
      "resourceId",
      "direction",
      "currentSku",
      "targetSku"
    ]
  },
  {
    "Name": "CollectMemoryDump",
    "Description": "Collect memory dump from an App Service experiencing memory leaks for analysis.",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "RestartWebApplication",
    "Description": "Restart a Web App instance to mitigate memory leaks. This is typically used after scaling up if memory issues persist. The restart will clear the memory and start fresh.",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "ScaleAppServicePlanVertically",
    "Description": "Scale up an App Service Plan to a higher tier. SHOULD be always suggested when experiencing memory leaks. Prioritizes Premium v2/v3 tiers for better memory allocation.A scale up operation would incur a cost increase similarly a scale down operation would save costs, customer must be notified.",
    "Parameters": [
      "resourceId"
    ]
  },
  {
    "Name": "StorageAccountSetSharedKeySupport",
    "Description": "Enables or disables the use of shared keys for accessing storage accounts Microsoft.Storage/storageAccounts. This controls whether callers are forced to use Managed Identities or Delegated Secure Access Token (SAS).",
    "Parameters": [
      "resourceId",
      "featureState"
    ]
  },
  {
    "Name": "StorageAccountSetContainerPublicAccess",
    "Description": "Enables or disables public access to blob containers in the storage account Microsoft.Storage/storageAccounts. This controls a security measure that prevents unauthorized access to blobs.",
    "Parameters": [
      "resourceId",
      "featureState"
    ]
  },
  {
    "Name": "CosmosDbSetKeyBasedAuthSupport",
    "Description": "Sets the key based local auth setting on cosmosdb accounts Microsoft.DocumentDB/databaseAccounts. This forces callers to use non key based authentication methods such as managed identities or service principals.",
    "Parameters": [
      "resourceId",
      "featureState"
    ]
  },
  {
    "Name": "EventHubSetLocalAuthSupport",
    "Description": "Sets the key based local auth setting on event hub accounts microsoft.eventhub/namespaces. This forces callers to use non key based authentication methods such as managed identities or service principals.",
    "Parameters": [
      "resourceId",
      "featureState"
    ]
  },
  {
    "Name": "ServiceBusSetLocalAuthSupport",
    "Description": "Sets the key based local auth setting on service bus accounts microsoft.servicebus/namespaces. This forces callers to use non key based authentication methods such as managed identities or service principals.",
    "Parameters": [
      "resourceId",
      "featureState"
    ]
  },
  {
    "Name": "AzureSqlServerSetLocalAuthSupport",
    "Description": "Sets the authentication on azure sql server Microsoft.Sql/servers, disabling or enabling local auth support. If disabled, this forces callers to use authentication methods such as managed identities or service principals.",
    "Parameters": [
      "resourceId",
      "featureState"
    ]
  },
  {
    "Name": "AzureAppServiceSetFtpAuthenticationSupport",
    "Description": "Sets the authentication on azure Microsoft.Web/sites, disabling or enabling FTP authentication support. If disabled, this forces callers to use authentication methods such as managed identities or service principals.",
    "Parameters": [
      "resourceId",
      "featureState"
    ]
  },
  {
    "Name": "AzureAppServiceSetScmAuthenticationSupport",
    "Description": "Sets the authentication on azure Microsoft.Web/sites, disabling or enabling SCM authentication support. If disabled, this forces callers to use authentication methods such as managed identities or service principals.",
    "Parameters": [
      "resourceId",
      "featureState"
    ]
  },
  {
    "Name": "SuggestNextSku",
    "Description": "Given a current sku suggest a possible next sku",
    "Parameters": [
      "resourceId",
      "direction",
      "currentSku"
    ]
  },
  {
    "Name": "GetRoleAssignments",
    "Description": "Gets all role assignments for a specific user/managed identity on an Azure resource. If principalId is null or empty, all role assignments on the resource are returned.",
    "Parameters": [
      "resourceId",
      "principalId"
    ]
  },
  {
    "Name": "AddRoleAssignment",
    "Description": "Adds a role assignment for a user or managed identity on an Azure resource",
    "Parameters": [
      "resourceId",
      "principalType",
      "principalId",
      "roleName"
    ]
  },
  {
    "Name": "RemoveRoleAssignment",
    "Description": "Removes a role assignment for a user or managed identity on an Azure resource",
    "Parameters": [
      "resourceId",
      "principalId",
      "roleName"
    ]
  },
  {
    "Name": "CheckRoleAssignment",
    "Description": "Checks if a user or managed identity has a specific role on an Azure resource",
    "Parameters": [
      "resourceId",
      "principalId",
      "roleName"
    ]
  },
  {
    "Name": "GetRoleDetailsFromName",
    "Description": "Gets details of the role definition for a specified role name that can be applied to the resource.",
    "Parameters": [
      "roleName",
      "resourceId"
    ]
  },
  {
    "Name": "SearchDocuments",
    "Description": "Peforms a semantic search for documents in a knowledge base. The knowledge base contains up-to-date documentation that may be newer than your own knowledge.\r\nThe knowledge base contains following topics:\r\n- Az CLI documentation\r\n- Kubectl documentation\r\n- Documentation and user manual of yourself, Azure SRE Agent.",
    "Parameters": [
      "searchText"
    ]
  },
  {
    "Name": "NotifyUser",
    "Description": "Sends the specified message to the user. Use this to send updates about your current task as you are working on it. Do not use this for asking questions to the user, only for status updates.",
    "Parameters": [
      "message"
    ]
  },
  {
    "Name": "AskUserForInput",
    "Description": "Sends the specified message to the user and indicates that you require a response to proceed. Do not use this for any scenario where you just need to send the user an update in a fire and forget manner. If the user responds in a manner that does not satisfactorily answer your question, use this tool again.",
    "Parameters": [
      "message"
    ]
  }
]
</availableTools>

# User Message
The incident description is as follows:
My web app has a very high latency on the API "/create"

The resource id is: /subscriptions/26214a40-7d5f-4eac-9345-bf7f2d0da1fe/resourceGroups/xiangy-aca/providers/Microsoft.Web/sites/xiangy-test-app

# LLM Output
[
  "GetAppConsoleLogs",
  "GetDeploymentActivity",
  "GetWebAppCpuMetrics",
  "GetMetricTimeSeriesElementsForAzureResource",
  "GetActivityLogsSummary"
]
