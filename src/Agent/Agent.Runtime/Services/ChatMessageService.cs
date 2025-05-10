// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agent.Runtime.Services;
public static class ChatMessageService
{
    /// <summary>
    /// Helper method  to serialize Investigation Summary message in the front-end format.
    /// format: <investigation-summary>{title: "", content: ""}</investigation-summary>
    /// </summary>
    /// <param name="title">Title of the investigation.</param>
    /// <param name="content">Investigation summary.</param>
    /// <returns></returns>
    public static string SerializeInvestigationSummaryMessage(string title, string summary, bool isCollapsed = true)
    {
        var investigationSummary = new
        {
            title,
            summary,
            isCollapsed
        };

        var serializedSummary = JsonSerializer.Serialize(investigationSummary);
        var investigationSummaryBlock = $"<investigation-summary>{serializedSummary}</investigation-summary>\n";

        return investigationSummaryBlock;
    }

    public static string InitializeInvestigationSummariesMessage(
        string containerTitle,
        IEnumerable<(string title, string summary, bool isCollapsed)> items)
    {
        var payload = new
        {
            containerTitle,
            summaries = items.Select(i => new
            {
                i.title,
                i.summary,
                i.isCollapsed
            })
        };

        var serialized = JsonSerializer.Serialize(payload);

        return $"<investigation-summaries>{serialized}</investigation-summaries>\n";
    }

    public static string AppendInvestigationSummary(
        string message,
        string newTitle,
        string newSummary,
        bool newIsCollapsed = true)
    {
        const string startMarker = "<investigation-summaries>";
        const string endMarker = "</investigation-summaries>";

        if (!message.StartsWith(startMarker))
            throw new ArgumentException("Not an investigation-summaries block", nameof(message));

        // Extract the JSON from between the XML tags
        int jsonStart = startMarker.Length;
        int jsonEnd = message.LastIndexOf(endMarker, StringComparison.Ordinal);
        if (jsonEnd <= jsonStart)
        {
            throw new ArgumentException("Invalid investigation-summaries format", nameof(message));
        }

        string json = message.Substring(jsonStart, jsonEnd - jsonStart).Trim();

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        var payload = JsonSerializer.Deserialize<InvestigationSummaries>(json, options)
                      ?? throw new InvalidOperationException("Could not parse existing summaries");

        payload.Summaries.Add(new SummaryItem
        {
            Title = newTitle,
            Summary = newSummary,
            IsCollapsed = newIsCollapsed
        });

        // re-serialize and wrap it again with the new format
        string updatedJson = JsonSerializer.Serialize(payload);
        return $"{startMarker}{updatedJson}{endMarker}\n";
    }
}


public class InvestigationSummaries
{
    [JsonPropertyName("containerTitle")]
    public string ContainerTitle { get; set; }

    [JsonPropertyName("summaries")]
    public List<SummaryItem> Summaries { get; set; }
}

public class SummaryItem
{
    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("summary")]
    public string Summary { get; set; }

    [JsonPropertyName("isCollapsed")]
    public bool IsCollapsed { get; set; }
}
