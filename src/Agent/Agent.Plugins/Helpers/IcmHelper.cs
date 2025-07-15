using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using ReverseMarkdown;

namespace Agent.Plugins.Helpers;
internal class IcmHelper
{
    private static string TruncateImageTags(string? htmlContent)
    {
        if (string.IsNullOrEmpty(htmlContent))
        {
            return "";
        }
        string pattern = @"<img.*?src=""data:image\/png;base64,.*?>";
        string replaced = @"<img src=""path/to/placeholder.png"">";

        return Regex.Replace(htmlContent, pattern, replaced);
    }

    public static string ConvertToMarkDown(string? sanitizedHtmlContent)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(sanitizedHtmlContent ?? string.Empty);
        var converter = new Converter();
        return converter.Convert(doc.DocumentNode.OuterHtml);
    }

    public static string ConvertToOptimizedMarkDown(string? htmlContent)
    {
        var doc = new HtmlDocument();
        var truncated = TruncateImageTags(htmlContent);
        doc.LoadHtml(truncated);
        var converter = new Converter();
        return converter.Convert(doc.DocumentNode.OuterHtml);
    }
}
