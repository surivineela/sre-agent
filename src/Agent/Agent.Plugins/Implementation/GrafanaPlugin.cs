// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.AI;
using System.ComponentModel;
using System.Text.Json;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Plugins.Interface;
using Agent.Framework;

namespace Agent.Plugins.Implementation
{
    public class GrafanaPlugin : IGrafanaPlugin
    {
        private readonly ILogger<GrafanaPlugin> _logger;
        private readonly DashboardSettings _dashboardSettings;
        private readonly IChatClientProvider _chatClientProvider;
        private readonly IAuthenticationService _authService;
        private readonly IHttpClientFactory _httpClientFactory;
        public GrafanaPlugin(ILogger<GrafanaPlugin> logger, DashboardSettings dashboardSettings, IChatClientProvider chatClientProvider, IAuthenticationService authService, IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
            _dashboardSettings = dashboardSettings;
            _chatClientProvider = chatClientProvider;
            _authService = authService;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }


        private async Task<HttpClient> CreateAuthenticatedClientAsync()
        {

            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(_dashboardSettings.GrafanaUrl);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var token = await _authService.GetGrafanaAccessToken();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            return client;
        }

        // Individual methods kept for flexibility
        public async Task<string> PublishDashboard(
            string dashboardJson,
            bool overwrite = true)
        {
            try
            {
                using (var client = await CreateAuthenticatedClientAsync())
                {
                    var dashboardObject = JsonConvert.DeserializeObject<JObject>(dashboardJson);

                    var payload = new
                    {
                        dashboard = dashboardObject,
                        overwrite = overwrite,
                        message = $"Dashboard updated at {DateTime.UtcNow:o}"
                    };

                    var content = new StringContent(
                        JsonConvert.SerializeObject(payload),
                        Encoding.UTF8,
                        "application/json");

                    var response = await client.PostAsync("/api/dashboards/db", content);

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger?.LogInternalError($"Failed to publish dashboard: {response.StatusCode}, {errorContent}");
                        return $"Failed to publish dashboard: {response.StatusCode}";
                    }

                    var responseContent = await response.Content.ReadAsStringAsync();
                    dynamic? result = JsonConvert.DeserializeObject(responseContent);
                    string dashboardUid = result?.uid ?? string.Empty;

                    _logger?.LogInternalInformation($"Dashboard published successfully with UID: {dashboardUid}");
                    return dashboardUid; // Return just the UID for easier chaining
                }
            }
            catch (Exception ex)
            {
                _logger?.LogInternalError($"Exception publishing dashboard: {ex.Message}");
                throw new Exception($"Exception publishing dashboard: {ex.Message}", ex);
            }
        }

        public async Task<byte[]> CaptureScreenshot(
            string dashboardUid,
            int width = 1920,
            int height = 1080)
        {
            try
            {
                using (var client = await CreateAuthenticatedClientAsync())
                {
                    client.Timeout = TimeSpan.FromSeconds(60);

                    var renderUrl = $"/render/d/{dashboardUid}?orgId=1&from=now-1h&to=now&width={width}&height={height}&theme=light";
                    var response = await client.GetAsync(renderUrl);

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger?.LogInternalError($"Failed to capture dashboard: {response.StatusCode}, {errorContent}");
                        throw new Exception($"Failed to capture dashboard: {response.StatusCode}");
                    }

                    _logger?.LogInternalInformation($"Screenshot captured for dashboard with UID: {dashboardUid}");
                    return await response.Content.ReadAsByteArrayAsync();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogInternalError($"Exception capturing dashboard: {ex.Message}");
                throw new Exception($"Exception capturing dashboard: {ex.Message}", ex);
            }
        }

