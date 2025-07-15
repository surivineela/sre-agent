// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
// A tool with this attribute overrides the default obo context for the obo flow
// Use this attribute to:
// 1. Disable obo flow for the tool
// 2. Specify a non-ARM scope for the obo token
public class OboContextAttribute : Attribute
{
    // If true, the obo flow won't be triggered on ToolExecutionUnauthorizedException
    public bool DisableObo { get; set; }
    // The scope to acquire the obo token for.
    public string Scope { get; set; }

    public OboContextAttribute(bool disableObo = false, string scope = Constants.DefaultOboTokenScope)
    {
        DisableObo = disableObo;
        Scope = scope;
    }
}

// A tool with this attribute enforces approval before execution when in review mode
// Approval is orthogonal to obo flow
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public class RequiresApprovalAttribute : Attribute
{
    public string? DisplayMessage { get; set; }
    public RequiresApprovalAttribute(string? displayMessage = null)
    {
        DisplayMessage = displayMessage;
    }
}

public class ApprovalRequiredException : Exception
{
    public ApprovalRequiredException()
    {
    }

    public ApprovalRequiredException(string message) : base(message)
    {
    }
}

public class ApprovalRejectedException : Exception
{
    public ApprovalRejectedException()
    {
    }

    public ApprovalRejectedException(string message) : base(message)
    {
    }
}
