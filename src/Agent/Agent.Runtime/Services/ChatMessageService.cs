// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;

namespace Agent.Runtime.Services;
public static class ChatMessageService
{
    /// <summary>
    /// Helper method  to serialize Investigation Summary message in the front-end format.
    /// format: ```investigation-summary {title: "", content: ""}```
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
        var investigationSummaryBlock = $"```investigation-summary\n{serializedSummary}\n```\n";

        return investigationSummaryBlock;
    }
}
