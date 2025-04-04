// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using FirstPartyAgent.Core.Constants;
using FirstPartyAgent.Core.Extensions;
using FirstPartyAgent.Core.Services;
using FirstPartyAgent.Helpers;
using FirstPartyAgent.Models;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.ComponentModel;

namespace FirstPartyAgent.Core.Plugins
{
    public class ICMPlugin
    {
        private readonly IICMAPIClient _icmApiClient;
        private readonly ICMWorkflowClient _icmWorkflowClient;
        private readonly ILogger<ICMPlugin> _logger;
        private readonly ITeamsClient _teamsClient;

        public ICMPlugin(IICMAPIClient icmAPIClient, ICMWorkflowClient icmWorkflowClient, ILogger<ICMPlugin> logger, ITeamsClient teamsClient)
        {
            _logger = logger;
            _icmApiClient = icmAPIClient;
            _icmWorkflowClient = icmWorkflowClient;
            _teamsClient = teamsClient;
        }

        /// <summary>
        /// Extracts text from an image using the chat completion service with SK (does not carry around the conversation history/context etc.; no tool calling)
        /// </summary>
        /// <param name="kernel"></param>
        /// <param name="mimeType"></param>
        /// <param name="base64Image"></param>
        /// <param name="chatCompletionService"></param>
        /// <returns></returns>
        private static async Task<ChatMessageContent> ExtractTextFromImage(Kernel kernel, string mimeType, string base64Image, IChatCompletionService chatCompletionService, ILogger<ICMPlugin> logger)
        {
            logger.LogInformation($"Extracting text from image ({mimeType}, {base64Image.Length} characters)");

            var history = new ChatHistory();
            var message = new ChatMessageContentItemCollection
                        {
                            new TextContent("Please extract the text from the image"),
                            new ImageContent($"{mimeType};base64,{base64Image}")
                        };

            history.AddUserMessage(message);

            var result = await chatCompletionService.GetChatMessageContentAsync(
            history,
            executionSettings: new()
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.None()
            },
            kernel: kernel);

            return result;
        }

        private async Task<string> ProcessComplexICMContent(string complexContent, Kernel kernel, bool skipImages = false)
        {
            List<(string, string)> base64Images = new List<(string, string)>();
            if (complexContent != null)
            {
                // remove base64 images from the complexContent (they would blow the response which goes back to the model and the model wouldn't make sense out of it) and store them in a list
                complexContent = TextProcessingHelpers.StripBase64Images(complexContent, base64Images);

                // remove html attributes as they don't provide much value and make the response longer (todo: it might be useful to strip html tags completely and convert the text rather to markdown or something like that)
                complexContent = TextProcessingHelpers.RemoveHtmlAttributes(complexContent);

                var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

                for (int i = 0; i < base64Images.Count; i++)
                {
                    string imageText = "No Image Description.";
                    if (!chatCompletionService.Attributes["DeploymentName"].ToString().StartsWith("o") && !skipImages)
                    {
                        // extract text from the image and replace the placeholder in the summary with the extracted text
                        try
                        {
                            ChatMessageContent result = await ExtractTextFromImage(kernel, base64Images[i].Item1, base64Images[i].Item2, chatCompletionService, _logger);
                            imageText = result.Content;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Error extracting text from image {base64Images[i].Item1} - {base64Images[i].Item2}");
                        }
                    }
                    
                    complexContent = complexContent.Replace($"####{i}####", "[The following text was in an image in the incident]" + imageText + "\r\n[End of the image]");
                }
            }
            return complexContent;
        }

        [KernelFunction("get_icm_incident_details")]
        [Description("Get ICM incident details")]
        public async Task<Incident> GetIncidentInfo(
           [Description("Incident ID")] string incidentId, Kernel kernel)
        {
            var logMessage = $"[get_icm_incident_details][{DateTime.UtcNow}] Invoked with incidentId {incidentId}";
            await kernel.LogInformation(logMessage, _logger, _teamsClient);
            var incident = _icmApiClient.IsEnabled()? await _icmApiClient.GetIncidentAsync(incidentId): await _icmWorkflowClient.GetIncidentAsync(incidentId);
            incident.Summary = await ProcessComplexICMContent(incident.Summary, kernel);
            return incident;
        }

