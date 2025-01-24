using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace OperationalAgentRuntime.Cli
{
    // Simple container for success/failure messages
    public sealed record RemediationResult(
    bool Success,
    string Action,
    string Details
    );


    public class GrafanaPlugin
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _httpClient;

        public GrafanaPlugin(IConfiguration config)
        {
            _config = config;
            _httpClient = new HttpClient();
        }

        [KernelFunction("deploy_grafana_dashboard")]
        [Description("Generates a Grafana dashboard JSON for specified metrics, then pushes it to managed Grafana.")]
        public async Task<RemediationResult> DeployGrafanaDashboardAsync(
            [Description("The name/title to give the dashboard")] string dashboardName,
            [Description("Comma-separated list of metrics to include")] string metrics)
        {
            try
            {
                // Example: metrics = "cpu_usage,memory_usage,disk_iops"  
                var metricsArray = metrics
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                // Build the dashboard JSON (panels, etc.)  
                string dashboardJson = BuildDashboardJson(dashboardName, metricsArray);
                string token = await GetAccessTokenAsync();
                string grafanaEndpoint = _config["Grafana:Endpoint"];
                if (string.IsNullOrEmpty(grafanaEndpoint))
                {
                    return new RemediationResult(
                        false,
                        "DeployGrafanaDashboard",
                        "No 'Grafana:Endpoint' found in configuration");
                }

                string requestUrl = $"{grafanaEndpoint.TrimEnd('/')}/api/dashboards/db";

                // Prepare request to Grafana  
                var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
                {
                    Content = new StringContent(dashboardJson, Encoding.UTF8, "application/json")
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // Call Grafana API  
                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    return new RemediationResult(
                        false,
                        "DeployGrafanaDashboard",
                        $"Failed to deploy dashboard ({response.StatusCode}): {errorBody}"
                    );
                }

                return new RemediationResult(
                    true,
                    "DeployGrafanaDashboard",
                    $"Successfully created/updated the '{dashboardName}' dashboard with {metricsArray.Length} metric panels."
                );
            }
            catch (Exception ex)
            {
                return new RemediationResult(false, "DeployGrafanaDashboard", ex.Message);
            }
        }

        /// <summary>  
        /// Builds a minimal dashboard JSON with panels for each given metric.  
        /// Adapt this method to structure your panels or queries the way Grafana expects.  
        /// </summary>  
        private string BuildDashboardJson(string dashboardName, string[] metrics)
        {
            // This is a bare-bones sample. In real usage, you'd structure  
            // each panel with your actual data source, query, etc.  

            var panels = new List<object>();
            int panelId = 1;
            foreach (var metric in metrics)
            {
                panels.Add(new
                {
                    id = panelId++,
                    title = $"Panel for {metric}",
                    type = "graph",
                    // Example: a very basic query panel referencing some placeholder metric variable  
                    targets = new[]
                    {
                    new {
                        refId = "A",
                        expr = metric, // Adjust to your data source / query  
                    }
                }
                });
            }

            // Grafana’s /api/dashboards/db expects a container object with "dashboard" and "overwrite"  
            var dashboardSpec = new
            {
                dashboard = new
                {
                    title = dashboardName,
                    panels = panels
                },
                overwrite = true
            };

            // Serialize as JSON  
            return JsonSerializer.Serialize(dashboardSpec, new JsonSerializerOptions { WriteIndented = true });
        }

        /// <summary>  
        /// Retrieves an Azure AD token suitable for Azure Managed Grafana using DefaultAzureCredential.  
        /// Make sure your principal has proper permissions on the Managed Grafana resource.  
        /// </summary>  
        private async Task<string> GetAccessTokenAsync()
        {
            var credential = new DefaultAzureCredential();
            var tokenRequestContext = new TokenRequestContext(new[] { "https://management.azure.com/.default" });
            var token = await credential.GetTokenAsync(tokenRequestContext);
            return token.Token;
        }
    }
}
