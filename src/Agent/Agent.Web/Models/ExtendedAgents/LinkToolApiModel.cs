// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels;

namespace Agent.Web.Models.ExtendedAgents;

public class LinkToolApiModel : ExtendedAgentToolApiModel
{
    public string Template { get; set; } = string.Empty;
}