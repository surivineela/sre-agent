// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Attributes;

/// <summary>
/// Attribute to mark methods that perform write operations.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public class WriteActionAttribute : Attribute
{
    public bool RunInReadOnlyMode { get; set; }
    public string? ReadOnlyMessage { get; set; }
    
    public WriteActionAttribute(bool runInReadOnlyMode = false, string? readOnlyMessage = null)
    {
        RunInReadOnlyMode = runInReadOnlyMode;
        ReadOnlyMessage = readOnlyMessage;
    }
}
