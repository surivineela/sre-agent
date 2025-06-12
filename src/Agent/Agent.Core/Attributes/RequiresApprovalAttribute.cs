// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public class RequiresApprovalAttribute : Attribute
{
    public string? DisplayMessage { get; set; }
    // Temp workaround
    // Only set to false if the plugin requires token scopes other than "https://management.core.windows.net/.default" because OBO token today hardcodes scope
    // When set to false, the action identity / crawler identity (if action identity is not set) will be used
    public bool UseOboToken { get; set; }
    // The scope to acquire the obo token for.
    // Not implemented yet
    public string Scope { get; set; }

    public RequiresApprovalAttribute(string? displayMessage = null, bool useOboToken = true, string scope = Constants.DefaultOboTokenScope)
    {
        DisplayMessage = displayMessage;
        UseOboToken = useOboToken;
        Scope = scope;
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
