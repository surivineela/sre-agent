// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace FirstPartyAgent.Helpers
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

        public static JObject FillICMAPIIncidentJObject(JObject obj)
        {
            // 1. Map IncidentId from "Id"
            if (obj["IncidentId"] == null && obj["Id"] != null)
            {
                obj["IncidentId"] = obj["Id"];
            }


            // 2. CloudInstance: not provided in source json, so default to "Public"  
            if (obj["CloudInstance"] == null)
            {
                obj["CloudInstance"] = "Public";
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

            // Additional mappings can be added here if needed.  
            return obj;
        }
    }
}

