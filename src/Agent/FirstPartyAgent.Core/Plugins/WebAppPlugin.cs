// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Services;
using FirstPartyAgent.Core.Extensions;
using FirstPartyAgent.Core.Models.Resources;
using FirstPartyAgent.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Newtonsoft.Json;
using System.ComponentModel;
using ObserverClientService = FirstPartyAgent.Core.Services.ObserverClientService;

namespace FirstPartyAgent.Core.Plugins
{
    public class WebAppPlugin
    {
        private readonly ILogger<WebAppPlugin> _logger;
        private readonly ObserverClientService _observerClient;
        private readonly ITeamsClient _teamsClient;
        private readonly Kernel _kernel;
        public WebAppPlugin(ObserverClientService observerClient, ILogger<WebAppPlugin> logger, ITeamsClient teamsClient, Kernel kernel)
        {
            _logger = logger;
            _observerClient = observerClient;
            _teamsClient = teamsClient;
            _kernel = kernel;
        }

        private async Task<string> ExtractWorkerRebootLink(string observerPayload)
        {
            var history = new ChatHistory();
            var message = new ChatMessageContentItemCollection
                        {
                            new TextContent("Please extract the worker reboot link from the following JSON payload. Focus on web_workers section in JSON for web worker details."),
                            new TextContent(observerPayload)
                        };
            history.AddUserMessage(message);
            var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();
            var result = await chatCompletionService.GetChatMessageContentAsync(
            history,
            executionSettings: new()
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.None()
            });
            return result.Content;
        }

        private async Task<string> ExtractHostnames(string observerPayload)
        {
            var history = new ChatHistory();
            var message = new ChatMessageContentItemCollection
                        {
                            new TextContent("Please extract the web app hostnames and their link from the following JSON payload"),
                            new TextContent(observerPayload)
                        };
            history.AddUserMessage(message);
            var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();
            var result = await chatCompletionService.GetChatMessageContentAsync(
            history,
            executionSettings: new()
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.None()
            });
            return result.Content;
        }

        [KernelFunction("get_webapp_reboot_worker_details")]
        [Description("Takes a web app name and a stamp name and fetches the details to reboot the worker like location, role, roleinstance, etc.")]
        public async Task<string> GetWebAppRebootWorkerDetails([Description("Name of the web app")] string webappName, [Description("Name of the stamp")] string stampName, Kernel kernel)
        {
            var logMessage = $"[get_webapp_reboot_worker_details][{DateTime.UtcNow}] Invoked with webappName {webappName} and stampName {stampName}";
            await kernel.LogInformation(logMessage, _logger, _teamsClient);
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
                    try {
                        List<StampSiteModel> webAppDetailsContent = JsonConvert.DeserializeObject<List<StampSiteModel>>(JsonConvert.SerializeObject(webAppDetails.Content));
                        var webApp = webAppDetailsContent.FirstOrDefault();
                        if (webApp == null)
                        {
                            return $"Web app {webappName} not found.";
                        }
                        var workerRebootLink = webApp.WebWorkers.FirstOrDefault()?.RebootLink;
                        if (workerRebootLink != null)
                        {
                            return workerRebootLink;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Failed to deserialize web app details content. Error: {ex.Message}.");
                    }
                    return await ExtractWorkerRebootLink(JsonConvert.SerializeObject(webAppDetails.Content));
                }                
            }
            catch (Exception ex)
            {
                return $"Failed to fetch the details for the web app {webappName}. Error: {ex.Message}";
            }
        }

        [KernelFunction("get_webapp_details_by_name")]
        [Description("Takes a web app name and fetches the details like subscription id, webspace name, hostnames etc.")]
        public async Task<string> GetWebAppDetailsByName([Description("Name of the web app")] string webappName, Kernel kernel)
        {
            var logMessage = $"[get_webapp_details_by_name][{DateTime.UtcNow}] Invoked with webappName {webappName}";
            await kernel.LogInformation(logMessage, _logger, _teamsClient);
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

        [KernelFunction("get_webapp_hostnames")]
        [Description("Takes a web app name and a stamp name and fetches the hostnames for the web app.")]
        public async Task<string> GetWebAppHostnames([Description("Name of the web app")] string webappName, [Description("Name of the stamp")] string stampName, Kernel kernel)
        {
            var logMessage = $"[get_webapp_hostnames][{DateTime.UtcNow}] Invoked with webappName {webappName}";
            await kernel.LogInformation(logMessage, _logger, _teamsClient);
            if (!_observerClient.IsEnabled)
            {
                return $"Cannot fetch the details of the webapp {webappName} as Observer API is not enabled.";
            }
            var webAppDetails = await _observerClient.GetSite(stampName, webappName);
            if (webAppDetails == null || (webAppDetails.StatusCode != System.Net.HttpStatusCode.OK))
            {
                return $"Web app {webappName} not found.";
            }
            else
            {
                try
                {
                    List<StampSiteModel> webAppDetailsContent = JsonConvert.DeserializeObject<List<StampSiteModel>>(JsonConvert.SerializeObject(webAppDetails.Content));
                    var webApp = webAppDetailsContent.FirstOrDefault();
                    if (webApp == null)
                    {
                        return $"Web app {webappName} not found.";
                    }
                    var result = JsonConvert.SerializeObject(webApp.Hostnames.Where(hostname => !hostname.Hostname.Contains(".scm.")).Select(x => x.Hostname).ToList());
                    return result;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to deserialize web app details content. Error: {ex.Message}.");
                }
                return await ExtractHostnames(JsonConvert.SerializeObject(webAppDetails.Content));
            }
        }
    }
}

