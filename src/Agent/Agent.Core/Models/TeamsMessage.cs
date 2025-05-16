// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models;

public class TeamsMessage
{
    public string User { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }
    public string? Image { get; set; }
    public string? MessageId { get; set; }

    public TeamsMessage(string content, string? image = null)
    {
        this.Content = content;
        this.Image = image ?? string.Empty;
    }
}

