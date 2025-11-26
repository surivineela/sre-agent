// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Data.DatabaseClients.GraphDbClient.Nodes;
using Agent.Data.DataModels;
using Agent.Data.Repositories;
using Agent.Framework;
using Agent.Plugins.Interface;
using Agent.Plugins.Services.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Thread = Agent.Core.Models.Api.v1.Thread;

namespace Agent.Runtime.SubAgents.DailySummaryAgent;

public class DailyReportScanner
{
    private readonly ILogger<DailyReportScanner> _logger;
    private readonly IThreadRepository _threadRepository;
    private readonly IAgentInboundCommunicationService _agentInboundCommunicationService;
    private readonly IGraphDatabaseClient _graphDatabaseClient;
    private readonly IGrafanaPlugin _grafanaPlugin;
    private readonly IGraphDBPlugin _graphDBPlugin;
    private readonly ICodeOptimizationsPlugin _codeOptimizationsPlugin;
    private readonly HttpClient _httpClient;
    private readonly IChatClientProvider _chatClientProvider;
    private readonly string _dashboardsDirectory;
    private readonly string _mainDashboardFilePath;
    private readonly string _grafanaUrl;
    private readonly string _prometheusUrl;
    private readonly string _dataSourceName;
    private readonly List<string> _dashboardsToActivate;
    private readonly string _puppeteerScreenshotApiUrl;
    private readonly DashboardSettings _dashboardSettings;
    private readonly IGraphService _graphDbService;
    private readonly bool _persistScreenshotsInFolder;
    private readonly List<ArmResourceNode> _armResourceNodes = [];
    private readonly IAuthenticationService _authenticationService;
    private readonly IIncidentRepository _incidentRepository;
    private readonly IGithubIssuePlugin _githubIssuePlugin;
    private readonly IConfiguration _configuration;
    private readonly IAppHealthHistoryRepository _appHealthHistoryRepository;
    private readonly CoreSettings _coreSettings;
    private readonly IHostEnvironment _hostEnvironment;

    private readonly string DashboardScreenshotsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DashboardScreenshots");

    private const string SreDashboard = "/d/azure-sre-resources/sre-azure-resource-overview";
    private readonly Dictionary<string, string> _dashboardsToProcessByResourceType = new(StringComparer.OrdinalIgnoreCase)
    {
        { "microsoft.app/containerapps", "azure-container-apps-container-app-view" },
        { "microsoft.storage/storageaccounts", "azure-insights-storage-accounts" },
        { "microsoft.documentdb/databaseaccounts", "azure-insights-cosmos-db" },
        { "microsoft.cache/redis", "azure-redis" },
        { "microsoft.web/sites", "azure-app-service" },
        // Pending: webapp, sql
    };

    public DailyReportScanner(
        IThreadRepository threadRepository,
        ILogger<DailyReportScanner> logger,
        IAgentInboundCommunicationService agentInboundCommunicationService,
        IGraphDatabaseClient graphDatabaseClient,
        IGrafanaPlugin grafanaPlugin,
        IGraphDBPlugin graphDBPlugin,
        ICodeOptimizationsPlugin codeOptimizationsPlugin,
        HttpClient httpClient,
        IChatClientProvider chatClientProvider,
        DashboardSettings dashboardSettings,
        IGraphService graphDbService,
        IAuthenticationService authenticationService,
        IIncidentRepository incidentRepository,
        IGithubIssuePlugin githubIssuePlugin,
        IConfiguration configuration,
        IAppHealthHistoryRepository appHealthHistoryRepository,
        CoreSettings coreSettings,
        IHostEnvironment hostEnvironment,
        string mainDashboardFile = "Main-Dashboard.json",
        string puppeteerScreenshotApiUrl = "https://test-capp.ambitiouspond-10f27fe1.canadaeast.azurecontainerapps.io")
    {
        _logger = logger;
        _threadRepository = threadRepository;
        _agentInboundCommunicationService = agentInboundCommunicationService;
        _graphDatabaseClient = graphDatabaseClient;
        _grafanaPlugin = grafanaPlugin;
        _httpClient = httpClient;
        _chatClientProvider = chatClientProvider;
        _dashboardsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SubAgents", "DailySummaryAgent", "Dashboards");
        _mainDashboardFilePath = Path.Combine(_dashboardsDirectory, mainDashboardFile);
        _grafanaUrl = dashboardSettings.GrafanaUrl.TrimEnd('/');
        _prometheusUrl = dashboardSettings.PrometheusUrl.TrimEnd('/');
        _graphDBPlugin = graphDBPlugin;
        _codeOptimizationsPlugin = codeOptimizationsPlugin;
        _dataSourceName = dashboardSettings.GrafanaDataSourceName ?? "KnowledgeGraph";
        _puppeteerScreenshotApiUrl = puppeteerScreenshotApiUrl;
        //_puppeteerScreenshotApiUrl = "http://20.57.166.55:3000";//puppeteerScreenshotApiUrl;

        // List of predefined Azure Monitor dashboards to activate
        _dashboardsToActivate = new List<string>
        {
            "Azure / Insights / Applications - Overview",
            "Azure / Infrastructure / Compute Monitoring",
            "Azure / Infrastructure / Data Monitoring",
            "Azure / Infrastructure / Network Monitoring",
            "Azure / Infrastructure / Storage and Key Vaults Monitoring",
            "Azure / Insights / Cosmos DB",
            "Azure / Insights / SQL Database",
            "Azure / Resources Overview",
        };
        _dashboardSettings = dashboardSettings;
        _graphDbService = graphDbService;
        _persistScreenshotsInFolder = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PERSIST_SCREENSHOTS"));
        _authenticationService = authenticationService;
        _incidentRepository = incidentRepository;
        _githubIssuePlugin = githubIssuePlugin;
        _configuration = configuration;
        _appHealthHistoryRepository = appHealthHistoryRepository;
        _coreSettings = coreSettings;
        _hostEnvironment = hostEnvironment;
    }

