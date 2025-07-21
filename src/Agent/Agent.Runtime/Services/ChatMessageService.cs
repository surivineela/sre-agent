// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agent.Runtime.Services;
public static class ChatMessageService
{
    private const string startMarker = "<investigation-summaries>";
    private const string endMarker = "</investigation-summaries>";

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
            isCollapsed,
            status = "loading"
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
                i.isCollapsed,
                status = "loading"
            })
        };

        var serialized = JsonSerializer.Serialize(payload);

        return $"<investigation-summaries>{serialized}</investigation-summaries>\n";
    }

    public static string AppendInvestigationSummary(
        string message,
        string newTitle,
        string newSummary,
        bool newIsCollapsed = true,
        string status = "loading",
        bool isFinal = false)
    {
        var payload = ExtractInvestigationSummariesFromMessage(message)
                      ?? throw new InvalidOperationException("Could not parse existing summaries");

        // Update status of previous summaries to completed
        foreach (var summary in payload.Summaries)
        {
            summary.Status = "completed";
        }

        payload.Summaries.Add(new SummaryItem
        {
            Title = newTitle,
            Summary = newSummary,
            IsCollapsed = newIsCollapsed,
            Status = status,
            IsFinal = isFinal
        });

        // re-serialize and wrap it again with the new format
        return SerializeInvestigationSummaries(payload);
    }

    //public static string AppendFinalSummaryToInvestigation(
    //    string message,
    //    string finalSummary)
    //{
    //    var summaries = ExtractInvestigationSummariesFromMessage(message)
    //        ?? throw new InvalidOperationException("could not parse existing summaries");

    //    // update last summary to mark it as complete and final
    //    var lastSummary = summaries.Summaries.Last();

    //    if (lastSummary != null)
    //    {
    //        lastSummary.IsFinal = true;
    //        lastSummary.Status = "completed";
    //    }

    //    var newMessage = $"{SerializeInvestigationSummaries(summaries)}{Environment.NewLine}{Environment.NewLine}<final-summary>{finalSummary}</final-summary>";

    //    return newMessage;
    //}

    private static InvestigationSummaries? ExtractInvestigationSummariesFromMessage(string message)
    {
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

        return payload;
    }

    private static string SerializeInvestigationSummaries(InvestigationSummaries investigationSummaries)
    {
        string updatedJson = JsonSerializer.Serialize(investigationSummaries);
        return $"{startMarker}{updatedJson}{endMarker}\n";
    }
}

public class InvestigationSummaries
{
    [JsonPropertyName("containerTitle")]
    public string ContainerTitle { get; set; } = string.Empty;

    [JsonPropertyName("summaries")]
    public List<SummaryItem> Summaries { get; set; } = new List<SummaryItem>();
}

public class SummaryItem
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("isCollapsed")]
    public bool IsCollapsed { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "loading";

    [JsonPropertyName("isFinal")]
    public bool IsFinal { get; set; }
}