        [Description(
            "Modifies an existing Grafana dashboard based on user-requested changes. " +
            "The dashboard can be specified by UID or by name. " +
            "If specified by name, it will first look up the UID. " +
            "Then it publishes the dashboard and returns the URL."
        )]
        public async Task<string> ModifyGrafanaDashboard(
            [Description("Description of changes the user wants")] string description,
            [Description("Name of the target dashboard")] string dashboardName,
            [Description("Optional dashboard UID - will look up by name if not provided")] string existingDashboardUid = "")
        {
            string dashboardJson;
            string contextMessage;

            // If existingDashboardUid is not provided but dashboardName is,
            // try to look up the dashboard UID by name
            if (string.IsNullOrEmpty(existingDashboardUid) && !string.IsNullOrEmpty(dashboardName))
            {
                _logger?.LogInternalInformation($"No dashboard UID provided, looking up dashboard by name: {dashboardName}");
                existingDashboardUid = await GetDashboardUidByName(dashboardName);

                if (string.IsNullOrEmpty(existingDashboardUid))
                {
                    _logger?.LogInternalWarning($"No dashboard found with name: {dashboardName}");
                }
                else
                {
                    _logger?.LogInternalInformation($"Found dashboard with name '{dashboardName}' and UID: {existingDashboardUid}");
                }
            }

            // Check if we're modifying an existing dashboard
            if (!string.IsNullOrEmpty(existingDashboardUid))
            {
                try
                {
                    // Fetch the existing dashboard JSON
                    dashboardJson = await GetDashboardJson(existingDashboardUid);
                    contextMessage = "You are modifying an existing Grafana dashboard. Preserve its structure and metrics " +
                                    "while incorporating the requested changes.";
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to retrieve dashboard with UID '{existingDashboardUid}': {ex.Message}", ex);
                }
            }
            else
            {
                return "Couldn't find the dashboard";
            }

            // Build the prompt for the LLM
            var systemMessage = new ChatMessage(
                ChatRole.System,
                $"Analysis performed at: {DateTime.Now:yyyy-MM-dd HH:mm:ss} local time. " +
                "You are an expert at generating valid Grafana dashboard JSON. " +
                contextMessage
            );

            var userPrompt =
                "# Grafana Dashboard Update Request\n\n" +
                $"## Current Dashboard\n" +
                "Below is the " + (string.IsNullOrEmpty(existingDashboardUid) ? "template" : "existing") + " Grafana dashboard JSON.\n\n" +
                "## Required Modifications\n" +
                $"1. Set dashboard title to: '{dashboardName}'\n" +
                $"2. Implement these specific changes:\n{description}\n\n" +
                "## Important Instructions\n" +
                "- Preserve all existing functionality not mentioned in changes\n" +
                "- Ensure dashboard maintains proper structure and relationships\n" +
                "- Keep modifications minimal and focused on the requested changes\n" +
                "- Return ONLY valid JSON without any comments, explanations, or markdown\n\n" +
                $"---BEGIN DASHBOARD JSON---\n{dashboardJson}\n---END DASHBOARD JSON---";

            var userMessage = new ChatMessage(ChatRole.User, userPrompt);

            var messages = new List<ChatMessage> { systemMessage, userMessage };
            var chatOptions = new ChatOptions
            {
                Temperature = 0.2f,
                AdditionalProperties = new AdditionalPropertiesDictionary
                {
                    ["response_format"] = "json"
                }
            };

            // Make the LLM call
            var llmResponse = await _chatClientProvider.DefaultModel.GetResponseAsync(messages, chatOptions);
            if (llmResponse.Messages.Count == 0)
            {
                throw new Exception("No response from LLM.");
            }

            var updatedDashboardJson = llmResponse.Messages[0].Text;

            // Publish the updated dashboard
            var dashboardUid = await PublishDashboard(updatedDashboardJson, overwrite: true);

            // Build and return the URL to the new dashboard
            var dashboardUrl = $"{_dashboardSettings.GrafanaUrl}/d/{dashboardUid}?orgId=1";
            return dashboardUrl;
        }

