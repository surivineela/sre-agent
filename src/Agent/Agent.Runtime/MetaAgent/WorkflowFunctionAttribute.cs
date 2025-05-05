// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Runtime.MetaAgent;

// Decorator for any Plugin Workflow class needs to be registered by Metaagent
[AttributeUsage(AttributeTargets.Class)]
public class WorkflowClassAttribute : Attribute
{
}


// Decorator for any Plugin Workflow function needs to be registered by Metaagent
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class WorkflowFunctionAttribute : Attribute
{
}


