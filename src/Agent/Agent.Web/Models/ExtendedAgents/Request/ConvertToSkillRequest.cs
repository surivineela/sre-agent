// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace Agent.Web.Models.ExtendedAgents.Request;

public class ConvertToSkillRequest
{
    [Required]
    public List<string> TopLevelAgents { get; set; } = [];
}