        /// <summary>
        /// Retrieves the JSON for an existing dashboard by its UID
        /// </summary>
        private async Task<string> GetDashboardJson(string dashboardUid)
        {
            using (var httpClient = await CreateAuthenticatedClientAsync())
            {
                var response = await httpClient.GetAsync($"/api/dashboards/uid/{dashboardUid}");

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to retrieve dashboard. Status: {response.StatusCode}");
                }

                var responseJson = await response.Content.ReadAsStringAsync();

                // Parse the response to extract just the dashboard part
                // Grafana API returns a wrapper object with dashboard, meta, etc.
                try
                {
                    using (JsonDocument doc = JsonDocument.Parse(responseJson))
                    {
                        JsonElement root = doc.RootElement;

                        if (root.TryGetProperty("dashboard", out JsonElement dashboard))
                        {
                            return dashboard.GetRawText();
                        }
                        else
                        {
                            throw new Exception("Dashboard property not found in response");
                        }
                    }
                }
                catch (System.Text.Json.JsonException ex)
                {
                    throw new Exception($"Error parsing dashboard JSON: {ex.Message}");
                }
            }
        }

        public async Task<string> SetupPrometheusDataSource(
            string dataSourceName,
            bool isDefault = false)
        {
            try
            {
                using (var client = await CreateAuthenticatedClientAsync())
                {
                    // Check if data source already exists
                    var existingDs = await GetDataSourceByName(client, dataSourceName);
                    if (existingDs != null)
                    {
                        _logger?.LogInternalInformation($"Data source '{dataSourceName}' already exists with UID: {existingDs.uid}");
                        return existingDs.uid;
                    }

                    var payload = new
                    {
                        name = dataSourceName,
                        type = "prometheus",
                        url = "https://gremlin.agreeablewave-1dce2c4c.northcentralusstage.azurecontainerapps.io",
                        access = "proxy",
                        isDefault = isDefault,
                        jsonData = new
                        {
                            httpMethod = "POST",
                            timeInterval = "15s"
                        }
                    };

                    var content = new StringContent(
                        JsonConvert.SerializeObject(payload),
                        Encoding.UTF8,
                        "application/json");

                    var response = await client.PostAsync("/api/datasources", content);

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger?.LogInternalError($"Failed to create data source: {response.StatusCode}, {errorContent}");
                        throw new Exception($"Failed to create data source: {response.StatusCode}");
                    }

                    var responseContent = await response.Content.ReadAsStringAsync();
                    dynamic? result = JsonConvert.DeserializeObject(responseContent);
                    string dataSourceUid = result?.datasource?.uid ?? string.Empty;

                    _logger?.LogInternalInformation($"Data source '{dataSourceName}' created successfully with UID: {dataSourceUid}");
                    return dataSourceUid; // Return just the UID for easier chaining
                }
            }
            catch (Exception ex)
            {
                _logger?.LogInternalError($"Exception setting up Prometheus data source: {ex.Message}");
                throw new Exception($"Exception setting up Prometheus data source: {ex.Message}", ex);
            }
        }

        public async Task<string> LinkDataSourceToDashboard(
            string dashboardUid,
            string dataSourceUid)
        {
            try
            {
                using (var client = await CreateAuthenticatedClientAsync())
                {
                    // Get the dashboard
                    var dashboardResponse = await client.GetAsync($"/api/dashboards/uid/{dashboardUid}");

                    if (!dashboardResponse.IsSuccessStatusCode)
                    {
                        var errorContent = await dashboardResponse.Content.ReadAsStringAsync();
                        _logger?.LogInternalError($"Failed to retrieve dashboard: {dashboardResponse.StatusCode}, {errorContent}");
                        throw new Exception($"Failed to retrieve dashboard: {dashboardResponse.StatusCode}");
                    }

                    var dashboardContent = await dashboardResponse.Content.ReadAsStringAsync();
                    dynamic? dashboardData = JsonConvert.DeserializeObject(dashboardContent);
                    var dashboard = dashboardData?.dashboard ?? string.Empty;

                    // Get data source details to get its name and type
                    var dsResponse = await client.GetAsync($"/api/datasources/uid/{dataSourceUid}");

                    if (!dsResponse.IsSuccessStatusCode)
                    {
                        var errorContent = await dsResponse.Content.ReadAsStringAsync();
                        _logger?.LogInternalError($"Failed to retrieve data source: {dsResponse.StatusCode}, {errorContent}");
                        throw new Exception($"Failed to retrieve data source: {dsResponse.StatusCode}");
                    }

                    var dsContent = await dsResponse.Content.ReadAsStringAsync();
                    dynamic? dsData = JsonConvert.DeserializeObject(dsContent);
                    string dsType = dsData?.type ?? string.Empty;
                    string dsName = dsData?.name ?? string.Empty;

                    // Update dashboard panels to use the data source
                    bool panelsUpdated = UpdateDashboardPanels(dashboard, dataSourceUid, dsType);

                    if (!panelsUpdated)
                    {
                        _logger?.LogInternalWarning($"No panels found in dashboard '{dashboardUid}' to update");
                    }

                    // Re-publish the dashboard with updated data source
                    var payload = new
                    {
                        dashboard = dashboard,
                        overwrite = true,
                        message = $"Updated dashboard to use data source '{dsName}' at {DateTime.UtcNow:o}"
                    };

                    var content = new StringContent(
                        JsonConvert.SerializeObject(payload),
                        Encoding.UTF8,
                        "application/json");

                    var response = await client.PostAsync("/api/dashboards/db", content);

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger?.LogInternalError($"Failed to update dashboard with data source: {response.StatusCode}, {errorContent}");
                        throw new Exception($"Failed to update dashboard with data source: {response.StatusCode}");
                    }

                    _logger?.LogInternalInformation($"Dashboard '{dashboardUid}' successfully linked to data source '{dsName}'");
                    return $"Dashboard '{dashboardUid}' successfully linked to data source '{dsName}'";
                }
            }
            catch (Exception ex)
            {
                _logger?.LogInternalError($"Exception linking data source to dashboard: {ex.Message}");
                throw new Exception($"Exception linking data source to dashboard: {ex.Message}", ex);
            }
        }

        // Combined method that performs all three operations
        public async Task<string> PublishDashboardWithPrometheusDataSource(
            string dashboardJson,
            string dataSourceName,
            bool isDefault = false)
        {
            try
            {
                using (var client = await CreateAuthenticatedClientAsync())
                {
                    // Step 1: Setup Prometheus data source
                    string dataSourceUid = await SetupPrometheusDataSource(
                        dataSourceName,
                        isDefault);

                    _logger?.LogInternalInformation($"Step 1 complete: Prometheus data source ready with UID '{dataSourceUid}'");

                    // Step 2: Pre-process dashboard JSON to update data source references
                    var dashboard = JsonConvert.DeserializeObject<JObject>(dashboardJson);

                    // Get data source type
                    var dsResponse = await client.GetAsync($"/api/datasources/uid/{dataSourceUid}");

                    if (!dsResponse.IsSuccessStatusCode)
                    {
                        throw new Exception($"Failed to retrieve data source details: {dsResponse.StatusCode}");
                    }

                    string dsContent = await dsResponse.Content.ReadAsStringAsync() ?? string.Empty;
                    dynamic? dsData = JsonConvert.DeserializeObject(dsContent);
                    string dsType = dsData?.type ?? string.Empty;

                    // Update panels in the dashboard JSON
                    UpdateDashboardPanelsJson(dashboard, dataSourceUid, dsType);

                    // Step 3: Publish the dashboard with data source already configured
                    var payload = new
                    {
                        dashboard = dashboard,
                        overwrite = true,
                        message = $"Dashboard with Prometheus data source created at {DateTime.UtcNow:o}"
                    };

                    var content = new StringContent(
                        JsonConvert.SerializeObject(payload),
                        Encoding.UTF8,
                        "application/json");

                    var response = await client.PostAsync("/api/dashboards/db", content);

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        throw new Exception($"Failed to publish dashboard: {response.StatusCode}, {errorContent}");
                    }

                    var responseContent = await response.Content.ReadAsStringAsync();
                    dynamic? result = JsonConvert.DeserializeObject(responseContent);
                    string dashboardUid = result?.uid ?? string.Empty;

                    _logger?.LogInternalInformation($"Dashboard published and linked to Prometheus data source");

                    return $"Dashboard published with UID '{dashboardUid}' and linked to Prometheus data source '{dataSourceName}'";
                }
            }
            catch (Exception ex)
            {
                _logger?.LogInternalError($"Exception in combined operation: {ex.Message}");
                throw new Exception($"Failed to complete the dashboard publishing workflow: {ex.Message}", ex);
            }
        }

        #region Helper Methods

        private async Task<dynamic?> GetDataSourceByName(HttpClient client, string dataSourceName)
        {
            var response = await client.GetAsync("/api/datasources");

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            dynamic? dataSources = JsonConvert.DeserializeObject(content);

            if (dataSources == null)
            {
                return null;
            }

            foreach (var ds in dataSources)
            {
                if (string.Equals((string)ds.name, dataSourceName, StringComparison.OrdinalIgnoreCase))
                {
                    return ds;
                }
            }

            return null;
        }

        private bool UpdateDashboardPanels(dynamic dashboard, string dataSourceUid, string dsType)
        {
            bool panelsUpdated = false;

            // Update each panel to use the specified data source
            if (dashboard.panels != null)
            {
                foreach (var panel in dashboard.panels)
                {
                    panel.datasource = new
                    {
                        type = dsType,
                        uid = dataSourceUid
                    };
                    panelsUpdated = true;
                }
            }

            // Handle nested panels in rows if they exist
            if (dashboard.rows != null)
            {
                foreach (var row in dashboard.rows)
                {
                    if (row.panels != null)
                    {
                        foreach (var panel in row.panels)
                        {
                            panel.datasource = new
                            {
                                type = dsType,
                                uid = dataSourceUid
                            };
                            panelsUpdated = true;
                        }
                    }
                }
            }

            return panelsUpdated;
        }

        private void UpdateDashboardPanelsJson(JObject? dashboard, string dataSourceUid, string dsType)
        {
            if (dashboard == null)
            {
                return;
            }
            // Update panels array
            if (dashboard["panels"] is JArray panels)
            {
                foreach (JObject panel in panels)
                {
                    panel["datasource"] = JObject.FromObject(new
                    {
                        type = dsType,
                        uid = dataSourceUid
                    });
                }
            }

            // Update panels in rows
            if (dashboard["rows"] is JArray rows)
            {
                foreach (JObject row in rows)
                {
                    if (row["panels"] is JArray rowPanels)
                    {
                        foreach (JObject panel in rowPanels)
                        {
                            panel["datasource"] = JObject.FromObject(new
                            {
                                type = dsType,
                                uid = dataSourceUid
                            });
                        }
                    }
                }
            }

            // Add a template variable for the data source
            if (dashboard == null)
            {
                dashboard = new JObject();
            }

            if (!(dashboard["templating"] is JObject templating))
            {
                templating = new JObject();
                dashboard["templating"] = templating;
            }

            if (!(templating["list"] is JArray list))
            {
                list = new JArray();
                templating["list"] = list;
            }

            // Add data source variable to templating
            var dsVariable = JObject.FromObject(new
            {
                name = "DS_PROMETHEUS",
                label = "Prometheus Data Source",
                type = "datasource",
                query = dsType,
                current = new
                {
                    value = dataSourceUid,
                    text = dsType
                },
                hide = 0
            });

            list.Add(dsVariable);
        }

        /// <summary>
        /// Looks up a dashboard UID by name using the Grafana search API
        /// </summary>
        public async Task<string> GetDashboardUidByName(string dashboardName)
        {
            try
            {
                using (var client = await CreateAuthenticatedClientAsync())
                {
                    // Get list of all available dashboards in Grafana using the search API
                    var dashboardsResponse = await client.GetAsync($"{_dashboardSettings.GrafanaUrl}/api/search?type=dash-db");
                    dashboardsResponse.EnsureSuccessStatusCode();

                    var dashboardsContent = await dashboardsResponse.Content.ReadAsStringAsync();
                    var dashboards = JsonDocument.Parse(dashboardsContent).RootElement;

                    // Search for the dashboard by name (case-insensitive)
                    foreach (var dashboard in dashboards.EnumerateArray())
                    {
                        if (dashboard.TryGetProperty("title", out var titleElement) &&
                            dashboard.TryGetProperty("uid", out var uidElement) &&
                            dashboard.TryGetProperty("type", out var typeElement) &&
                            string.Equals(typeElement.GetString(), "dash-db", StringComparison.OrdinalIgnoreCase))
                        {
                            string title = titleElement.GetString() ?? string.Empty;

                            // If we find a matching title, return the uid
                            if (string.Equals(title, dashboardName, StringComparison.OrdinalIgnoreCase))
                            {
                                return uidElement.GetString() ?? string.Empty;
                            }
                        }
                    }

                    // If we got here, no matching dashboard was found
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogInternalError($"Error looking up dashboard by name: {ex.Message}");
                throw new Exception($"Error looking up dashboard by name: {ex.Message}", ex);
            }
        }

        #endregion
    }
}
