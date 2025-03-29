using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Plugins;
using Agent.Runtime.Services;
using Azure.Identity;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Thread = Agent.Core.Models.Api.v1.Thread;

namespace Agent.Runtime.SubAgents.DailyReportSummary
{
    public class DailyReportScanner
    {
        private readonly ILogger<DailyReportScanner> _logger;
        private readonly DurableTaskClient _durableTaskClient;
        private readonly IThreadRepository _threadRepository;
        private readonly DailyReportSummaryAgentFactory _dailyReportSummaryAgentFactory;
        private readonly IAgentInboundCommunicationService _agentInboundCommunicationService;
        private readonly IGraphDatabaseClient _graphDatabaseClient;
        private readonly IGrafanaPlugin _grafanaPlugin;
        private readonly IGraphDBPlugin _graphDBPlugin;
        private readonly HttpClient _httpClient;
        private readonly IChatClient _chatClient;
        private static bool didItOnce = false;
        private readonly string _dashboardsDirectory;
        private readonly string _mainDashboardFilePath;
        private readonly string _grafanaUrl;
        private readonly string _prometheusUrl;
        private readonly string _dataSourceName;
        private readonly DefaultAzureCredential _azureCredential;
        private readonly List<string> _dashboardsToActivate;
        private readonly string _puppeteerScreenshotApiUrl;
        private readonly DashboardSettings _dashboardSettings;
        private readonly IGraphDbService _graphDbService;
        private readonly bool _persistScreenshotsInFolder;
        private List<ArmResourceNode> _armResourceNodes;

        private readonly string DashboardScreenshotsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DashboardScreenshots");

