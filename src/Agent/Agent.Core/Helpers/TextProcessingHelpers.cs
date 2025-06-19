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
    static internal class TextProcessingHelpers
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

        public static async Task<string> ProcessComplexICMContent(string complexContent, Kernel kernel, ILogger logger, bool skipImages = false)
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
                            ChatMessageContent result = await ExtractTextFromImage(kernel, base64Images[i].Item1, base64Images[i].Item2, chatCompletionService, logger);
                            imageText = result.Content;
                        }
                        catch (Exception ex)
                        {
                            logger.LogInternalError(ex, $"Error extracting text from image {base64Images[i].Item1} - {base64Images[i].Item2}");
                        }
                    }

                    complexContent = complexContent.Replace($"####{i}####", "[The following text was in an image in the incident]" + imageText + "\r\n[End of the image]");
                }
            }
            return complexContent;
        }

        public static JObject FillICMAPIIncidentJObject(JObject obj)
        {
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
            if (obj["Slice"] == null && obj["IncidentLocation"]?["ServiceInstanceId"] != null)
            {
                obj["Slice"] = obj["IncidentLocation"]["ServiceInstanceId"];
            }

            // 4. Environment: fill from IncidentLocation.Environment  
            if (obj["Environment"] == null && obj["IncidentLocation"]?["Environment"] != null)
            {
                obj["Environment"] = obj["IncidentLocation"]["Environment"];
            }

            // 5. CreatedBy: fill from Source.CreatedBy  
            if (obj["CreatedBy"] == null && obj["Source"]?["CreatedBy"] != null)
            {
                obj["CreatedBy"] = obj["Source"]["CreatedBy"];
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
            if (obj["OwningServiceId"] == null && obj["ImpactedServicesIds"] is JArray arr && arr.Count > 0)
            {
                obj["OwningServiceId"] = arr[0];
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
            if (obj["MonitoringRole"] == null && obj["RaisingLocation"]?["DeviceGroup"] != null)
            {
                obj["MonitoringRole"] = obj["RaisingLocation"]["DeviceGroup"];
            }

            // 14. MonitoringSlice: map from "RaisingLocation.ServiceInstanceId"  
            if (obj["MonitoringSlice"] == null && obj["RaisingLocation"]?["ServiceInstanceId"] != null)
            {
                obj["MonitoringSlice"] = obj["RaisingLocation"]["ServiceInstanceId"];
            }

            // 15. Optionally convert Severity to a string in case it appears as a number  
            if (obj["Severity"] != null && obj["Severity"].Type != JTokenType.String)
            {
                obj["Severity"] = obj["Severity"].ToString();
            }

            if (obj["IncidentType"] == null && obj["Type"] != null){
                obj["IncidentType"] = obj["Type"];
            }

            // Additional mappings can be added here if needed.  
            return obj;
        }
    }
}

