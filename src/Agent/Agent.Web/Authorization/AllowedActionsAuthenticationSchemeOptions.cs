// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Azure.ResourceManager.Storage.Models;
using Microsoft.AspNetCore.Authentication;

public static class AllowedActionsAuthenticationConstants
{
    public const string SchemeName = "AllowedActions";
    public const string ActionHeaderName = "x-allowed-actions";
    public const string FeatureFlagName = "PdpAuthZv2";
    public const string AllowedActionClaimType = "AllowedAction";
}

public class AllowedActionsAuthenticationSchemeOptions : AuthenticationSchemeOptions
{
    public string ActionHeaderName { get; set; } = AllowedActionsAuthenticationConstants.ActionHeaderName;
}