    public async Task<Thread?> ScanAndGenerateReport(CancellationToken cancellationToken)
    {
        _logger.LogInternalInformation("Starting daily report generation...");

        // Check if we need to run the daily report (e.g., only during certain hours)
        var now = DateTime.UtcNow;
        var todayReportTime = new DateTime(now.Year, now.Month, now.Day, 7, 0, 0, DateTimeKind.Utc); // 7 AM UTC

        //Skip if it's not time yet for the daily report
        // the daily report timer interval is 1 hour, so this will evaluate to false if the current hour is not 7
        if (now.Hour != todayReportTime.Hour)
        {
            _logger.LogDebug("Not time for daily report yet. Current hour: {CurrentHour}, Target hour: {TargetHour}",
                now.Hour, todayReportTime.Hour);

            return null;
        }

        var cveSummary = await GetCVESummary();
        var incidentsSummary = await GetIncidentsSummary();
        var appGroupsHealthSummary = await GetAppGroupsHealthSummaryAsync();
        var codeOptimizationsSummary = await GetCodeOptimizationsSummaryAsync();
        var dashboardSummary = string.Empty;

        var mainDashboardUrl = string.Empty;

        // only try to generate dashboard summary if grafana is enabled
        if (!string.IsNullOrWhiteSpace(_dashboardSettings.GrafanaUrl))
        {
            try
            {
                mainDashboardUrl = await TryToImportDashboards(cancellationToken);
                dashboardSummary = await CaptureAndSummarizeDashboardsAsync(mainDashboardUrl);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Failed to create and publish dashboard, generate daily report without it");
            }
        }

        var summarizedCodeOptimizationsReport = $"Total Recommendations: {codeOptimizationsSummary.TotalRecommendations}, " +
               $"CPU Recommendations: {codeOptimizationsSummary.CpuRecommendations}, " +
               $"Memory Recommendations: {codeOptimizationsSummary.MemoryRecommendations}, " +
               $"Blocking Recommendations: {codeOptimizationsSummary.BlockingRecommendations}";

        var suggestedActionsAndObservations = await GenerateSuggestedActionsAndOverallObservations(
            dashboardSummary,
            mainDashboardUrl,
            cveSummary,
            appGroupsHealthSummary,
            incidentsSummary,
            summarizedCodeOptimizationsReport);

        var overview = GenerateOverview(cveSummary, incidentsSummary, appGroupsHealthSummary);
        _logger.LogInternalInformation("Overview generated successfully.");

        // Prepare the input for the agent
        var input = new DailyReportSummaryInput
        {
            ReportType = "Daily",
            Timespan = "1d",
            Overview = overview,
            CVESummary = cveSummary,
            IncidentsSummary = incidentsSummary,
            AppGroupResourceSummary = appGroupsHealthSummary,
            CodeOptimizationsSummary = codeOptimizationsSummary,
            RecommendedActionsAndObservations = suggestedActionsAndObservations
        };

        // generate thread
        // Add the title with the day of the week and date
        var dayOfWeek = now.ToString("dddd", CultureInfo.InvariantCulture); // e.g., Monday
        var dateFormatted = now.ToString("MMMM dd, yyyy", CultureInfo.InvariantCulture); // e.g., March 20, 2023

        // generate json string to pass to the agent
        var report = JsonSerializer.Serialize(input, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            }
        });
        _logger.LogInternalInformation("Daily report input JSON: {ReportJson}", report);

        (var thread, var agentContext) = await _agentInboundCommunicationService.CreateAgentThread(
            $"Daily Resources Report - {dateFormatted}\n\n",
            report,
            agentTypeEnum: AgentTypeEnum.Meta,
            source: ThreadSource.DailyReport,
            isDailyReport: true);

        _logger.LogInternalInformation("Created thread for daily report: {ThreadId}", thread.Id);

        _logger.LogInternalInformation("Using Agent Framework to process daily report summary");
        var message = new ThreadMessage(
            ThreadId: agentContext.ThreadId,
            AgentContextId: agentContext.Id,
            MessageId: Guid.NewGuid(),
            Message: "Summarize the report.",
            UserId: "",
            DisplayName: "",
            Timestamp: DateTime.UtcNow);
        await _agentInboundCommunicationService.ProcessUserMessageAsync(message);
        return thread;
    }

    private async Task<CodeOptimizationsSummary> GetCodeOptimizationsSummaryAsync()
    {
        var summary = new CodeOptimizationsSummary();

        try
        {
            // Get all App Services
            var appServiceList = await _graphDBPlugin.ListResourcesByTypeAsync(
                resourceType: "microsoft.web/sites",
                propertyName: string.Empty,
                propertyValue: string.Empty,
                skip: 0,
                take: -1);

            // Build a list of resource info for batch call
            var resourceInfoList = appServiceList
                .Select(res => new
                {
                    SubscriptionId = res.GetValueOrDefault("subscriptionId")?.ToString() ?? string.Empty,
                    ResourceGroupName = res.GetValueOrDefault("resourceGroupName")?.ToString() ?? string.Empty,
                    Name = res.GetValueOrDefault("resourceName")?.ToString() ?? string.Empty,
                    Type = res.GetValueOrDefault("resourceType")?.ToString() ?? string.Empty
                })
                .Where(x => !string.IsNullOrEmpty(x.SubscriptionId) && !string.IsNullOrEmpty(x.ResourceGroupName) && !string.IsNullOrEmpty(x.Name))
                .ToList();

            // Build resourceIds for bulk call
            var resourceIds = resourceInfoList
                .Select(x => $"/subscriptions/{x.SubscriptionId}/resourceGroups/{x.ResourceGroupName}/providers/{x.Type}/{x.Name}")
                .ToList();

            // Call insights for all resources
            var insightsBulkResult = await _codeOptimizationsPlugin.GetCodeOptimizationInsightsBulkAsync(resourceIds);

            // Log that we fetched code optimizations insights
            var totalInsights = insightsBulkResult?.Values.Sum(insights => insights?.Count() ?? 0) ?? 0;
            _logger.LogInternalInformation(
                "Fetched code optimization insights for {ResourceIdCount} resource IDs, total insights: {TotalInsights}",
                resourceIds.Count,
                totalInsights);

            // Group by subscription + resource group
            var groups = resourceInfoList
                .GroupBy(r => new
                {
                    r.SubscriptionId,
                    r.ResourceGroupName
                })
                .OrderBy(g => g.Key.SubscriptionId)
                .ThenBy(g => g.Key.ResourceGroupName);

            foreach (var group in groups)
            {
                var rgEntry = new ResourceGroupCodeInsights
                {
                    SubscriptionId = group.Key.SubscriptionId,
                    ResourceGroupName = group.Key.ResourceGroupName
                };

                foreach (var resource in group)
                {
                    var resourceId = $"/subscriptions/{resource.SubscriptionId}/resourceGroups/{resource.ResourceGroupName}/providers/{resource.Type}/{resource.Name}";
                    try
                    {
                        List<InsightsRecommendationContract> insightsList = new List<InsightsRecommendationContract>();
                        if (insightsBulkResult != null && insightsBulkResult.TryGetValue(resourceId, out var insights))
                        {
                            insightsList = insights.ToList();
                        }

                        if (insightsList.Count > 0)
                        {
                            rgEntry.Apps.Add(new AppCodeInsights
                            {
                                ResourceId = resourceId,
                                Name = resource.Name,
                                Type = resource.Type,
                                Insights = insightsList
                            });

                            summary.TotalRecommendations += insightsList.Count;
                            summary.CpuRecommendations += insightsList.Count(i => string.Equals(i.Type, "CPU", StringComparison.OrdinalIgnoreCase));
                            summary.MemoryRecommendations += insightsList.Count(i => string.Equals(i.Type, "Memory", StringComparison.OrdinalIgnoreCase));
                            summary.BlockingRecommendations += insightsList.Count(i => string.Equals(i.Type, "Blocking", StringComparison.OrdinalIgnoreCase));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogInternalWarning("Failed to fetch code optimization insights for {ResourceId}: {Message}", resourceId, ex.Message);
                    }
                }

                if (rgEntry.Apps.Count > 0)
                {
                    summary.ResourceGroups.Add(rgEntry);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error generating Code Optimizations Summary: {Message}", ex.Message);
        }

        return summary;
    }

    // Returns main dashboard url
    public async Task<string?> TryToImportDashboards(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_grafanaUrl))
        {
            return "";
        }

        try
        {
            var uid = await SetupPrometheusDataSourceAsync(_grafanaUrl, _prometheusUrl, _dataSourceName);
            _logger.LogInternalInformation("data source {uid} already setup", uid);
            var (dashboardUrl, dashboardUid) = await CreateAndPublishDashboard(uid, cancellationToken);
            _logger.LogInternalInformation("Main dashboard {uid} imported. Url {url}", dashboardUid, dashboardUrl);

            // Activate predefined Azure Monitor dashboards
            await ActivateAzureMonitorDashboards(cancellationToken);
            _logger.LogInternalInformation("Azure monitor dashboards imported");

            // Activate predefined customized dashboards (except the main dashboard)
            await ActivateCustomizedDashboards([_mainDashboardFilePath], cancellationToken);
            _logger.LogInternalInformation("Customized dashboards imported");
            return dashboardUrl;
        }
        catch (Exception e)
        {
            _logger.LogInternalError(e, "Failed to import dashboards");
        }

        return "";
    }

    /// <summary>
    /// Captures screenshots of all dashboards and uses LLM to summarize them
    /// </summary>
    private async Task<string> CaptureAndSummarizeDashboardsAsync(string? dashboardUrl)
    {
        try
        {
            var token = await GetAccessTokenForGrafana();

            // Dictionary to store dashboard screenshots (name -> base64 image)
            Dictionary<string, string> dashboardScreenshots = new Dictionary<string, string>();

            // Get list of all available dashboards in Grafana
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

            var dashboardsResponse = await _httpClient.GetAsync($"{_grafanaUrl}/api/search?type=dash-db");
            dashboardsResponse.EnsureSuccessStatusCode();

            var dashboardsContent = await dashboardsResponse.Content.ReadAsStringAsync();
            var dashboards = JsonSerializer.Deserialize<JsonElement>(dashboardsContent);

            _logger.LogInternalInformation("Found {Count} dashboards to capture", dashboards.GetArrayLength());

            // Capture each dashboard
            foreach (var dashboard in dashboards.EnumerateArray())
            {
                if (dashboard.TryGetProperty("url", out var urlElement) &&
                    dashboard.TryGetProperty("title", out var titleElement) &&
                    dashboard.TryGetProperty("type", out var itemType) &&
                    string.Equals(itemType.GetString(), "dash-db", StringComparison.OrdinalIgnoreCase))
                {
                    var url = urlElement.GetString() ?? string.Empty;
                    var title = titleElement.GetString() ?? string.Empty;

                    try
                    {
                        // Capture the dashboard screenshot
                        var (resourceType, dashboardType) = _dashboardsToProcessByResourceType.FirstOrDefault(resourceType => url.Contains(resourceType.Value, StringComparison.OrdinalIgnoreCase));
                        if (string.IsNullOrEmpty(dashboardType))
                        {
                            _logger.LogInternalWarning("Failed to capture screenshot for dashboard: {Title}, Reason: Dashboard type not supported", title);
                            continue;
                        }

                        // Capture screenshot for each arm resource node
                        var screenshotResponses = new List<ScreenshotResponse>();
                        // TODO: This line will always result in a null reference exception because _armResourceNodes was not initialized.
                        // This is likely a dead code.
                        var armResourceNodes = _armResourceNodes
                            .Where(a => string.Equals(a.ResourceType, resourceType, StringComparison.OrdinalIgnoreCase));

                        foreach (var armResourceNode in armResourceNodes)
                        {
                            screenshotResponses.Add(await CaptureDashboardScreenshotAsync(url, armResourceNode));
                        }

                        if (!screenshotResponses.Any())
                        {
                            _logger.LogInternalWarning("Failed to capture screenshot for dashboard: {Title}, Reason: No screenshot received for {ArmResourceCount} {ResourceType} resources", title, armResourceNodes.Count(), resourceType);
                            continue;
                        }

                        var index = 0; // To handle multiple screenshots for the same dashboard if needed
                        foreach (var screenshotResponse in screenshotResponses)
                        {
                            var indexedTitle = title + ++index;
                            var base64Image = screenshotResponse?.Screenshot;
                            if (!string.IsNullOrEmpty(base64Image))
                            {
                                PersistScreenshot(indexedTitle, base64Image);
                                dashboardScreenshots.Add(indexedTitle, base64Image);
                                _logger.LogInternalInformation($"Successfully captured screenshot for dashboard: {title}", title);
                            }
                        }

                        _logger.LogInternalInformation($"[Summary] Successfully captured {index} screenshots for resource type: {resourceType}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogInternalError(ex, "Error capturing screenshot for dashboard {Title}: {Message}", title, ex.Message);
                    }
                }
            }

            // If we have any screenshots, use LLM to summarize them
            if (dashboardScreenshots.Count > 0)
            {
                _logger.LogInternalInformation("Summarizing {Count} dashboard screenshots using LLM", dashboardScreenshots.Count);
                return await SummarizeDashboardScreenshotsAsync(dashboardScreenshots, dashboardUrl);
            }
            else
            {
                _logger.LogInternalWarning("No dashboard screenshots were captured, skipping LLM summarization");
                return "No dashboard screenshots available for analysis.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error capturing and summarizing dashboard screenshots: {Message}", ex.Message);
            return $"Error generating dashboard visual summary: {ex.Message}";
        }
    }

    private void PersistScreenshot(string title, string base64Image)
    {
        if (!_persistScreenshotsInFolder ||
            string.IsNullOrEmpty(base64Image))
        {
            return;
        }

        if (!Directory.Exists(DashboardScreenshotsFolder))
        {
            Directory.CreateDirectory(DashboardScreenshotsFolder);
            Console.WriteLine($"DashboardScreenshotsFolder created successfully: {DashboardScreenshotsFolder}");
        }

        // Convert Base64 string to byte array
        var imageBytes = Convert.FromBase64String(base64Image);

        // Save the image to a file
        var filePath = Path.Combine(DashboardScreenshotsFolder, $"{SanitizeFileName(title)}-{DateTime.Now:HHmmss}.jpg");
        File.WriteAllBytes(filePath, imageBytes);

        _logger.LogInternalInformation($"Image saved successfully at: {Path.GetFullPath(filePath)}");
    }

    /// <summary>
    /// Captures a screenshot of a specific dashboard in Grafana
    /// </summary>
    private async Task<ScreenshotResponse> CaptureDashboardScreenshotAsync(string dashboardUrl, ArmResourceNode armResourceNode)
    {
        try
        {
            var dashboardUrlWithParameters = GetParameterizedDashboardUrl(dashboardUrl, armResourceNode);

            if (!string.IsNullOrEmpty(dashboardUrlWithParameters))
            {
                _logger.LogInternalInformation($"Requesting screenshot for {armResourceNode?.ResourceName} from dashboard: {dashboardUrlWithParameters}");
                var request = new HttpRequestMessage(HttpMethod.Post, $"{_puppeteerScreenshotApiUrl}/screenshot");

                var payload = new
                {
                    grafanaEndpoint = _grafanaUrl.TrimEnd('/'),
                    grafanaToken = await GetAccessTokenForGrafana(),
                    dashboardUrl = dashboardUrlWithParameters,
                };

                request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var stopwatch = Stopwatch.StartNew();
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                var responseJson = await response.Content.ReadAsStringAsync();

                stopwatch.Stop(); // Stop timing
                var duration = stopwatch.Elapsed; // Get the elapsed time

                var screenshotResponse = JsonSerializer.Deserialize<ScreenshotResponse>(responseJson);
                if (screenshotResponse == null)
                {
                    throw new Exception("Failed to deserialize screenshot response");
                }

                _logger.LogInternalInformation($"Successfully captured screenshot (Length: {screenshotResponse.Screenshot?.Length}) for {armResourceNode?.ResourceName} from dashboard: {dashboardUrlWithParameters}. Duration: {duration.TotalSeconds} seconds");
                return screenshotResponse;
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error capturing screenshot for {ResourceName} from dashboard {DashboardUrl}: {Message}", armResourceNode?.ResourceName, dashboardUrl, ex.Message);
        }

        return new ScreenshotResponse() { Screenshot = string.Empty };
    }

    private async Task<string> GetAccessTokenForGrafana()
    {
        return await _authenticationService.GetGrafanaAccessToken();
    }

    private string? GetParameterizedDashboardUrl(string dashboardUrl, ArmResourceNode armResourceNode)
    {
        if (armResourceNode == null || armResourceNode.ResourceType == null)
        {
            return dashboardUrl;
        }

        _dashboardsToProcessByResourceType.TryGetValue(armResourceNode.ResourceType, out var dashboardType);
        if (!string.IsNullOrEmpty(dashboardType))
        {
            var dashboardQueryParameters = GetQueryVariables(dashboardType, armResourceNode);
            return AddQueryParameters(dashboardUrl, dashboardQueryParameters);
        }

        return null;
    }

    private Dictionary<string, string> GetQueryVariables(string dashboardType, ArmResourceNode armResourceNode)
    {
        var baseVariables = new Dictionary<string, string>
        {
            { "var-ds", "azure-monitor-oob" },
            { "var-ns", armResourceNode.ResourceType ?? string.Empty.ToLowerInvariant()},
            { "var-sub", armResourceNode.SubscriptionId ?? string.Empty},
            { "var-rg", armResourceNode.ResourceGroupName ?? string.Empty.ToLowerInvariant() },
            { "var-resource", armResourceNode.ResourceName ?? string.Empty.ToLowerInvariant()}
        };
        var additionalVariables = dashboardType switch
        {
            "azure-insights-storage-accounts" => new Dictionary<string, string>
            {
            },
            "azure-container-apps-container-app-view" => new Dictionary<string, string>
            {
                { "var-containerapp", armResourceNode.ResourceName ?? string.Empty.ToLowerInvariant() }
            },
            "azure-insights-cosmos-db" => new Dictionary<string, string>
            {
            },
            "azure-redis" => new Dictionary<string, string>
            {
                { "var-name", armResourceNode.ResourceName ?? string.Empty.ToLowerInvariant() }
            },
            "azure-app-service" => new Dictionary<string, string>
            {
                { "var-name", armResourceNode.ResourceName ?? string.Empty.ToLowerInvariant() }
            },
            _ => []
        };
        return additionalVariables.Concat(baseVariables).ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    private Dictionary<string, string> GetHardcodedQueryVariablesForTesting(string dashboardType)
    {
        var baseVariables = new Dictionary<string, string>
        {
            { "var-ds", "azure-monitor-oob" },
            { "var-sub", "a058f7c6-592d-4490-887a-803e748787c0" }
        };

        var additionalVariables = dashboardType switch
        {
            "azure-insights-storage-accounts" => new Dictionary<string, string>
            {
                { "var-rg", "default-storage-southcentralus" },
                { "var-ns", "microsoft.storage/storageaccounts" },
                { "var-resource", "sanchitkube" }
            },
            "azure-container-apps-container-app-view" => new Dictionary<string, string>
            {
                { "var-ns", "microsoft.app/containerapps" },
                { "var-rg", "sessions-customcontainer-rg" },
                { "var-containerapp", "frontend-app" }
            },
            "azure-insights-cosmos-db" => new Dictionary<string, string>
            {
                { "var-ns", "Microsoft.DocumentDb/databaseAccounts" },
                { "var-rg", "lgn-rcp-rg-sanmeht1118" },
                { "var-resource", "lgn-rcp-db-sanmeht1118" }
            },
            "azure-redis" => new Dictionary<string, string>
            {
                { "var-rg", "capps-gpu-sessions-001-rg" },
                { "var-name", "cappsredis-e1d30" }
            },
            _ => []
        };

        return additionalVariables.Concat(baseVariables).ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    private string AddQueryParameters(string url, Dictionary<string, string> parameters)
    {
        // Return the original URL if there are no parameters to add
        if (string.IsNullOrEmpty(url) || parameters == null || parameters.Count == 0)
        {
            return url;
        }

        // Convert dictionary to a query string with URL-encoded parameters
        var queryString = string.Join("&", parameters
            .Select(param => $"{Uri.EscapeDataString(param.Key)}={Uri.EscapeDataString(param.Value)}"));

        // Determine the correct separator: ? if no query exists, otherwise use &
        var separator = url.Contains("?") ? '&' : '?';

        // Append the query string to the URL
        return $"{url}{separator}{queryString}";
    }

    public static string SanitizeFileName(string fileName)
    {
        // Remove invalid characters and trim spaces
        var sanitized = fileName.Trim();
        sanitized = sanitized.Replace("\\", "_"); // Remove backslashes
        sanitized = Regex.Replace(sanitized, @"\s+", " "); // Remove multiple spaces
        sanitized = Regex.Replace(sanitized, @"[<>:""/\\|?*]", ""); // Remove invalid filename characters

        return sanitized;
    }

    public async Task<string> SetupPrometheusDataSourceAsync(
        string grafanaUrl,
        string prometheusUrl,
        string dataSourceName,
        bool isDefault = false)
    {
        try
        {
            var token = await GetAccessTokenForGrafana();
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
            // Check if data source already exists
            var existingDs = await GetDataSourceByName(_httpClient, dataSourceName);
            if (existingDs != null)
            {
                _logger?.LogInternalInformation($"Data source '{dataSourceName}' already exists");
                // Extract UID from existing data source
                if (existingDs.Value.TryGetProperty("uid", out var uidElement))
                {
                    return uidElement.GetString() ?? string.Empty;
                }
                // Fallback if uid property doesn't exist directly
                return dataSourceName;
            }

            var payload = new
            {
                name = dataSourceName,
                type = "prometheus",
                url = prometheusUrl,
                access = "proxy",
                isDefault = isDefault,
                jsonData = new
                {
                    httpMethod = "POST",
                    timeInterval = "15s",
                    azureAuthType = "msi",
                    azureCredentials = new
                    {
                        authType = "msi"
                    },
                }
            };

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var jsonPayload = JsonSerializer.Serialize(payload, options);
            var content = new StringContent(
                jsonPayload,
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync($"{_grafanaUrl}/api/datasources", content);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger?.LogInternalError($"Failed to create data source: {response.StatusCode}, {errorContent}");
                throw new Exception($"Failed to create data source: {response.StatusCode}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(responseContent);
            var dataSourceUid = result.GetProperty("datasource").GetProperty("uid").GetString() ?? string.Empty;

            _logger?.LogInternalInformation($"Data source '{dataSourceName}' created successfully with UID: {dataSourceUid}");
            return dataSourceUid;
        }
        catch (Exception ex)
        {
            _logger?.LogInternalError($"Exception setting up Prometheus data source: {ex.Message}");
            throw new Exception($"Exception setting up Prometheus data source: {ex.Message}", ex);
        }
    }

    private async Task<JsonElement?> GetDataSourceByName(HttpClient client, string dataSourceName)
    {
        var response = await client.GetAsync($"{_grafanaUrl}/api/datasources");
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var content = await response.Content.ReadAsStringAsync();
        var dataSources = JsonSerializer.Deserialize<JsonElement>(content);

        // Check if the result is an array
        if (dataSources.ValueKind == JsonValueKind.Array)
        {
            foreach (var ds in dataSources.EnumerateArray())
            {
                if (ds.TryGetProperty("name", out var nameElement) &&
                    string.Equals(nameElement.GetString(), dataSourceName, StringComparison.OrdinalIgnoreCase))
                {
                    return ds;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Uses the LLM to summarize the dashboard screenshots
    /// </summary>
    private async Task<string> SummarizeDashboardScreenshotsAsync(Dictionary<string, string> screenshots, string? dashboardUrl)
    {
        try
        {
            // Create messages list for the LLM call
            var messages = new List<ChatMessage>();

            // Add system message with instructions
            messages.Add(new ChatMessage(ChatRole.System,
                 "You are an expert in analyzing Azure infrastructure and monitoring dashboards. You address yourself as Azure SRE Agent. " +
                 "Your task is to examine the provided dashboard screenshots for last 24 hours and create a very very detailed summary about usage and health of resources" +
                 "of the current system status, focusing on key metrics, trends, anomalies, and actionable insights. " +

                 "**Report Generation Goals**\n" +
                 "1. Analyze all dashboard components to obtain resource information\n" +
                 "2. Collect key metrics for the specified resources over the monitored timespan\n" +
                 "3. Identify patterns in the existing visualizations\n" +
                 "4. Present a clear summary of resource status and trends\n" +
                 "5. Highlight any anomalies or issues that need attention\n" +

                 "**Dashboard Analysis Approach**\n" +
                 "The analysis should include:\n" +
                 "- Evaluating resource counts by type\n" +
                 "- Assessing resource status (health, performance)\n" +
                 "- Identifying key metrics trends\n" +
                 "- Focus on Detecting anomaly\n" +

                 "**Summary Format**\n" +
                 "Structure your analysis as follows:\n" +
                 "1. Overview with key metrics\n" +
                 "2. Resource breakdown by type\n" +
                 "3. Performance trends\n" +
                 "4. Identified issues or anomalies\n" +
                 "5. Recommendations should be very actionable, do not offer suggestions (if applicable)\n" +

                 "**Response Format 📝**\n" +
                 "- Begin with the local time and date of the analysis\n" +
                 "- Use H3 headings only (###)\n" +
                 "- Include appropriate line breaks for readability\n" +
                 "- Put Azure IDs in code blocks\n" +
                 "- Use clear indicators: ✅ healthy, ⚠️ warning, ❌ critical\n" +
                 "- Provide reasoning for identified metrics and anomalies\n" +
                 "- Focus on actionable insights\n" +

                 "The final report should be comprehensive and provide a complete picture of the system status. " +
                 "When a user asks follow-up questions about the report, listen carefully to their needs and provide " +
                 "detailed, relevant information based on the dashboard data. "
             ));
            messages.Add(new ChatMessage(ChatRole.System, $"Analysis performed at: {DateTime.Now:yyyy-MM-dd HH:mm:ss} local time."));
            messages.Add(new ChatMessage(ChatRole.System, $"You have also plotted this summary Dashboard for the customer from my Knowledge Graph {dashboardUrl}. I must include this in my summary"));

            // Add a user message for each dashboard screenshot
            foreach (var screenshot in screenshots)
            {
                var imageData = Convert.FromBase64String(screenshot.Value);
                var content = new List<AIContent>
                {
                    new TextContent($"This is a screenshot of the '{screenshot.Key}' dashboard:"),
                    new DataContent(imageData, "image/png")
                };

                messages.Add(new ChatMessage(ChatRole.User, content));
            }

            messages.Add(new ChatMessage(ChatRole.User,
                "Based on all the dashboard screenshots provided, please generate a comprehensive summary " +
                "that highlights the current status of Azure resources, any notable issues or anomalies, significant trends, " +
                "and actionable recommendations. Structure your response with clear sections and prioritize the most important findings."));

            // Set up LLM options
            var options = new ChatOptions
            {
                Temperature = (float)0.2,
                AdditionalProperties = new AdditionalPropertiesDictionary
                {
                    ["response_format"] = "text"
                }
            };

            // Call the LLM to get the summary
            var response = await _chatClientProvider.GeneralPurposeModel.GetResponseAsync(messages, options);
            var summary = response.Messages.Count > 0 ? response.Messages[0].Text : "Unable to generate summary from dashboards.";
            try
            {
                var applications = await _graphDBPlugin.DiscoverApplications("a058f7c6-592d-4490-887a-803e748787c0");
                if (applications != null && applications.Count > 0)
                {
                    summary += $"Following are applications identified via knowledge graph connections; ie they are compute resources connected to data resources: {applications}";
                }
            }
            catch (Exception)
            {
                // Good to have
            }
            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error summarizing dashboards with LLM: {Message}", ex.Message);
            return $"Error generating dashboard visual summary: {ex.Message}";
        }
    }

    private async Task<RecommendedActionsAndObservations> GenerateSuggestedActionsAndOverallObservations(
        string dashboardSummary,
        string? dashboardUrl,
        CVESummary cveSummary,
        List<AppGroupResourceSummary> appHealthSummary,
        IncidentSummary incidentsSummary,
        string? codeOptimizationsSummary)
    {
        try
        {
            var messages = new List<ChatMessage>();

            // Convert all objects to JSON strings
            var context = new
            {
                DashboardSummary = dashboardSummary,
                CVESummary = cveSummary,
                AppHealthSummary = appHealthSummary,
                IncidentsSummary = incidentsSummary,
                CodeOptimizationsSummary = codeOptimizationsSummary
            };

            var jsonContext = JsonSerializer.Serialize(context, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            // Concise refinement instructions with context
            messages.Add(new ChatMessage(ChatRole.System,
                "You are Azure SRE Agent, an expert in analyzing Azure infrastructure and generating actionable insights. " +
                "I will provide you with monitoring data in JSON format that may include some or all of these sections:\n" +
                "- DashboardSummary: Overall system dashboard metrics and trends\n" +
                "- CVESummary: Security vulnerabilities and their details\n" +
                "- AppHealthSummary: Health status of applications across subscriptions. Healthy Apps will not have historical data. Unhealthy Apps will have detailed historical data for reference.\n" +
                "- IncidentsSummary: Active and recent incidents from PagerDuty and Azure Monitor\n" +
                "- CodeOptimizationsSummary: Code optimization recommendations for improving CPU, memory, or thread blocking issues\n\n" +
                "For any sections that are null, empty, or missing, simply ignore them and focus on the available data.\n\n" +
                "Here is the current monitoring data:\n\n" + jsonContext + "\n\n" +
                "Based on the available data, generate a structured response with prioritized actions and key observations. " +
                "Your response must be in this JSON format:\n" +
                "{\n" +
                "  \"actions\": [\n" +
                "    {\n" +
                "      \"priority\": \"High/Medium/Low\",\n" +
                "      \"description\": \"Clear, specific action to take\",\n" +
                "      \"eta\": \"Immediate/Today/Tomorrow/This week\"\n" +
                "    }\n" +
                "  ],\n" +
                "  \"observations\": [\n" +
                "    \"Clear, data-driven insights with specific metrics\"\n" +
                "  ]\n" +
                "}\n\n" +
                "Guidelines:\n" +
                "1. Only reference data that is actually present in the monitoring data\n" +
                "2. Prioritize by severity: Critical CVEs > High CVEs > Unhealthy Apps > Performance Issues\n" +
                "3. For each action, ensure the assignee and ETA are appropriate for the task\n" +
                "4. Include specific metrics and trends in observations when available\n" +
                "5. Suggest automated remediation for common issues\n" +
                "6. If no critical issues are found, focus on optimization and preventive measures"
            ));

            messages.Add(new ChatMessage(ChatRole.System, $"Analysis performed at: {DateTime.Now:yyyy-MM-dd HH:mm:ss} local time."));
            if (!string.IsNullOrEmpty(dashboardUrl))
            {
                messages.Add(new ChatMessage(ChatRole.System, $"Reference dashboard URL: {dashboardUrl}"));
            }

            messages.Add(new ChatMessage(ChatRole.User,
                "Generate a JSON response with prioritized actions and key observations based on the available monitoring data."));

            var options = new ChatOptions
            {
                Temperature = (float)0.2,
                AdditionalProperties = new AdditionalPropertiesDictionary
                {
                    ["response_format"] = "json"
                }
            };

            var response = await _chatClientProvider.GeneralPurposeModel.GetResponseAsync(messages, typeof(RecommendedActionsAndObservations), options);
            try
            {
                var result = (RecommendedActionsAndObservations?)response.result;
                if (result == null)
                {
                    throw new Exception("Deserialized result is null");
                }
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Failed to parse LLM response as JSON: {Response}", response.response.Messages[0].Text);
                return new RecommendedActionsAndObservations();
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Daily Report error occurred while generating suggested actions: {Message}", ex.Message);
            return new RecommendedActionsAndObservations();
        }
    }

    private async Task<(string?, string?)> CreateAndPublishDashboard(string dataSourceUid, CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(_mainDashboardFilePath))
            {
                _logger.LogInternalError("Dashboard template file not found: {FilePath}", _mainDashboardFilePath);
                return (null, null);
            }

            var dashboardJson = await File.ReadAllTextAsync(_mainDashboardFilePath);
            dashboardJson = dashboardJson.Replace("\"datasource\": \"KnowledgeGraph\"", $"\"datasource\": \"{_dataSourceName}\"");
            dashboardJson = dashboardJson.Replace("\"PROMETHEUS_UID\"", $"\"{dataSourceUid}\"");
            dashboardJson = dashboardJson.Replace("\"uid\": \"azure-sre-resources\"", $"\"uid\": \"{AgentNameHelper.GetMainDashboardUid(_hostEnvironment.IsProduction())}\"");

            // string dateFormatted = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            dashboardJson = dashboardJson.Replace("\"title\": \"SRE Azure Resource Overview\"",
                $"\"title\": \"{AgentNameHelper.GetCustomerAgentName(_hostEnvironment.IsProduction())} - Azure Resource Overview\"");

            // Ensure SRE folder exists (create if missing) and get its UID
            var (folderUid, folderId) = await EnsureSreFolderAsync(cancellationToken);

            // Get access token for Azure Managed Grafana
            var token = await GetAccessTokenForGrafana();

            var input = new DashboardInput
            {
                Name = "DS_PROMETHEUS",
                Type = "datasource",
                PluginId = "prometheus",
                Value = _dataSourceName,
            };

            // Publish dashboard to the Azure SRE Agent folder
            var dashboardUid = await PublishDashboardToManagedGrafana(dashboardJson, token, new[] { input }, folderUid, folderId);

            _logger.LogInternalInformation("Successfully published dashboard with UID: {DashboardUid} in SRE folder (uid={FolderUid})", dashboardUid, folderUid);
            var dashboardUrl = $"{_grafanaUrl}/d/{dashboardUid}";

            return (dashboardUrl, dashboardUid);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error creating and publishing dashboard: {Message}", ex.Message);
            return (null, null);
        }
    }

    private async Task<(string? folderUid, int? folderId)> EnsureSreFolderAsync(CancellationToken cancellationToken)
    {
        const string folderTitle = "Azure SRE Agent";
        const int maxAttempts = 3;
        var attempt = 0;

        // Early cancellation check
        cancellationToken.ThrowIfCancellationRequested();

        while (attempt < maxAttempts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            attempt++;
            try
            {
                var token = await GetAccessTokenForGrafana(); // (No CT overload available)

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

                // 1. Search for existing folder
                var searchResponse = await _httpClient.GetAsync(
                    $"{_grafanaUrl}/api/search?type=dash-folder&query={Uri.EscapeDataString(folderTitle)}",
                    cancellationToken);

                if (searchResponse.IsSuccessStatusCode)
                {
                    var json = await searchResponse.Content.ReadAsStringAsync(cancellationToken);
                    var arr = JsonSerializer.Deserialize<JsonElement>(json);
                    if (arr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in arr.EnumerateArray())
                        {
                            if (item.TryGetProperty("title", out var titleEl) &&
                                string.Equals(titleEl.GetString(), folderTitle, StringComparison.OrdinalIgnoreCase))
                            {
                                var existingUid = item.TryGetProperty("uid", out var uidEl) ? uidEl.GetString() : null;
                                int? existingId = item.TryGetProperty("id", out var idEl) ? idEl.GetInt32() : null;
                                if (!string.IsNullOrEmpty(existingUid))
                                {
                                    if (attempt > 1)
                                    {
                                        _logger.LogInternalInformation("SRE folder found after retry attempt {Attempt}", attempt);
                                    }
                                    return (existingUid, existingId);
                                }
                            }
                        }
                    }
                }

                // 2. Not found -> attempt create
                var createPayload = new { title = folderTitle };
                var createContent = new StringContent(JsonSerializer.Serialize(createPayload), Encoding.UTF8, "application/json");
                var createResponse = await _httpClient.PostAsync(
                    $"{_grafanaUrl}/api/folders",
                    createContent,
                    cancellationToken);

                if (createResponse.IsSuccessStatusCode)
                {
                    var createJson = await createResponse.Content.ReadAsStringAsync(cancellationToken);
                    var obj = JsonSerializer.Deserialize<JsonElement>(createJson);
                    var uid = obj.TryGetProperty("uid", out var uidEl2) ? uidEl2.GetString() : null;
                    int? id = obj.TryGetProperty("id", out var idEl2) ? idEl2.GetInt32() : null;
                    _logger.LogInternalInformation("Created SRE folder (attempt {Attempt}) uid={Uid} id={Id}", attempt, uid, id);
                    return (uid, id);
                }

                if (createResponse.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    _logger.LogInternalInformation("SRE folder creation returned 409 Conflict (attempt {Attempt}/{MaxAttempts}). Retrying search.", attempt, maxAttempts);
                    await Task.Delay(TimeSpan.FromMilliseconds(150 * attempt), cancellationToken);
                    continue;
                }

                var err = await createResponse.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogInternalWarning("Attempt {Attempt}/{MaxAttempts} failed to create SRE folder: {Status} {Error}", attempt, maxAttempts, createResponse.StatusCode, err);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInternalInformation("EnsureSreFolderAsync canceled at attempt {Attempt}", attempt);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogInternalWarning("Attempt {Attempt}/{MaxAttempts} exception ensuring SRE folder: {Message}", attempt, maxAttempts, ex.Message);
                await Task.Delay(TimeSpan.FromMilliseconds(150 * attempt), cancellationToken);
            }
        }

        _logger.LogInternalWarning("Exceeded maximum attempts ({MaxAttempts}) ensuring SRE folder.", maxAttempts);
        return (null, null);
    }

    private async Task<string> PublishDashboardToManagedGrafana(string dashboardJson, string accessToken, DashboardInput[] dashboardInputs, string? agentFolderUid = null, int? agentFolderId = null)
    {
        var dashboardObject = JsonSerializer.Deserialize<JsonElement>(dashboardJson);

        // Build the import request with either agentFolderUid or agentFolderId (Grafana accepts either)
        object importRequest = new
        {
            dashboard = dashboardObject,
            overwrite = true,
            folderUid = agentFolderUid,
            folderId = !string.IsNullOrEmpty(agentFolderUid) ? null : agentFolderId ?? (int?)0,
            inputs = dashboardInputs,
        };

        var requestJson = JsonSerializer.Serialize(importRequest);
        var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

        var response = await _httpClient.PostAsync($"{_grafanaUrl}/api/dashboards/db", content);
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogInternalError("Failed to publish dashboard: {StatusCode}, {ErrorContent}", response.StatusCode, errorContent);
            throw new Exception($"Failed to publish dashboard: {response.StatusCode}");
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        var responseObject = JsonSerializer.Deserialize<JsonElement>(responseContent);

        if (responseObject.TryGetProperty("uid", out var uidElement))
        {
            return uidElement.GetString() ?? string.Empty;
        }

        throw new Exception("Failed to get dashboard UID from response");
    }

    private async Task ActivateAzureMonitorDashboards(CancellationToken cancellationToken)
    {
        try
        {
            var token = await GetAccessTokenForGrafana();

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

            // Get list of available dashboards - this matches exactly the API call in your trace
            var availableDashboardsResponse = await _httpClient.GetAsync($"{_grafanaUrl}/api/plugins/grafana-azure-monitor-datasource/dashboards");
            availableDashboardsResponse.EnsureSuccessStatusCode();
            var availableDashboardsContent = await availableDashboardsResponse.Content.ReadAsStringAsync();
            var availableDashboards = JsonSerializer.Deserialize<JsonElement>(availableDashboardsContent);

            _logger.LogInternalInformation("Found {Count} available Azure Monitor dashboards", availableDashboards.GetArrayLength());

            // Dictionary to map dashboard titles to their path values
            var dashboardMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Extract dashboard information
            foreach (var dashboard in availableDashboards.EnumerateArray())
            {
                if (dashboard.TryGetProperty("title", out var titleElement) &&
                    dashboard.TryGetProperty("path", out var pathElement) &&
                    dashboard.TryGetProperty("imported", out var importedElement))
                {
                    var title = titleElement.GetString() ?? string.Empty;
                    var path = pathElement.GetString() ?? string.Empty;
                    var imported = importedElement.GetBoolean();

                    // Store only dashboards that aren't already imported
                    if (!imported)
                    {
                        dashboardMap[title] = path;
                    }
                    else
                    {
                        _logger.LogInternalInformation("Dashboard '{Title}' is already imported, skipping", title);
                    }
                }
            }

            if (dashboardMap.Count == 0)
            {
                _logger.LogInternalInformation("No new Azure Monitor dashboards available for import");
                return;
            }

            _logger.LogInternalInformation("Prepared {Count} dashboards available for import", dashboardMap.Count);

            // Ensure folder before import
            var (agentFolderUid, agentFolderId) = await EnsureSreFolderAsync(cancellationToken);

            // Activate each dashboard in our list
            foreach (var dashboardTitle in _dashboardsToActivate)
            {
                if (!dashboardMap.TryGetValue(dashboardTitle, out var dashboardPath))
                {
                    // Try to find a partial match
                    var matchingKey = dashboardMap.Keys.FirstOrDefault(k =>
                        k.Contains(dashboardTitle, StringComparison.OrdinalIgnoreCase) ||
                        dashboardTitle.Contains(k, StringComparison.OrdinalIgnoreCase));

                    if (matchingKey != null)
                    {
                        dashboardPath = dashboardMap[matchingKey];
                        _logger.LogInternalInformation("Found partial match for dashboard '{DashboardTitle}': '{MatchingTitle}'",
                            dashboardTitle, matchingKey);
                    }
                    else
                    {
                        _logger.LogInternalWarning("Dashboard '{DashboardTitle}' not found in available dashboards", dashboardTitle);
                        continue;
                    }
                }

                try
                {

                    // Create the import request exactly matching the format in your trace
                    var importRequest = new
                    {
                        pluginId = "grafana-azure-monitor-datasource",
                        path = dashboardPath,
                        overwrite = true,
                        folderUid = agentFolderUid,
                        folderId = !string.IsNullOrEmpty(agentFolderUid) ? null : agentFolderId ?? (int?)0,
                        inputs = new[]
                        {
                            new
                            {
                                name = "*",
                                type = "datasource",
                                pluginId = "grafana-azure-monitor-datasource",
                                value = "Azure Monitor"
                            }
                        }
                    };

                    var requestJson = JsonSerializer.Serialize(importRequest);
                    var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

                    // POST to the import endpoint exactly as shown in your trace
                    var response = await _httpClient.PostAsync($"{_grafanaUrl}/api/dashboards/import", content);
                    response.EnsureSuccessStatusCode();
                    var responseContent = await response.Content.ReadAsStringAsync();

                    _logger.LogInternalInformation("Successfully imported dashboard: {DashboardTitle}", dashboardTitle);
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, "Failed to import dashboard {DashboardTitle}: {Message}", dashboardTitle, ex.Message);
                }
            }

            _logger.LogInternalInformation("Completed activation of Azure Monitor dashboards");
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error activating Azure Monitor dashboards: {Message}", ex.Message);
        }
    }

    private async Task ActivateCustomizedDashboards(IList<string> excludes, CancellationToken cancellationToken)
    {
        var dashboards = Directory.GetFiles(_dashboardsDirectory, "*.json").Except(excludes);

        try
        {
            var accessToken = await GetAccessTokenForGrafana();
            var (folderUid, folderId) = await EnsureSreFolderAsync(cancellationToken);
            foreach (var dashboard in dashboards)
            {
                var dashboardJson = File.ReadAllText(dashboard);
                dashboardJson = dashboardJson.Replace("\"datasource\": \"KnowledgeGraph\"", $"\"datasource\": \"{_dataSourceName}\"");
                await PublishDashboardToManagedGrafana(dashboardJson, accessToken, Array.Empty<DashboardInput>(), folderUid, folderId);
                _logger.LogInternalInformation("Successfully published customized dashboard: {DashboardName}", Path.GetFileName(dashboard));
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error activating customize dashboards {Message}", ex.Message);
        }
    }

    private async Task<CVESummary> GetCVESummary()
    {
        _logger.LogInternalInformation("Fetching CVE summary...");
        var summary = new CVESummary();

        try
        {
            // Get all repositories from the graph database
            var unscannedRepos = await _graphDatabaseClient.Query(@"
                    g.V().has('resourceType', 'microsoft.source/repository').has('isDeleted', false)
                    .values('resourceId')");

            var repos = unscannedRepos
                .Select(x => (string)x)
                .OrderBy(resourceId => resourceId.Split("/").Last())
                .ToList();

            foreach (var repoUrl in repos)
            {
                try
                {
                    // Fetch GitHub security dependabot alerts for each repo
                    var vulnerabilities = await _githubIssuePlugin.FetchGithubSecurityDependabotAlerts(repoUrl);

                    // Filter vulnerabilities from the last 24 hours
                    var now = DateTime.UtcNow;
                    var last24Hours = now.AddDays(-1);

                    foreach (var vulnerability in vulnerabilities)
                    {
                        var cveInfo = new CVEInfo
                        {
                            RepoUrl = repoUrl,
                            Number = vulnerability.Number,
                            State = vulnerability.State,
                            Title = vulnerability.Title,
                            Description = vulnerability.Body
                        };

                        summary.Vulnerabilities.Add(cveInfo);

                        // Track vulnerabilities by repo
                        if (!summary.VulnerabilitiesByRepo.ContainsKey(repoUrl))
                        {
                            summary.VulnerabilitiesByRepo[repoUrl] = new List<string>();
                        }
                        summary.VulnerabilitiesByRepo[repoUrl].Add(vulnerability.Title);

                        // Update vulnerability counts
                        summary.TotalVulnerabilities++;
                        switch (vulnerability.Body.ToLowerInvariant())
                        {
                            case var s when s.Contains("critical"):
                                summary.CriticalVulnerabilities++;
                                break;
                            case var s when s.Contains("high"):
                                summary.HighVulnerabilities++;
                                break;
                            case var s when s.Contains("medium"):
                                summary.ModerateVulnerabilities++;
                                break;
                            case var s when s.Contains("low"):
                                summary.LowVulnerabilities++;
                                break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, "Error fetching CVE information for repo {RepoUrl}: {Message}", repoUrl, ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error generating CVE summary: {Message}", ex.Message);
        }

        return summary;
    }

    private async Task<List<AppGroupResourceSummary>> GetAppGroupsHealthSummaryAsync()
    {
        var result = new List<AppGroupResourceSummary>();
        var subscriptions = await _graphDBPlugin.ListSubscriptionsAsync();

        foreach (var sub in subscriptions)
        {
            var appGroups = await _graphDbService.GetAppGroupsBySubscriptionAsync(sub["id"]);
            var summary = new List<AppGroupResourceInfo>();

            if (appGroups != null)
            {
                foreach (var appGroup in appGroups)
                {
                    var properties = appGroup["properties"] as IDictionary<string, object>;
                    string appId = appGroup["id"]?.ToString() ?? string.Empty;

                    if (properties != null && appId != null && properties.TryGetValue("appHealthInfo", out var appHealthInfoObj) && appHealthInfoObj != null)
                    {
                        var options = new JsonSerializerOptions
                        {
                            IncludeFields = true,
                        };

                        var jsonStringList = ((IEnumerable<object>)appHealthInfoObj)
                            .OfType<string>()
                            .Where(s => !string.IsNullOrEmpty(s))
                            .ToList();

                        if (jsonStringList.Any())
                        {
                            // Get latest health data point
                            var latestHealthInfo = JsonSerializer.Deserialize<AppHealthInfo>(jsonStringList[0], options);

                            if (latestHealthInfo != null)
                            {
                                // Get health history from CosmosDB
                                try
                                {
                                    // Get the historical data document
                                    var historyDocument = await _appHealthHistoryRepository.GetAppHealthHistoryAsync(appId.Replace("_", "/"));

                                    if (historyDocument != null)
                                    {
                                        // Create aggregated view based on stored history
                                        var aggregatedHealthInfo = AggregateHealthInfoFromHistory(historyDocument);

                                        summary.Add(new AppGroupResourceInfo
                                        {
                                            Name = appGroup["name"]?.ToString() ?? string.Empty,
                                            Type = appGroup["type"]?.ToString() ?? string.Empty,
                                            AppHealthInfo = aggregatedHealthInfo ?? latestHealthInfo
                                        });
                                    }
                                    else
                                    {
                                        // No history document found, use latest health info only
                                        summary.Add(new AppGroupResourceInfo
                                        {
                                            Name = appGroup["name"]?.ToString() ?? string.Empty,
                                            Type = appGroup["type"]?.ToString() ?? string.Empty,
                                            AppHealthInfo = latestHealthInfo
                                        });
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogInternalError(ex, "Failed to retrieve app health history for {AppId}, using latest health info only", appId);

                                    // Fall back to using the latest health info only
                                    summary.Add(new AppGroupResourceInfo
                                    {
                                        Name = appGroup["name"]?.ToString() ?? string.Empty,
                                        Type = appGroup["type"]?.ToString() ?? string.Empty,
                                        AppHealthInfo = latestHealthInfo
                                    });
                                }
                            }
                        }
                    }
                }
            }

            result.Add(new AppGroupResourceSummary
            {
                SubscriptionId = sub["id"]?.ToString() ?? string.Empty,
                SubscriptionName = sub["name"]?.ToString() ?? string.Empty,
                AppGroups = summary
            });

            _logger.LogInternalInformation("Daily Report Processed {AppGroupCount} app groups for subscription {SubscriptionId}", summary.Count, (string?)sub["id"]?.ToString());
        }

        return result;
    }

    private AppHealthInfo? AggregateHealthInfoFromHistory(AppHealthHistoryDocument historyDocument)
    {
        if (historyDocument == null || historyDocument.HistoryData == null || !historyDocument.HistoryData.Any())
        {
            return null;
        }

        var healthInfos = historyDocument.HistoryData
            .Select(dp => new
            {
                HealthState = dp.Health,
                Availability = dp.Availability,
                CpuUsage = dp.AvgCpuUsage,
                MemoryUsage = dp.AvgMemoryUsage,
                Transactions = dp.Transactions,
                Timestamp = dp.LastDataCaptureTimeStampInUTC
            }).ToList();

        // Get the timestamp from the most recent data point
        var latestTimestamp = healthInfos.Max(h => h.Timestamp);

        // Determine overall health based on worst state in past 24 hours
        var aggregateHealth = DetermineAggregateHealth(healthInfos.Select(h => h.HealthState));

        return new AppHealthInfo
        {
            LastDataCaptureTimeStampInUTC = latestTimestamp,

            // Determine overall health based on worst state in past 24 hours
            Health = aggregateHealth,
            // Average metrics for last 24 hours
            Availability = healthInfos.Average(h => h.Availability),
            AvgCpuUsage = healthInfos.Average(h => h.CpuUsage),
            AvgMemoryUsage = healthInfos.Average(h => h.MemoryUsage),

            // Sum transactions over the period
            Transactions = healthInfos.Sum(h => h.Transactions ?? 0),

            // Include the 24-hour historical data
            // Only include historical data for unhealthy/degraded apps to reduce payload size
            HistoricalData = aggregateHealth == ScorecardHealthState.Healthy
                ? new List<HistoricalDataPoint>() // Empty list for healthy apps
                : healthInfos
                .OrderBy(h => h.Timestamp)
                .Select(h => new HistoricalDataPoint
                {
                    Timestamp = h.Timestamp,
                    Availability = h.Availability,
                    CpuUsage = h.CpuUsage,
                    MemoryUsage = h.MemoryUsage
                })
                .ToList()
        };
    }

    private static ScorecardHealthState DetermineAggregateHealth(IEnumerable<ScorecardHealthState> healthStatuses)
    {
        if (healthStatuses.Any(h => h == ScorecardHealthState.Unhealthy))
        {
            return ScorecardHealthState.Unhealthy;
        }

        if (healthStatuses.Any(h => h == ScorecardHealthState.Degraded))
        {
            return ScorecardHealthState.Degraded;
        }
        else
        {
            return ScorecardHealthState.Healthy;
        }
    }

    private async Task<IncidentSummary> GetIncidentsSummary()
    {
        _logger.LogInternalInformation("Fetching incidents summary...");

        var result = new IncidentSummary();

        // get all incidents from the last 24 hours
        var now = DateTime.UtcNow;
        var last24Hours = now.AddDays(-1);
        var pagerDutyIncidents = await _incidentRepository.GetAllPagerDutyIncidentsAsync();
        var azMonIncidents = await _incidentRepository.GetAllAzMonIncidentsAsync();

        // Filter incidents created in the last 24 hours
        pagerDutyIncidents = pagerDutyIncidents.Where(i => i.CreatedAt >= last24Hours).ToList();
        azMonIncidents = azMonIncidents.Where(i => i.CreatedAt >= last24Hours).ToList();

        _logger.LogInternalInformation("Daily Report Scanner found {pagerDutyIncidentCount} pagerDutyIncidents and {azMonIncidentCount} azMonIncidents in the last 24 hours. Summarizing incidents.", pagerDutyIncidents.Count, azMonIncidents.Count);

        // PagerDuty Incidents
        var pagerDutyIncidentsSummary = new List<IncidentInfo>();
        foreach (var incident in pagerDutyIncidents)
        {
            var analysisResult = await GenerateIncidentAnalysisAsync(incident.ToString(), incident.Status);

            // get incident thread (ie: thread in Cosmos where IncidentId = incident.Id)
            var threadId = "";
            // Get all threads and find the one with matching incident ID
            var allThreads = await _threadRepository.GetThreadsAsync(null);
            var incidentThread = allThreads.FirstOrDefault(t =>
                t.IncidentSource?.IncidentId == incident.Id);

            if (incidentThread != null)
            {
                threadId = incidentThread.Id.ToString();
            }

            pagerDutyIncidentsSummary.Add(
               new IncidentInfo
               {
                   IncidentId = incident.Id,
                   Name = incident.Title,
                   CreateTime = incident.CreatedAt,
                   Duration = DateTime.UtcNow - incident.CreatedAt,
                   Status = incident.Status,
                   Impact = analysisResult.Impact,
                   Resolution = analysisResult.Resolution,
                   InvestigationDetails = analysisResult.InvestigationDetails,
                   ThreadLink = GenerateThreadLink(threadId)
               });
        }

        result.PagerDuty = pagerDutyIncidentsSummary;

        // azure monitor
        var azMonIncidentsSummary = new List<IncidentInfo>();
        foreach (var incident in azMonIncidents)
        {
            var analysisResult = await GenerateIncidentAnalysisAsync(incident.ToString(), incident.Status);

            // get incident thread (ie: thread in Cosmos where IncidentId = incident.Id)
            var threadId = "";
            // Get all threads and find the one with matching incident ID
            var allThreads = await _threadRepository.GetThreadsAsync(null);
            var incidentThread = allThreads.FirstOrDefault(t =>
                t.Status?.IncidentStatus?.IncidentId == incident.Id);

            if (incidentThread != null)
            {
                threadId = incidentThread.Id.ToString();
            }

            var incidentSummary = new IncidentInfo
            {
                IncidentId = incident.Id,
                Name = incident.Title,
                CreateTime = incident.CreatedAt,
                Duration = DateTime.UtcNow - incident.CreatedAt,
                Status = incident.Status,
                Impact = analysisResult.Impact,
                Resolution = analysisResult.Resolution,
                InvestigationDetails = analysisResult.InvestigationDetails,
                ThreadLink = GenerateThreadLink(threadId)
            };

            azMonIncidentsSummary.Add(incidentSummary);
        }

        result.AzureMonitor = azMonIncidentsSummary;

        return result;
    }

    private async Task<string> GenerateThreadSummaryAsync()
    {
        var now = DateTime.UtcNow;
        var last24Hours = now.AddDays(-1);

        // Fetch threads created in the last 24 hours
        var recentThreads = await _threadRepository.GetThreadsAsync();
        // to do: stacy zeng - further break agent created threads by source code, security, etc threads
        var agentThreads = recentThreads
            .Where(t => t.CreatedTimestamp >= last24Hours && t.Source == ThreadSource.Agent)
            .ToList();
        var incidentThreads = recentThreads
            .Where(t => t.CreatedTimestamp >= last24Hours && t.Source == ThreadSource.Incident)
            .ToList();

        // Build the summary
        var threadSummary = new StringBuilder();
        threadSummary.AppendLine("### 💬 Check out these new threads from the past day:");
        threadSummary.AppendLine();

        // Add Alert Threads Section
        threadSummary.AppendLine($"#### 🛠️ SRE Agent identified issues and created {agentThreads.Count} new threads");
        threadSummary.AppendLine($"#### 🚨 Agent Created Threads new:");
        if (agentThreads.Any())
        {
            foreach (var thread in agentThreads)
            {
                threadSummary.AppendLine($"- **Title**: {thread.Title}");
                threadSummary.AppendLine($"  **Created**: {thread.CreatedTimestamp:yyyy-MM-dd HH:mm:ss} UTC");
            }
            _logger.LogInternalInformation("Added {Count} alert threads to the summary.", agentThreads.Count);
        }
        else
        {
            threadSummary.AppendLine("No new alert threads in the past 24 hours.");
            _logger.LogInternalInformation("No alert threads found in the past 24 hours.");
        }
        threadSummary.AppendLine();

        // Add Incident Threads Section
        threadSummary.AppendLine($"#### 🛠️ SRE Agent investigated and addressed {incidentThreads.Count} new incidents ");
        threadSummary.AppendLine($"#### 🔒 Incident Threads new:");
        if (incidentThreads.Any())
        {
            foreach (var thread in incidentThreads)
            {
                threadSummary.AppendLine($"- **Title**: {thread.Title}");
                threadSummary.AppendLine($"  **Created**: {thread.CreatedTimestamp:yyyy-MM-dd HH:mm:ss} UTC");
            }
            _logger.LogInternalInformation("Added {Count} incident threads to the summary.", incidentThreads.Count);
        }
        else
        {
            threadSummary.AppendLine("No new incident threads in the past 24 hours.");
            _logger.LogInternalInformation("No incident threads found in the past 24 hours.");
        }

        return threadSummary.ToString();
    }

    private string GenerateThreadLink(string threadId)
    {
        // If threadId is empty, return an empty string or placeholder link
        if (string.IsNullOrEmpty(threadId))
        {
            _logger.LogInternalWarning("No thread found for incident, cannot generate thread link");
            return string.Empty;
        }

        // to do: temporary, add logic to generate based on agent host name
        var agentHost = "https://portal.azure.com/";

        var queryString = "?feature.customPortal=false&feature.canmodifystamps=true&feature.fastmanifest=false&nocdn=force&websitesextension_loglevel=verbose&Microsoft_Azure_PaasServerless=beta&microsoft_azure_paasserverless_assettypeoptions=%7B%22SreAgentCustomMenu%22%3A%7B%22options%22%3A%22%22%7D%7D";

        // if local then append local flag
        var environment = _configuration["ASPNETCORE_ENVIRONMENT"];
        if (string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase))
        {
            queryString += "&Microsoft_Azure_PaasServerless_sre_local=true";
        }

        var deepLinkPath = $"%2Fviews%2Factivities%2Fthreads%2F{threadId}";
        var hash = $"#view/Microsoft_Azure_PaasServerless/AgentFrameBlade.ReactView/sreLink/{deepLinkPath}/id/%2F";

        return $"{agentHost}{queryString}{hash}";
    }

    public class ScreenshotResponse
    {
        [JsonPropertyName("screenshot")]
        public string Screenshot { get; set; } = string.Empty;
    }

    public class DashboardInput
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;
        [JsonPropertyName("pluginId")]
        public string PluginId { get; set; } = string.Empty;
        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;
    }

    private ReportOverview GenerateOverview(CVESummary cveSummary, IncidentSummary incidentsSummary, List<AppGroupResourceSummary> appGroupsHealthSummary)
    {
        var overview = new ReportOverview();

        // Populate Security Findings overview
        if (cveSummary != null)
        {
            overview.SecurityFindings.Critical = cveSummary.CriticalVulnerabilities;
            overview.SecurityFindings.High = cveSummary.HighVulnerabilities;
            overview.SecurityFindings.Moderate = cveSummary.ModerateVulnerabilities;
            overview.SecurityFindings.Low = cveSummary.LowVulnerabilities;
            overview.SecurityFindings.TotalCount = cveSummary.TotalVulnerabilities;
        }

        // Populate Incidents overview
        if (incidentsSummary != null)
        {
            // Count active incidents (non-closed)
            overview.Incidents.Active =
                (incidentsSummary.PagerDuty?.Count(i => i.Status != "closed" && i.Status != "resolved") ?? 0) +
                (incidentsSummary.AzureMonitor?.Count(i => i.Status != "closed" && i.Status != "resolved") ?? 0);

            // Count mitigated incidents (acknowledged)
            overview.Incidents.Mitigated =
                (incidentsSummary.PagerDuty?.Count(i => i.Status == "acknowledged") ?? 0) +
                (incidentsSummary.AzureMonitor?.Count(i => i.Status == "acknowledged") ?? 0);

            // Count resolved incidents
            overview.Incidents.Resolved =
                (incidentsSummary.PagerDuty?.Count(i => i.Status == "closed" || i.Status == "resolved") ?? 0) +
                (incidentsSummary.AzureMonitor?.Count(i => i.Status == "closed" || i.Status == "resolved") ?? 0);

            // Total count
            overview.Incidents.TotalCount =
                (incidentsSummary.PagerDuty?.Count ?? 0) +
                (incidentsSummary.AzureMonitor?.Count ?? 0);
        }

        // Populate Health and Performance overview
        if (appGroupsHealthSummary != null)
        {
            var healthy = 0;
            var degraded = 0;
            var unhealthy = 0;

            foreach (var appGroup in appGroupsHealthSummary)
            {
                foreach (var app in appGroup.AppGroups)
                {
                    if (app.AppHealthInfo != null)
                    {
                        switch (app.AppHealthInfo.Health)
                        {
                            case ScorecardHealthState.Healthy:
                                healthy++;
                                break;
                            case ScorecardHealthState.Degraded:
                                degraded++;
                                break;
                            case ScorecardHealthState.Unhealthy:
                                unhealthy++;
                                break;
                        }
                    }
                }
            }

            overview.HealthAndPerformance.Healthy = healthy;
            overview.HealthAndPerformance.Degraded = degraded;
            overview.HealthAndPerformance.Unhealthy = unhealthy;
            overview.HealthAndPerformance.TotalCount = healthy + degraded + unhealthy;
        }

        return overview;
    }

    /// <summary>
    /// Generates consolidated incident analysis (impact, resolution, investigation) in a single LLM call
    /// </summary>
    private async Task<IncidentAnalysisResult> GenerateIncidentAnalysisAsync(string incidentInfo, string incidentStatus)
    {
        var isClosedIncident = string.Equals(incidentStatus, "closed", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(incidentStatus, "resolved", StringComparison.OrdinalIgnoreCase);

        const int maxAttempts = 2; // Initial attempt + 1 retry

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                var messages = new List<ChatMessage>();

                messages.Add(new ChatMessage(ChatRole.System,
                    "You are an Azure SRE Agent analyzing incident information. " +
                    "Generate a consolidated analysis of the incident including impact, resolution details (if closed), and investigation status. " +
                    "Your response must be in this JSON format:\n" +
                    "{\n" +
                    "  \"impact\": \"Brief, one sentence summary of the incident impact and key impact points\",\n" +
                    "  \"resolution\": \"Brief, one sentence summary of how the incident was resolved (only if status is closed/resolved, otherwise empty string)\",\n" +
                    "  \"investigationDetails\": \"Brief, one sentence summary of investigation steps taken\"\n" +
                    "}\n\n" +
                    "Guidelines:\n" +
                    "- Keep each response concise and focused (one sentence each)\n" +
                    "- For closed incidents: provide impact, resolution, and investigationDetails\n" +
                    "- For open incidents: provide impact and investigationDetails, leave resolution empty\n" +
                    "- Focus on actionable information and key insights\n" +
                    $"- The incident status is: {incidentStatus}"));

                messages.Add(new ChatMessage(ChatRole.User,
                    $"Analyze this incident information and provide the consolidated analysis:\n\n{incidentInfo}"));

                var options = new ChatOptions
                {
                    Temperature = 0.2f,
                    AdditionalProperties = new AdditionalPropertiesDictionary
                    {
                        ["response_format"] = "json"
                    }
                };

                var response = await _chatClientProvider.GeneralPurposeModel.GetResponseAsync(messages, typeof(IncidentAnalysisResult), options);
                try
                {
                    var result = (IncidentAnalysisResult?)response.result;
                    if (result == null)
                    {
                        _logger.LogInternalWarning("Deserialized result is null for incident analysis, attempt {Attempt}/{MaxAttempts}",
                            attempt + 1, maxAttempts);

                        // If this is the last attempt, break out of the loop
                        if (attempt == maxAttempts - 1)
                        {
                            break;
                        }

                        // Wait 1 second before retry
                        await Task.Delay(TimeSpan.FromSeconds(1));
                        continue;
                    }

                    return result;
                }
                catch (JsonException ex)
                {
                    _logger.LogInternalWarning(ex, "JSON deserialization failed for incident analysis, attempt {Attempt}/{MaxAttempts}: {Message}",
                        attempt + 1, maxAttempts, ex.Message);

                    // If this is the last attempt, break out of the loop
                    if (attempt == maxAttempts - 1)
                    {
                        break;
                    }

                    // Wait 1 second before retry
                    await Task.Delay(TimeSpan.FromSeconds(1));
                    continue;
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error generating consolidated incident analysis on attempt {Attempt}/{MaxAttempts}: {Message}",
                    attempt + 1, maxAttempts, ex.Message);

                // If this is the last attempt, break out of the loop
                if (attempt == maxAttempts - 1)
                {
                    break;
                }

                // Wait 1 second before retry
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }

        // Return fallback result if all attempts failed
        return new IncidentAnalysisResult
        {
            Impact = "Error generating impact analysis.",
            Resolution = isClosedIncident ? "Error generating resolution summary." : string.Empty,
            InvestigationDetails = !isClosedIncident ? "Error generating investigation summary." : string.Empty
        };
    }
}
