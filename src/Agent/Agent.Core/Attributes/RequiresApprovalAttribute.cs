// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Attributes;

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
