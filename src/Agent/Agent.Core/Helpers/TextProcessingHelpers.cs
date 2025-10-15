// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.RegularExpressions;

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
    }
}

