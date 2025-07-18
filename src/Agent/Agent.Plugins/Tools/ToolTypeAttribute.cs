// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ToolTypeAttribute : Attribute
{
    public string Name { get; }

    public ToolTypeAttribute(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }
}
