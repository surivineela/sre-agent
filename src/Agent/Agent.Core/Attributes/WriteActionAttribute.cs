// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Attributes;

/// <summary>
/// Attribute to mark methods that support dry run.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public class WriteActionAttribute : Attribute
{
    public bool RunInReadOnlyMode { get; set; }
    public WriteActionAttribute(bool runInReadOnlyMode = false)
    {
        RunInReadOnlyMode = runInReadOnlyMode;
    }

}
