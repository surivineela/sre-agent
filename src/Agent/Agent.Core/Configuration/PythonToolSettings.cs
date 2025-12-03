// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Configuration;

public class PythonToolSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether Python tool creation is enabled.
    /// When enabled, users can create custom Python function tools in the extended agents graph.
    /// </summary>
    public bool Enabled { get; set; } = false;
}
