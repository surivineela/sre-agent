// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Plugins.Attributes
{
    /// <summary>
    /// Indicates that a function requires a threadId parameter.
    /// The GenericAgentOrchestrator will automatically inject the current threadId when calling functions with this attribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class ThreadSpecificAttribute : Attribute
    {
    }
}

