// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace Agent.Web.Models.ExtendedAgents;

public class PythonToolApiModel : ExtendedAgentToolApiModel
{
    [YamlMember(Alias = "function_code")]
    public string FunctionCode { get; set; } = string.Empty;

    [YamlMember(Alias = "timeout_seconds")]
    public int TimeoutSeconds { get; set; } = 120;

    [YamlMember(Alias = "dependencies")]
    public List<string> Dependencies { get; set; } = new();
}
