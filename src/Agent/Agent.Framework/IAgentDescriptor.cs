// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Framework;

public interface IAgentDescriptor
{
    public string Name { get; set; }
    public string Instructions { get; set; }
    public string? HandoffDescription { get; set; }
    public List<string> Handoffs { get; set; }
    public List<string> Tools { get; set; }
    public bool AllowParallelToolCalls { get; set; }
    public int MaxReflectionCount { get; set; }
    public string CustomReflectionNote { get; set; }
    public string CriticPromptPath { get; set; }
    public List<string> CommonPrompts { get; set; }
    public float? Temperature { get; set; }
}
