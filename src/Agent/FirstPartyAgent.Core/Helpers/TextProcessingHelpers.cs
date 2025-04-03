// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.RegularExpressions;

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
            // Regex pattern to remove attributes but keep tags
            string pattern = "<([a-zA-Z0-9]+)(\\s+[^>]+)?>";

            return Regex.Replace(html, pattern, "<$1>");
        }

        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }
    }
}

