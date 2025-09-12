// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.RegularExpressions;
using Agent.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Newtonsoft.Json.Linq;

namespace Agent.Core.Helpers
{
    static public class TextProcessingHelpers
    {
        /// <summary>
        /// Strips base64 images from the HTML content and adds them to the base64Images list
        /// </summary>
        /// <param name="html"></param>
        /// <param name="base64Images"></param>
        /// <returns></returns>
        public static string StripBase64Images(string html, List<(string, string)> base64Images)
        {
            string pattern = @"<img\s+[^>]*src\s*=\s*""(?<mimeType>data:image\/(?<format>png|jpeg|jpg|gif|bmp));base64,(?<base64>[A-Za-z0-9+\/=]+)""[^>]*>";
            int imageCounter = 1;

            return Regex.Replace(html, pattern, match =>
            {
                string mimeType = match.Groups["mimeType"].Value;
                string base64Data = match.Groups["base64"].Value;
                base64Images.Add((mimeType, base64Data));

                return $"####{imageCounter}####";
            }, RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Removes HTML attributes from the string, making it shorter, but keeping still the structure
        /// </summary>
        /// <param name="html"></param>
        /// <returns></returns>
        public static string RemoveHtmlAttributes(string html)
        {
            // Pattern to match <a> tags and preserve href attribute
            string anchorPattern = @"<a\s+[^>]*href\s*=\s*(['""])(?<href>.*?)\1[^>]*>";
            // Replace <a ... href="..."> with <a href="...">
            html = Regex.Replace(html, anchorPattern, m => $"<a href=\"{m.Groups["href"].Value}\">", RegexOptions.IgnoreCase);

            // Pattern to match all other tags and remove their attributes
            string otherTagsPattern = @"<([a-zA-Z0-9]+)(\s+[^>]+)?>";
            html = Regex.Replace(html, otherTagsPattern, "<$1>", RegexOptions.IgnoreCase);

            return html;
        }

        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }

        /// <summary>
        /// Extracts text from an image using the chat completion service with SK (does not carry around the conversation history/context etc.; no tool calling)
        /// </summary>
        /// <param name="kernel"></param>
        /// <param name="mimeType"></param>
        /// <param name="base64Image"></param>
        /// <param name="chatCompletionService"></param>
        /// <returns></returns>
        public static async Task<ChatMessageContent> ExtractTextFromImage(Kernel kernel, string mimeType, string base64Image, IChatCompletionService chatCompletionService, ILogger logger)
        {
            logger.LogInternalInformation($"Extracting text from image ({mimeType}, {base64Image.Length} characters)");

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

        public static JObject FillICMAPIIncidentJObject(JObject obj)
        {
            // Guard against null input
            if (obj == null)
                return new JObject();

            // 1. Map IncidentId from "Id"
            if (obj["IncidentId"] == null && obj["Id"] != null)
            {
                obj["IncidentId"] = obj["Id"];
            }

            if (obj["Status"] == null && obj["State"] != null)
            {
                obj["Status"] = obj["State"];
            }

            // 2. CloudInstance: not provided in source json, so default to "Public"  
            if (obj["CloudInstance"] == null && obj["CloudName"] != null)
            {
                obj["CloudInstance"] = obj["CloudName"];
            }

            // 3. Slice: fill using IncidentLocation.ServiceInstanceId  
            var incidentLocation = obj["IncidentLocation"] as JObject;
            if (obj["Slice"] == null && incidentLocation?["ServiceInstanceId"] != null)
            {
                obj["Slice"] = incidentLocation["ServiceInstanceId"];
            }

            // 4. Environment: fill from IncidentLocation.Environment  
            if (obj["Environment"] == null && incidentLocation?["Environment"] != null)
            {
                obj["Environment"] = incidentLocation["Environment"];
            }

            // 5. CreatedBy: fill from Source.CreatedBy  
            var source = obj["Source"] as JObject;
            if (obj["CreatedBy"] == null && source?["CreatedBy"] != null)
            {
                obj["CreatedBy"] = source["CreatedBy"];
            }

            // 6. CreatedDate: model expects CreatedDate but source has CreateDate  
            if (obj["CreatedDate"] == null && obj["CreateDate"] != null)
            {
                obj["CreatedDate"] = obj["CreateDate"];
            }

            if (obj["MitigatedDate"] == null && obj["MitigateTime"] != null)
            {
                obj["MitigatedDate"] = obj["MitigateTime"];
            }

            // 7. OwningService: not provided. Default to empty string.  
            if (obj["OwningService"] == null)
            {
                obj["OwningService"] = string.Empty;
            }

            // 8. OwningServiceId: derive the first element from ImpactedServicesIds array.  
            var impactedServicesIds = obj["ImpactedServicesIds"] as JArray;
            if (obj["OwningServiceId"] == null && impactedServicesIds != null && impactedServicesIds.Count > 0)
            {
                obj["OwningServiceId"] = impactedServicesIds[0];
            }

            // 9. OwningTeam: map from "OwningTeamId"  
            if (obj["OwningTeam"] == null && obj["OwningTeamId"] != null)
            {
                obj["OwningTeam"] = obj["OwningTeamId"];
            }

            // 10. OwningTeamName: not provided – default to empty  
            if (obj["OwningTeamName"] == null)
            {
                obj["OwningTeamName"] = string.Empty;
            }

            // 11. Owner: not provided – default to empty  
            if (obj["Owner"] == null)
            {
                obj["Owner"] = string.Empty;
            }

            // 12. DiscussionEntry: map from "NewDescriptionEntry"  
            if (obj["DiscussionEntry"] == null && obj["NewDescriptionEntry"] != null)
            {
                obj["DiscussionEntry"] = obj["NewDescriptionEntry"];
            }

            // 13. MonitoringRole: map from "RaisingLocation.DeviceGroup"  
            var raisingLocation = obj["RaisingLocation"] as JObject;
            if (obj["MonitoringRole"] == null && raisingLocation?["DeviceGroup"] != null)
            {
                obj["MonitoringRole"] = raisingLocation["DeviceGroup"];
            }

            // 14. MonitoringSlice: map from "RaisingLocation.ServiceInstanceId"  
            if (obj["MonitoringSlice"] == null && raisingLocation?["ServiceInstanceId"] != null)
            {
                obj["MonitoringSlice"] = raisingLocation["ServiceInstanceId"];
            }

            // 15. Optionally convert Severity to a string in case it appears as a number  
            var severityToken = obj["Severity"];
            if (severityToken != null && severityToken.Type != JTokenType.String)
            {
                obj["Severity"] = severityToken.ToString();
            }

            if (obj["IncidentType"] == null && obj["Type"] != null)
            {
                obj["IncidentType"] = obj["Type"];
            }

            var occuringLocation = obj["OccuringLocation"] as JObject;

            if (occuringLocation?["Slice"] != null)
            {
                obj["Stamp"] = occuringLocation["Slice"]?.ToString();
            }

            if (occuringLocation?["Datacenter"] != null)
            {
                obj["Datacenter"] = occuringLocation["Datacenter"]?.ToString();
            }

            // Additional mappings can be added here if needed.  
            return obj;
        }
    }
}

