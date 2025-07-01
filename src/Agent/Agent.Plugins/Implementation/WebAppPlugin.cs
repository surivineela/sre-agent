// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Services;
using Agent.Logging;
using Agent.Plugins.Interface;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Agent.Plugins;

public class WebAppPlugin : IWebAppPlugin
{
    private readonly ILogger<WebAppPlugin> _logger;
    private readonly ObserverClientService _observerClient;

    public WebAppPlugin(
        ILogger<WebAppPlugin> logger,
        ObserverClientService observerClient)
    {
        _logger = logger;
        _observerClient = observerClient;
    }

    public async Task<string> GetWebAppRebootWorkerDetails(string webappName, string stampName)
    {
        var logMessage = $"[get_webapp_reboot_worker_details] Invoked with webappName {webappName} and stampName {stampName}";
        _logger.LogInternalInformation(logMessage);

        if (!_observerClient.IsEnabled)
        {
            return $"Cannot fetch the details of the webapp {webappName} as Observer API is not enabled.";
        }

        try
        {
            var webAppDetails = await _observerClient.GetSite(stampName, webappName);
            if (webAppDetails == null || (webAppDetails.StatusCode != System.Net.HttpStatusCode.OK))
            {
                return $"Web app {webappName} not found.";
            }
            else
            {
                try
                {
                    var webAppDetailsContent = JsonConvert.DeserializeObject<List<dynamic>>(JsonConvert.SerializeObject(webAppDetails.Content));
                    var webApp = webAppDetailsContent?.FirstOrDefault();
                    if (webApp == null)
                    {
                        return $"Web app {webappName} not found.";
                    }

                    // Try to extract worker reboot link from the response
                    var webWorkers = webApp?.web_workers as IEnumerable<dynamic>;
                    var workerRebootLink = webWorkers?.FirstOrDefault()?.reboot_link?.ToString();
                    
                    if (!string.IsNullOrEmpty(workerRebootLink))
                    {
                        return workerRebootLink;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInternalWarning($"Failed to deserialize web app details content. Error: {ex.Message}.");
                }

                // If structured extraction fails, return the raw content for manual extraction
                return JsonConvert.SerializeObject(webAppDetails.Content);
            }
        }
        catch (Exception ex)
        {
            return $"Failed to fetch the details for the web app {webappName}. Error: {ex.Message}";
        }
    }

    public async Task<string> GetWebAppDetailsByName(string webappName)
    {
        var logMessage = $"[get_webapp_details_by_name] Invoked with webappName {webappName}";
        _logger.LogInternalInformation(logMessage);

        if (!_observerClient.IsEnabled)
        {
            return $"Cannot fetch the details of the webapp {webappName} as Observer API is not enabled.";
        }

        try
        {
            var webApp = await _observerClient.GetSite(webappName);
            if (webApp == null || (webApp.StatusCode != System.Net.HttpStatusCode.OK))
            {
                return $"Web app {webappName} not found.";
            }

            var result = JsonConvert.SerializeObject(webApp.Content);
            return result;
        }
        catch (Exception ex)
        {
            return $"Failed to fetch the details for the web app {webappName}. Error: {ex.Message}";
        }
    }

    public async Task<string> GetWebAppHostnames(string webappName, string stampName)
    {
        var logMessage = $"[get_webapp_hostnames] Invoked with webappName {webappName} and stampName {stampName}";
        _logger.LogInternalInformation(logMessage);

        if (!_observerClient.IsEnabled)
        {
            return $"Cannot fetch the details of the webapp {webappName} as Observer API is not enabled.";
        }

        try
        {
            var webAppDetails = await _observerClient.GetSite(stampName, webappName);
            if (webAppDetails == null || (webAppDetails.StatusCode != System.Net.HttpStatusCode.OK))
            {
                return $"Web app {webappName} not found.";
            }
            else
            {
                try
                {
                    List<dynamic> webAppDetailsContent = JsonConvert.DeserializeObject<List<dynamic>>(JsonConvert.SerializeObject(webAppDetails.Content));
                    var webApp = webAppDetailsContent?.FirstOrDefault();
                    if (webApp == null)
                    {
                        return $"Web app {webappName} not found.";
                    }

                    // Extract hostnames excluding .scm. domains
                    var hostnames = webApp?.hostnames as IEnumerable<dynamic>;
                    var filteredHostnames = hostnames?
                        .Where(hostname => !hostname?.hostname?.ToString()?.Contains(".scm.") == true)
                        .Select(x => x?.hostname?.ToString())
                        .Where(h => !string.IsNullOrEmpty(h))
                        .ToList();

                    if (filteredHostnames?.Any() == true)
                    {
                        return JsonConvert.SerializeObject(filteredHostnames);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInternalWarning($"Failed to deserialize web app details content. Error: {ex.Message}.");
                }

                // If structured extraction fails, return the raw content for manual extraction
                return JsonConvert.SerializeObject(webAppDetails.Content);
            }
        }
        catch (Exception ex)
        {
            return $"Failed to fetch the hostnames for the web app {webappName}. Error: {ex.Message}";
        }
    }
}
