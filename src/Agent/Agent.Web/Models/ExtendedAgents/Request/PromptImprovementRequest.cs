// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace Agent.Web.Models.ExtendedAgents.Request;

public class PromptImprovementRequest
{
    [Required]
    public string Prompt { get; set; } = string.Empty;
}
