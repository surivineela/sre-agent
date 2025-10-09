// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Configuration;

public class ExtendedAgentsGraphSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether the Extended Agents Graph view is enabled.
    /// When enabled, users can visualize extended agents, tools, and connectors in a graph format.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
