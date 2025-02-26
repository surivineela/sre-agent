using Agent.Core.Helpers;
using FirstPartyAgent.Core.Services;
using FirstPartyAgent.Helpers;
using FirstPartyAgent.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FirstPartyAgent.Plugins
{
    public class ICMPlugin
    {
        private readonly IICMAPIClient _icmApiClient;
        private readonly ICMWorkflowClient _icmWorkflowClient;

        public ICMPlugin(IConfiguration configuration, IICMAPIClient icmAPIClient, ICMWorkflowClient icmWorkflowClient)
        {
            _icmApiClient = icmAPIClient;
            _icmWorkflowClient = icmWorkflowClient;
        }

        /// <summary>
        /// Extracts text from an image using the chat completion service with SK (does not carry around the conversation history/context etc.; no tool calling)
        /// </summary>
        /// <param name="kernel"></param>
        /// <param name="mimeType"></param>
        /// <param name="base64Image"></param>
        /// <param name="chatCompletionService"></param>
        /// <returns></returns>
        private static async Task<ChatMessageContent> ExtractTextFromImage(Kernel kernel, string mimeType, string base64Image, IChatCompletionService chatCompletionService)
        {
            Console.WriteLine($"Extracting text from image ({mimeType}, {base64Image.Length} characters)");

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

        private async Task<string> ProcessComplexICMContent(string complexContent, Kernel kernel)
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
                    // extract text from the image and replace the placeholder in the summary with the extracted text
                    ChatMessageContent result = await ExtractTextFromImage(kernel, base64Images[i].Item1, base64Images[i].Item2, chatCompletionService);

                    complexContent = complexContent.Replace($"####{i}####", "[The following text was in an image in the incident]" + result.Content + "\r\n[End of the image]");
                }
            }
            return complexContent;
        }

        [KernelFunction("get_icm_incident_details")]
        [Description("Get ICM incident details")]
        public async Task<Incident> GetIncidentInfo(
           [Description("Incident ID")] string incidentId, Kernel kernel)
        {
            var incident = _icmApiClient.IsEnabled()? await _icmApiClient.GetIncidentAsync(incidentId): await _icmWorkflowClient.GetIncidentAsync(incidentId);
            incident.Summary = await ProcessComplexICMContent(incident.Summary, kernel);
            return incident;
        }

        [KernelFunction("get_icm_discussion_entries")]
        [Description("Get ICM discussion entries")]
        public async Task<List<DiscussionEntry>> GetDiscussionEntries(
            [Description("Incident ID")] string incidentId, Kernel kernel)
        {
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

        [KernelFunction("get_applens_diagnostics_for_icm_incident")]
        [Description("Get AppLens diagnostics for ICM incident")]
        public async Task<string> GetAppLensDiagnostics(
           [Description("Incident ID")] string incidentId)
        {
            return await _icmWorkflowClient.GetAppLensDiagnosticsAsync(incidentId);
        }

        [KernelFunction("transfer_icm_incident")]
        [Description("Transfer ICM incident")]
        public async Task<string> TransferIncident(
               [Description("Incident ID")] string incidentId,
               [Description("Discussion Entry - reason for transferring the incident")] string discussionEntry,
               [Description("Tenant of the team to transfer the incident to")] string tenantName,
               [Description("Owning Team to transfer the incident to")] string owningTeam)
        {
            return await _icmWorkflowClient.TransferIncidentAsync(incidentId, discussionEntry, tenantName, owningTeam);
        }

        [KernelFunction("mitigate_icm_incident")]
        [Description("Mitigate ICM incident")]
        public async Task<string> MitigateIncident(
           [Description("Incident ID")] string incidentId,
           [Description("Discussion Entry - reason for mitigating the incident")] string discussionEntry)
        {
            return await _icmWorkflowClient.MitigateIncidentAsync(incidentId, discussionEntry);
        }

        [KernelFunction("downgrade_sev2_incident_to_sev3")]
        [Description("Downgrade severity of ICM incident 2 to 3")]
        public async Task<string> DowngradeSeverity(
            [Description("Incident ID")] string incidentId,
            [Description("Discussion Entry - reason for downgrading the incident")] string discussionEntry)
        {
            return await _icmWorkflowClient.DowngradeSeverityAsync(incidentId, discussionEntry);
        }

        [KernelFunction("transfer_icm_incident_to_human_intervention")]
        [Description("Transfer ICM incident to human intervention")]
        public async Task<string> TransferIncidentToHumanIntervention(
            [Description("Incident ID")] string incidentId,
            [Description("Discussion Entry - reason for transferring the incident to human intervention")] string discussionEntry)
        {
            return await _icmWorkflowClient.TransferIncidentToHumanInterventionAsync(incidentId, discussionEntry);
        }

        [KernelFunction("resolve_icm_incident")]
        [Description("Resolve ICM incident")]
        public async Task<string> ResolveIncident(
               [Description("Incident ID")] string incidentId,
               [Description("Discussion Entry - reason for resolving the incident")] string discussionEntry)
        {
            return await _icmWorkflowClient.ResolveIncidentAsync(incidentId, discussionEntry);
        }

        [KernelFunction("post_icm_discussion_entry")]
        [Description("Post ICM discussion entry")]
        public async Task<string> PostDiscussionEntry(
           [Description("Incident ID")] string incidentId,
           [Description("Discussion Entry")] string discussionEntry)
        {
            return await _icmWorkflowClient.PostDiscussionEntryAsync(incidentId, discussionEntry);
        }

        [KernelFunction("mark_subscription_as_first_party")]
        [Description("Mark Subscription as first party")]
        public async Task<string> MarkSubFirstParty(
           [Description("Subscription ID")] string subscriptionId)
        {
            return await _icmWorkflowClient.MarkSubFirstPartyAsync(subscriptionId);
        }

        [KernelFunction("get_subscription_details_from_geneva")]
        [Description("Get subscription details from geneva")]
        public async Task<string> GetSubDetailsFromGeneva(
           [Description("Subscription ID")] string subscriptionId)
        {
            return await _icmWorkflowClient.GetSubDetailsFromGenevaAsync(subscriptionId);
        }

        [KernelFunction("icm_add_tag")]
        [Description("Add a tag to an ICM incident")]
        public async Task<string> AddTagToIncident(
            [Description("Id of the incident")] string incidentId,
            [Description("Tag to add")] string tag)
        {
            return await _icmWorkflowClient.AddTagToIncident(incidentId, tag);
        }

        [KernelFunction("get_icm_incidents_by_team")]
        [Description("Gets a list of ICM incidents by Tenant and Team")]
        public async Task<List<Incident>> GetIncidents(
        [Description("The name of the tenant")] string tenant,
        [Description("Comma-separated list of metrics to include")] string metrics)
        {
            return new List<Incident>();
        }
    }
}