        [KernelFunction("get_alerting_discussion_entry")]
        [Description("Get Azure Alerting discussion entry")]
        public async Task<DiscussionEntry> GetAlertingDiscussionEntry(
            [Description("Incident ID")] string incidentId,
            Kernel kernel)
        {
            var logMessage = $"[get_alerting_discussion_entry][{DateTime.UtcNow}] Invoked with incidentId {incidentId}";
            await kernel.LogInformation(logMessage, _logger, _teamsClient);
            var discussionEntries = _icmApiClient.IsEnabled() ? await _icmApiClient.GetIncidentDiscussionEntriesAsync(incidentId) : await _icmWorkflowClient.GetIncidentDiscussionEntriesAsync(incidentId);
            if (discussionEntries != null)
            {
                foreach (var entry in discussionEntries)
                {
                    if (entry.IsHtml)
                    {
                        entry.Text = await ProcessComplexICMContent(entry.Text, kernel, skipImages: true);
                    }
                    if (entry.Text.Contains("Open in AzureAlerting"))
                    {
                        return entry;
                    }
                }
            }
            return null;
        }

        [KernelFunction("get_icm_discussion_entries")]
        [Description("Get ICM discussion entries")]
        public async Task<List<DiscussionEntry>> GetDiscussionEntries(
            [Description("Incident ID")] string incidentId, Kernel kernel)
        {
            var logMessage = $"[get_icm_discussion_entries][{DateTime.UtcNow}] Fetching ICM Discussion entries for Incident {incidentId}";
            await kernel.LogInformation(logMessage, _logger, _teamsClient);
            var discussionEntries = _icmApiClient.IsEnabled()? await _icmApiClient.GetIncidentDiscussionEntriesAsync(incidentId): await _icmWorkflowClient.GetIncidentDiscussionEntriesAsync(incidentId);
            if (discussionEntries != null)
            {
                foreach (var entry in discussionEntries)
                {
                    if (entry.IsHtml)
                    {
                        entry.Text = await ProcessComplexICMContent(entry.Text, kernel);
                    }
                }
            }
            return discussionEntries;
        }

        [KernelFunction("transfer_icm_incident")]
        [Description("Transfer ICM incident")]
        public async Task<string> TransferIncident(
               [Description("Incident ID")] string incidentId,
               [Description("Discussion Entry - reason for transferring the incident")] string discussionEntry,
               [Description("Tenant ID of the team to transfer the incident to")] string tenantName,
               [Description("Team ID of the team to transfer the incident to")] string owningTeam,
               Kernel kernel)
        {
            var logMessage = $"[transfer_icm_incident][{DateTime.UtcNow}] Transferring Incident {incidentId} to the team {tenantName}/{owningTeam}.\n<b>Reason</b>:\n {discussionEntry}";
            await kernel.LogInformation(logMessage, _logger, _teamsClient);
            discussionEntry = IcmPostTemplates.DiscussionEntryTemplate.Replace("POST_CONTENT_HERE", discussionEntry);
            return _icmApiClient.IsEnabled() ? await _icmApiClient.TransferIncidentAsync(incidentId, discussionEntry, tenantName, owningTeam) : await _icmWorkflowClient.TransferIncidentAsync(incidentId, discussionEntry, tenantName, owningTeam);
        }

        [KernelFunction("mitigate_icm_incident")]
        [Description("Mitigate ICM incident")]
        public async Task<string> MitigateIncident(
           [Description("Incident ID")] string incidentId,
           [Description("Discussion Entry (HTML) - reason for mitigating the incident")] string discussionEntry,
           Kernel kernel)
        {
            var logMessage = $"[mitigate_icm_incident][{DateTime.UtcNow}] Invoked with incidentId {incidentId}.\n<b>discussionEntry</b>:\n {discussionEntry}";
            await kernel.LogInformation(logMessage, _logger, _teamsClient);
            discussionEntry = IcmPostTemplates.DiscussionEntryTemplate.Replace("POST_CONTENT_HERE", discussionEntry);

            var mitigationResult = _icmApiClient.IsEnabled() ? await _icmApiClient.MitigateIncidentAsync(incidentId, discussionEntry) : await _icmWorkflowClient.MitigateIncidentAsync(incidentId, discussionEntry);
            var addMitigationTagResult = await _icmWorkflowClient.AddTagToIncident(incidentId, "SREAgent_Mitigated");
            return mitigationResult;
        }