        private const string SreDashboard = "/d/azure-sre-resources/sre-azure-resource-overview";
        private readonly Dictionary<string, string> _dashboardsToProcessByResourceType = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "microsoft.app/containerapps", "azure-container-apps-container-app-view" },
            { "microsoft.storage/storageaccounts", "azure-insights-storage-accounts" },
            { "microsoft.documentdb/databaseaccounts", "azure-insights-cosmos-db" },
            { "microsoft.cache/redis", "azure-redis" }
            // Pending: webapp, sql
        };

        public DailyReportScanner(
            DurableTaskClient durableTaskClient,
            IThreadRepository threadRepository,
            DailyReportSummaryAgentFactory dailyReportSummaryAgentFactory,
            ILogger<DailyReportScanner> logger,
            IAgentInboundCommunicationService agentInboundCommunicationService,
            IGraphDatabaseClient graphDatabaseClient,
            IGrafanaPlugin grafanaPlugin,
            IGraphDBPlugin graphDBPlugin,
            HttpClient httpClient,
            IChatClient chatClient,
            DashboardSettings dashboardSettings,
            IGraphDbService graphDbService,
            string mainDashboardFile = "Main-Dashboard.json",
            string puppeteerScreenshotApiUrl = "https://test-capp.ambitiouspond-10f27fe1.canadaeast.azurecontainerapps.io")
        {
            _logger = logger;
            _durableTaskClient = durableTaskClient;
            _threadRepository = threadRepository;
            _dailyReportSummaryAgentFactory = dailyReportSummaryAgentFactory;
            _agentInboundCommunicationService = agentInboundCommunicationService;
            _graphDatabaseClient = graphDatabaseClient;
            _grafanaPlugin = grafanaPlugin;
            _httpClient = httpClient;
            _chatClient = chatClient;
            _dashboardsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SubAgents", "DailySummaryAgent", "Dashboards");
            _mainDashboardFilePath = Path.Combine(_dashboardsDirectory, mainDashboardFile);
            _grafanaUrl = dashboardSettings.GrafanaUrl.TrimEnd('/');
            _prometheusUrl = dashboardSettings.PrometheusUrl.TrimEnd('/');
            _graphDBPlugin = graphDBPlugin;
            // _dataSourceName = dashboardSettings.GrafanaDataSourceName;
            _dataSourceName = "KnowledgeGraph";
            _puppeteerScreenshotApiUrl = puppeteerScreenshotApiUrl;
            //_puppeteerScreenshotApiUrl = "http://20.57.166.55:3000";//puppeteerScreenshotApiUrl;
            _azureCredential = new DefaultAzureCredential();

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
                "d/azure-sre-resources/sre-azure-resource-overview"
            };
            _dashboardSettings = dashboardSettings;
            _graphDbService = graphDbService;
            _persistScreenshotsInFolder = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PERSIST_SCREENSHOTS"));
        }

        public async Task<Thread?> ScanAndGenerateReport(CancellationToken cancellationToken)
        {
            // Check if a report agent is already running
            var runningAgents = await _durableTaskClient.GetAllInstancesAsync(new OrchestrationQuery
            {
                Statuses = new[] { OrchestrationRuntimeStatus.Running },
                InstanceIdPrefix = DailyReportSummaryAgentFactory.OrchestrationInstanceIdPrefix
            }).ToListAsync();

            if (runningAgents.Count > 0)
            {
                _logger.LogInformation("Daily report summary agent already running, skipping this run.");
                return null;
            }

            // Check if we need to run the daily report (e.g., only during certain hours)
            var now = DateTime.UtcNow;
            var todayReportTime = new DateTime(now.Year, now.Month, now.Day, 7, 0, 0, DateTimeKind.Utc); // 7 AM UTC

            // Skip if it's not time yet for the daily report
            if (now.Hour != todayReportTime.Hour && didItOnce)
            {
                _logger.LogDebug("Not time for daily report yet. Current hour: {CurrentHour}, Target hour: {TargetHour}",
                    now.Hour, todayReportTime.Hour);
                return null;
            }

            didItOnce = true;

            // Get the list of resource types from the knowledge graph
            _armResourceNodes = await _graphDbService.GetAllResourceNodes();
            _logger.LogInformation("Found {Count} resource nodes in the knowledge graph", _armResourceNodes.Count);

            // Create and publish custom dashboard
            string uid = await SetupPrometheusDataSourceAsync(_grafanaUrl, _prometheusUrl, _dataSourceName);
            (string dashboardUrl, string dashboardUid) = await CreateAndPublishDashboard(uid);

            // Activate predefined Azure Monitor dashboards
            await ActivateAzureMonitorDashboards();

            // Create a thread for the report
            var dateFormatted = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            // NEW: Capture screenshots of all dashboards and get LLM summary before starting orchestration
            _logger.LogInformation("Capturing dashboard screenshots for LLM analysis");
            var dashboardSummary = await CaptureAndSummarizeDashboardsAsync(dashboardUrl);

            var conciseSummary = await GenerateConciseSummaryAsync(dashboardSummary, dashboardUrl);

            var screenshot = (await CaptureDashboardScreenshotAsync(SreDashboard, armResourceNode: null)).Screenshot;
            PersistScreenshot("SreDashboard", screenshot);

            var initialMessage = $"{conciseSummary}\n\n" + $"**I created this dashboard for you to give an overview : [SRE Agent Resource Dashboard]({dashboardUrl})**\n\n";
            var thread = await _agentInboundCommunicationService.CreateAgentThread(
                $"Daily Resources Report - {dateFormatted}\n\n",
                initialMessage);

            // Append the screenshot as separate message, this message will be excluded from the chat history to LLM due to token limitation.
            await _agentInboundCommunicationService.AppendAgentImageMessage(thread.Id, $"![DailyReport Dashboard](data:image/png;base64,{screenshot})\r\n");

            // Prepare the input for the agent
            var input = new DailyReportSummaryInput
            {
                ReportType = "Daily",
                Timespan = "1d",
                DashboardSummary = dashboardSummary
            };

            // Start the agent orchestration
            var instanceId = await _dailyReportSummaryAgentFactory.StartOrchestration(input, thread.Id.ToString());

            _logger.LogInformation("Started daily report generation with instance ID: {InstanceId}", instanceId);

            // Wait for completion or handle timeout
            try
            {
                using (var timeoutCts = new CancellationTokenSource(TimeSpan.FromHours(1))) // 1 hour timeout
                using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken))
                {
                    await _durableTaskClient.WaitForInstanceCompletionAsync(instanceId, linkedCts.Token);
                    _logger.LogInformation("Daily report generation completed successfully.");
                }
            }
            catch (OperationCanceledException)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Daily report generation was cancelled.");
                }
                else
                {
                    _logger.LogWarning("Daily report generation timed out.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error waiting for daily report generation: {Message}", ex.Message);
            }
            return thread;
        }

        /// <summary>
        /// Captures screenshots of all dashboards and uses LLM to summarize them
        /// </summary>
        private async Task<string> CaptureAndSummarizeDashboardsAsync(string dashboardUrl)
        {
            try
            {
                var token = GetAccessTokenForGrafana();

                // Dictionary to store dashboard screenshots (name -> base64 image)
                Dictionary<string, string> dashboardScreenshots = new Dictionary<string, string>();

                // Get list of all available dashboards in Grafana
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

                var dashboardsResponse = await _httpClient.GetAsync($"{_grafanaUrl}/api/search?type=dash-db");
                dashboardsResponse.EnsureSuccessStatusCode();

                var dashboardsContent = await dashboardsResponse.Content.ReadAsStringAsync();
                var dashboards = JsonSerializer.Deserialize<JsonElement>(dashboardsContent);

                _logger.LogInformation("Found {Count} dashboards to capture", dashboards.GetArrayLength());

                // Capture each dashboard
                foreach (var dashboard in dashboards.EnumerateArray())
                {
                    if (dashboard.TryGetProperty("url", out var urlElement) &&
                        dashboard.TryGetProperty("title", out var titleElement) &&
                        dashboard.TryGetProperty("type", out var itemType) &&
                        string.Equals(itemType.GetString(), "dash-db", StringComparison.OrdinalIgnoreCase))
                    {
                        string url = urlElement.GetString();
                        string title = titleElement.GetString();

                        try
                        {
                            // Capture the dashboard screenshot
                            var (resourceType, dashboardType) = _dashboardsToProcessByResourceType.FirstOrDefault(resourceType => url.Contains(resourceType.Value, StringComparison.OrdinalIgnoreCase));
                            if (string.IsNullOrEmpty(dashboardType))
                            {
                                _logger.LogWarning("Failed to capture screenshot for dashboard: {Title}, Reason: Dashboard type not supported", title);
                                continue;
                            }

                            // Capture screenshot for each arm resource node
                            var screenshotResponses = new List<ScreenshotResponse>();
                            var armResourceNodes = _armResourceNodes.Where(a => a.ResourceType.Equals(resourceType, StringComparison.OrdinalIgnoreCase));

                            foreach (var armResourceNode in armResourceNodes)
                            {
                                screenshotResponses.Add(await CaptureDashboardScreenshotAsync(url, armResourceNode));
                            }

                            if (!screenshotResponses.Any())
                            {
                                _logger.LogWarning("Failed to capture screenshot for dashboard: {Title}, Reason: No screenshot received for {ArmResourceCount} {ResourceType} resources", title, armResourceNodes.Count(), resourceType);
                                continue;
                            }

                            int index = 0; // To handle multiple screenshots for the same dashboard if needed
                            foreach (var screenshotResponse in screenshotResponses)
                            {
                                var indexedTitle = title + ++index;
                                var base64Image = screenshotResponse?.Screenshot;
                                if (!string.IsNullOrEmpty(base64Image))
                                {
                                    PersistScreenshot(indexedTitle, base64Image);
                                    dashboardScreenshots.Add(indexedTitle, base64Image);
                                    _logger.LogInformation($"Successfully captured screenshot for dashboard: {title}", title);
                                }
                            }

                            _logger.LogInformation($"[Summary] Successfully captured {index} screenshots for resource type: {resourceType}");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error capturing screenshot for dashboard {Title}: {Message}", title, ex.Message);
                        }
                    }
                }

                // If we have any screenshots, use LLM to summarize them
                if (dashboardScreenshots.Count > 0)
                {
                    _logger.LogInformation("Summarizing {Count} dashboard screenshots using LLM", dashboardScreenshots.Count);
                    return await SummarizeDashboardScreenshotsAsync(dashboardScreenshots, dashboardUrl);
                }
                else
                {
                    _logger.LogWarning("No dashboard screenshots were captured, skipping LLM summarization");
                    return "No dashboard screenshots available for analysis.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error capturing and summarizing dashboard screenshots: {Message}", ex.Message);
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
            byte[] imageBytes = Convert.FromBase64String(base64Image);

            // Save the image to a file
            var filePath = Path.Combine(DashboardScreenshotsFolder, $"{SanitizeFileName(title)}-{DateTime.Now.ToString("HHmmss")}.jpg");
            File.WriteAllBytes(filePath, imageBytes);

            _logger.LogInformation($"Image saved successfully at: {Path.GetFullPath(filePath)}");
        }

        /// <summary>
        /// Captures a screenshot of a specific dashboard in Grafana
        /// </summary>
        private async Task<ScreenshotResponse> CaptureDashboardScreenshotAsync(string dashboardUrl, ArmResourceNode armResourceNode)
        {
            try
            {
                string? dashboardUrlWithParameters = GetParameterizedDashboardUrl(dashboardUrl, armResourceNode);

                if (!string.IsNullOrEmpty(dashboardUrlWithParameters))
                {
                    _logger.LogInformation($"Requesting screenshot for {armResourceNode.ResourceName} from dashboard: {dashboardUrlWithParameters}");
                    var request = new HttpRequestMessage(HttpMethod.Post, $"{_puppeteerScreenshotApiUrl}/screenshot");

                    var payload = new
                    {
                        grafanaEndpoint = _grafanaUrl.TrimEnd('/'),
                        grafanaToken = GetAccessTokenForGrafana(),
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

                    _logger.LogInformation($"Succesfully captured screenshot (Length: {screenshotResponse?.Screenshot?.Length}) for {armResourceNode.ResourceName} from dashboard: {dashboardUrlWithParameters}. Duration: {duration.TotalSeconds} seconds");
                    return screenshotResponse;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error capturing screenshot for {ResourceName} from dashboard {DashboardUrl}: {Message}", armResourceNode.ResourceName, dashboardUrl, ex.Message);
            }

            return new ScreenshotResponse() { Screenshot = string.Empty };
        }

        private string? GetParameterizedDashboardUrl(string dashboardUrl, ArmResourceNode armResourceNode)
        {
            if (armResourceNode == null)
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
                { "var-ns", armResourceNode.ResourceType.ToLowerInvariant() },
                { "var-sub", armResourceNode.SubscriptionId },
                { "var-rg", armResourceNode.ResourceGroupName.ToLowerInvariant() },
                { "var-resource", armResourceNode.ResourceName.ToLowerInvariant() }
            };
            var additionalVariables = dashboardType switch
            {
                "azure-insights-storage-accounts" => new Dictionary<string, string>
                {
                },
                "azure-container-apps-container-app-view" => new Dictionary<string, string>
                {
                    { "var-containerapp", armResourceNode.ResourceName.ToLowerInvariant() }
                },
                "azure-insights-cosmos-db" => new Dictionary<string, string>
                {
                },
                "azure-redis" => new Dictionary<string, string>
                {
                    { "var-name", armResourceNode.ResourceName.ToLowerInvariant() }
                },
                _ => null
            };
            return additionalVariables?.Concat(baseVariables).ToDictionary(kv => kv.Key, kv => kv.Value);
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
                _ => null
            };

            return additionalVariables?.Concat(baseVariables).ToDictionary(kv => kv.Key, kv => kv.Value);
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
            char separator = url.Contains("?") ? '&' : '?';

            // Append the query string to the URL
            return $"{url}{separator}{queryString}";
        }

        public static string SanitizeFileName(string fileName)
        {
            // Remove invalid characters and trim spaces
            string sanitized = fileName.Trim();
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
                string token = GetAccessTokenForGrafana();
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                // Check if data source already exists
                var existingDs = await GetDataSourceByName(_httpClient, dataSourceName);
                if (existingDs != null)
                {
                    _logger?.LogInformation($"Data source '{dataSourceName}' already exists");
                    // Extract UID from existing data source
                    if (existingDs.Value.TryGetProperty("uid", out var uidElement))
                    {
                        return uidElement.GetString();
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
                        timeInterval = "15s"
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
                    _logger?.LogError($"Failed to create data source: {response.StatusCode}, {errorContent}");
                    throw new Exception($"Failed to create data source: {response.StatusCode}");
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<JsonElement>(responseContent);
                string dataSourceUid = result.GetProperty("datasource").GetProperty("uid").GetString();

                _logger?.LogInformation($"Data source '{dataSourceName}' created successfully with UID: {dataSourceUid}");
                return dataSourceUid;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Exception setting up Prometheus data source: {ex.Message}");
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
        private async Task<string> SummarizeDashboardScreenshotsAsync(Dictionary<string, string> screenshots, string dashboardUrl)
        {
            try
            {
                // Create messages list for the LLM call
                var messages = new List<ChatMessage>();

                // Add system message with instructions
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
                     "- Use H3 headings only (###) with professional emojis\n" +
                     "- Include appropriate line breaks for readability\n" +
                     "- Put Azure IDs in code blocks\n" +
                     "- Use clear indicators: ✅ healthy, ⚠️ warning, ❌ critical\n" +
                     "- Provide reasoning for identified metrics and anomalies\n" +
                     "- Focus on actionable insights\n" +

                     "The final report should be comprehensive and provide a complete picture of the system status. " +
                     "When a user asks follow-up questions about the report, listen carefully to their needs and provide " +
                     "detailed, relevant information based on the dashboard data. "
                 ));
                messages.Add(new ChatMessage(ChatRole.System, $"Analysis performed at: {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")} local time."));
                messages.Add(new ChatMessage(ChatRole.System, $"You have also plotted this summary Dashboard for the customer from my Knowledge Graph {dashboardUrl}. I must include this in my summary"));

                // Add a user message for each dashboard screenshot
                foreach (var screenshot in screenshots)
                {
                    byte[] imageData = Convert.FromBase64String(screenshot.Value);
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
                var response = await _chatClient.GetResponseAsync(messages, options);
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
                _logger.LogError(ex, "Error summarizing dashboards with LLM: {Message}", ex.Message);
                return $"Error generating dashboard visual summary: {ex.Message}";
            }
        }

        private async Task<string> GenerateConciseSummaryAsync(string detailedSummary, string dashboardUrl)
        {
            var messages = new List<ChatMessage>();

            // Concise refinement instructions
            messages.Add(new ChatMessage(ChatRole.System,
                "You are Azure SRE Agent. Your task is now to refine the provided detailed dashboard summary " +
                "into a very concise version. Focus solely on the most critical insights, key metrics, and actionable findings. " +
                "Eliminate any unnecessary details while retaining the essence of the dashboard analysis." +
                "**Response Format 📝**\n" +
                "- Begin with the local time and date of the analysis\n" +
                "- Use H3 headin{gs only (###) with professional emojis\n" +
                "- Include appropriate line breaks for readability\n" +
                "- Put Azure IDs in code blocks\n" +
                "- Use clear indicators: ✅ healthy, ⚠️ warning, ❌ critical\n" +
                "- Provide reasoning for identified metrics and anomalies\n" +
                "- Focus on actionable insights\n"
            ));
            messages.Add(new ChatMessage(ChatRole.System, $"Refinement performed at: {DateTime.Now:yyyy-MM-dd HH:mm:ss} local time."));
            messages.Add(new ChatMessage(ChatRole.System, $"Reference dashboard URL: {dashboardUrl}"));
            messages.Add(new ChatMessage(ChatRole.User,
                $"Here is the detailed summary:\n\n{detailedSummary}\n\n" +
                $"If there is a gremlin json(Very concise), summarize it in max 1-2 lines.\n\n" +
                $"Notify: Activated the Default Dashboards on Azure Managed Grafana for Cosmos, WebApp, Redis, ContainerApp, Storage. Used some of these dashboard to produce the summary for today\n\n" +
                $"Include: Here are the results of Critical CVEs detected by Application Groups: nextjs-contianerapp with connected source code: https://github.com/serverless-paas-balam/sreagent-demo-nextjsauth, has a critical CVE: Authorization Bypass in Next.js Middleware #1\r\nImpact\r\nIt is possible to bypass authorization checks within a Next.js application, if the authorization check occurs in middleware.\r\n\r\nPatches\r\nFor Next.js 15.x, this issue is fixed in 15.2.3\r\nFor Next.js 14.x, this issue is fixed in 14.2.25\r\nFor Next.js 13.x, this issue is fixed in 13.5.9\r\nFor Next.js 12.x, this issue is fixed in 12.3.5\r\nFor Next.js 11.x, consult the below workaround.\r\nNote: Next.js deployments hosted on Vercel are automatically protected against this vulnerability. User should pay attention to this ASAP" +
                "Please produce a very concise version highlighting only the most important points, <important>include the CVE Summary with repo url and app name.</important>"
            ));

            var options = new ChatOptions
            {
                Temperature = 0.2f,
                AdditionalProperties = new AdditionalPropertiesDictionary
                {
                    ["response_format"] = "text"
                }
            };

            var response = await _chatClient.GetResponseAsync(messages, options);
            return response.Messages.Count > 0 ? response.Messages[0].Text : "Unable to generate concise summary.";
        }

        private async Task<(string, string)> CreateAndPublishDashboard(string dataSourceUid)
        {
            try
            {
                if (!File.Exists(_mainDashboardFilePath))
                {
                    _logger.LogError("Dashboard template file not found: {FilePath}", _mainDashboardFilePath);
                    return (null, null);
                }

                string dashboardJson = await File.ReadAllTextAsync(_mainDashboardFilePath);
                dashboardJson = dashboardJson.Replace("\"datasource\": \"Prometheus\"", $"\"datasource\": \"{_dataSourceName}\"");
                dashboardJson = dashboardJson.Replace("\"PROMETHEUS_UID\"", $"\"{dataSourceUid}\"");

                string dateFormatted = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                dashboardJson = dashboardJson.Replace("\"title\": \"Resource Monitoring Dashboard\"",
                    $"\"title\": \"Resource Monitoring Dashboard - {dateFormatted}\"");

                // Get access token for Azure Managed Grafana
                var token = GetAccessTokenForGrafana();

                // Publish dashboard directly to Azure Managed Grafana
                var dashboardUid = await PublishDashboardToManagedGrafana(dashboardJson, token);

                _logger.LogInformation("Successfully published dashboard with UID: {DashboardUid}", dashboardUid);
                string dashboardUrl = $"{_grafanaUrl}/d/{dashboardUid}";

                return (dashboardUrl, dashboardUid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating and publishing dashboard: {Message}", ex.Message);
                return (null, null);
            }
        }

        private string GetAccessTokenForGrafana()
        {
            return _dashboardSettings.GrafanaApiKey ?? throw new Exception("Grafana API TOKEN not found");
        }

        private async Task<string> PublishDashboardToManagedGrafana(string dashboardJson, string accessToken)
        {
            var dashboardObject = JsonSerializer.Deserialize<JsonElement>(dashboardJson);

            // Prepare the dashboard import request
            var importRequest = new
            {
                dashboard = dashboardObject,
                overwrite = true,
                folderId = 0,
                inputs = new[]
                {
                    new
                    {
                        name = "DS_PROMETHEUS",
                        type = "datasource",
                        pluginId = "prometheus",
                        value = _dataSourceName
                    }
                }
            };

            var requestJson = JsonSerializer.Serialize(importRequest);
            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            var token = GetAccessTokenForGrafana();
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

            var response = await _httpClient.PostAsync($"{_grafanaUrl}/api/dashboards/db", content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var responseObject = JsonSerializer.Deserialize<JsonElement>(responseContent);

            if (responseObject.TryGetProperty("uid", out var uidElement))
            {
                return uidElement.GetString();
            }

            throw new Exception("Failed to get dashboard UID from response");
        }

        private async Task ActivateAzureMonitorDashboards()
        {
            try
            {
                var token = GetAccessTokenForGrafana();

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

                // Get list of available dashboards - this matches exactly the API call in your trace
                var availableDashboardsResponse = await _httpClient.GetAsync($"{_grafanaUrl}/api/plugins/grafana-azure-monitor-datasource/dashboards");
                availableDashboardsResponse.EnsureSuccessStatusCode();
                var availableDashboardsContent = await availableDashboardsResponse.Content.ReadAsStringAsync();
                var availableDashboards = JsonSerializer.Deserialize<JsonElement>(availableDashboardsContent);

                _logger.LogInformation("Found {Count} available Azure Monitor dashboards", availableDashboards.GetArrayLength());

                // Dictionary to map dashboard titles to their path values
                var dashboardMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                // Extract dashboard information
                foreach (var dashboard in availableDashboards.EnumerateArray())
                {
                    if (dashboard.TryGetProperty("title", out var titleElement) &&
                        dashboard.TryGetProperty("path", out var pathElement) &&
                        dashboard.TryGetProperty("imported", out var importedElement))
                    {
                        string title = titleElement.GetString();
                        string path = pathElement.GetString();
                        bool imported = importedElement.GetBoolean();

                        // Store only dashboards that aren't already imported
                        if (!imported)
                        {
                            dashboardMap[title] = path;
                        }
                        else
                        {
                            _logger.LogInformation("Dashboard '{Title}' is already imported, skipping", title);
                        }
                    }
                }

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
                            _logger.LogInformation("Found partial match for dashboard '{DashboardTitle}': '{MatchingTitle}'",
                                dashboardTitle, matchingKey);
                        }
                        else
                        {
                            _logger.LogWarning("Dashboard '{DashboardTitle}' not found in available dashboards", dashboardTitle);
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

                        _logger.LogInformation("Successfully imported dashboard: {DashboardTitle}", dashboardTitle);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to import dashboard {DashboardTitle}: {Message}", dashboardTitle, ex.Message);
                    }
                }

                _logger.LogInformation("Completed activation of Azure Monitor dashboards");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error activating Azure Monitor dashboards: {Message}", ex.Message);
            }
        }
    }

    public class ScreenshotResponse
    {
        [JsonPropertyName("screenshot")]
        public string Screenshot { get; set; }
    }
}
