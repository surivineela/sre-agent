// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Plugins.Kusto;

public class KustoFunctionInfo
{
    public string? Name { get; set; }
    public string? Folder { get; set; }
    public string? DocString { get; set; }
    public string? Parameters { get; set; }

    public string ToFunctionSignature() =>
        string.IsNullOrWhiteSpace(Parameters)
        ? $"{Name}()"
        : $"{Name}({Parameters})";
}