        [KernelFunction("downgrade_sev2_incident_to_sev3")]
        [Description("Downgrade severity of ICM incident 2 to 3")]
        public async Task<string> DowngradeSeverity(
            [Description("Incident ID")] string incidentId,
            [Description("Discussion Entry (HTML) - reason for downgrading the incident")] string discussionEntry,
            Kernel kernel)
        {
            var logMessage = $"[downgrade_sev2_incident_to_sev3][{DateTime.UtcNow}] Invoked with incidentId {incidentId}.\n<b>discussionEntry</b>:\n {discussionEntry}";
            await kernel.LogInformation(logMessage, _logger, _teamsClient);
            discussionEntry = IcmPostTemplates.DiscussionEntryTemplate.Replace("POST_CONTENT_HERE", discussionEntry);
            return _icmApiClient.IsEnabled() ? await _icmApiClient.ChangeSeverityAsync(incidentId, 3, discussionEntry) : await _icmWorkflowClient.DowngradeSeverityAsync(incidentId, discussionEntry);
        }

        [KernelFunction("resolve_icm_incident")]
        [Description("Resolve ICM incident")]
        public async Task<string> ResolveIncident(
               [Description("Incident ID")] string incidentId,
               [Description("Discussion Entry (HTML) - reason for resolving the incident")] string discussionEntry,
               Kernel kernel)
        {
            var logMessage = $"[resolve_icm_incident][{DateTime.UtcNow}] Invoked with incidentId {incidentId}.\n<b>discussionEntry</b>:\n {discussionEntry}";
            await kernel.LogInformation(logMessage, _logger, _teamsClient);
            discussionEntry = IcmPostTemplates.DiscussionEntryTemplate.Replace("POST_CONTENT_HERE", discussionEntry);
            return _icmApiClient.IsEnabled() ? await _icmApiClient.ResolveIncidentAsync(incidentId, discussionEntry) : await _icmWorkflowClient.ResolveIncidentAsync(incidentId, discussionEntry);
        }

        [KernelFunction("post_icm_discussion_entry")]
        [Description("Post ICM discussion entry")]
        public async Task<string> PostDiscussionEntry(
           [Description("Incident ID")] string incidentId,
           [Description("Discussion Entry (HTML)")] string discussionEntry, Kernel kernel)
        {
            var logMessage = $"[post_icm_discussion_entry][{DateTime.UtcNow}] Invoked with incidentId {incidentId}.\n<b>discussionEntry</b>:\n {discussionEntry}";
            await kernel.LogInformation(logMessage, _logger, _teamsClient);
            discussionEntry = IcmPostTemplates.DiscussionEntryTemplate.Replace("POST_CONTENT_HERE", discussionEntry);
            return _icmApiClient.IsEnabled() ? await _icmApiClient.PostDiscussionEntryAsync(incidentId, discussionEntry) : await _icmWorkflowClient.PostDiscussionEntryAsync(incidentId, discussionEntry);
        }

        [KernelFunction("icm_add_tag")]
        [Description("Add a tag to an ICM incident")]
        public async Task<string> AddTagToIncident(
            [Description("Id of the incident")] string incidentId,
            [Description("Tag to add")] string tag,
            Kernel kernel)
        {
            var logMessage = $"[icm_add_tag][{DateTime.UtcNow}] Invoked with incidentId {incidentId}, tag: {tag}";
            await kernel.LogInformation(logMessage, _logger, _teamsClient);
            return _icmApiClient.IsEnabled() ? await _icmApiClient.AddTagToIncident(incidentId, tag) : await _icmWorkflowClient.AddTagToIncident(incidentId, tag);
        }


        // acknowledge_icm_incident using ICM API
        [KernelFunction("acknowledge_icm_incident")]
        [Description("Acknowledges an ICM incident")]
        public async Task<string> AcknowledgeIncident(
            [Description("Incident ID")] string incidentId,
            Kernel kernel)
        {
            var logMessage = $"[acknowledge_icm_incident][{DateTime.UtcNow}] Invoked with incidentId {incidentId}";
            await kernel.LogInformation(logMessage, _logger, _teamsClient);
            if (!_icmApiClient.IsEnabled())
            {
                throw new InvalidOperationException("ICM API client is not enabled.");
            }
            return await _icmApiClient.AcknowledgeIncidentAsync(incidentId);
        }
    }
}

