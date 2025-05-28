// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using YamlDotNet.Serialization;

namespace Agent.Framework;

public class YamlPromptDescriptor : IPromptDescriptor
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;


    [YamlMember(Alias = "prompt")]
    public string Prompt { get; set; } = string.Empty;
}


