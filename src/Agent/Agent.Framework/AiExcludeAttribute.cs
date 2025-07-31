// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------


namespace Agent.Framework;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class AiExcludeAttribute : Attribute
{
    public AiExcludeAttribute(string reason) { }
}
