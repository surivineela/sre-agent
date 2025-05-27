// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.SubAgents.Core;

public class WriteActionActivityOutput
{
    public FunctionCallContent? ModifiedFunctionCall { get; set; }

    public bool IsWriteAction { get; set; }

    public string Prompt { get; set; } = string.Empty;

    public bool NeedSkip { get; set; } = false;
}
