// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Agent.Core.Configuration
{

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum IncidentManagementType
    {
        PagerDuty,
        Icm,
        AzMonitor,
        ServiceNow,
        None,
    }

    public class IncidentManagementSettings
    {
        [Required]
        public IncidentManagementType? Type { get; set; }

        public string? ConnectionName { get; set; }

        public string? ConnectionUrl { get; set; }

        public string? ConnectionKey { get; set; }

        // Write actions taken by this agent will be on behalf of this user.
        // For PagerDuty, this is the email address of a valid user.
        public string? OboUser { get; set; }

        public ICMAPISettings ICMAPI { get; set; } = new();

        public AutomatedRCASettings AutomatedRCA { get; set; } = new();

        /// <summary>
        /// Maximum number of automated investigation attempts for recurring alerts before requesting user input.
        /// When an alert fires repeatedly and automated RCA fails to find a definitive root cause,
        /// the agent will ask the user for additional context after this many attempts.
        /// </summary>
        public int MaxAutomatedInvestigationAttempts { get; set; } = 3;

        /// <summary>
        /// Name of the Azure API Connection resource for ServiceNow OAuth.
        /// This is set during OAuth setup and used by ServiceNowOAuthClient.
        /// When set, OAuth authentication is used instead of basic auth.
        /// Example: "servicenow-myagent-20260112120000"
        /// </summary>
        public string? ApiConnectionName { get; set; }
    }

    public class AutomatedRCASettings
    {
        public bool Enabled { get; set; } = false;
        public string WebBaseUrl { get; set; } = string.Empty;
        public string DefaultResultTag { get; set; } = "RCAPreflightProcessed";
        public Dictionary<string, string> ResultTags { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public string AccessNote { get; set; } = "\n\n> Note: One-time setup required before you can open the link:" +
                                           "\n> 1. Join the [SREAgent-Functions security group](https://idweb.microsoft.com/IdentityManagement/aspx/groups/MyGroups.aspx?popupFromClipboard=https%3A%2F%2Fidweb.microsoft.com%2Fidentitymanagement%2Faspx%2FGroups%2FEditGroup.aspx%3Fid%3De881e4a1-26b0-4e74-9bda-7e0fb6e4009c%26UOCInitialTabName%3DGroupingBasicInfo)." +
                                           "\n> 2. Go to https://aka.ms/sreagent-portal and add an External agent:" +
                                           "\n>    - Agent: Functions RCA" +
                                           "\n>    - URI: https://functionsrca-001--06ae8b31.3d3f9aed.eastus2.azuresre.ai" +
                                           "\n> After completing these steps once, you can access the link directly.";
    }

    public class ICMAPISettings
    {
        [Required]
        public string APIEndpoint { get; set; } = string.Empty;
        public string CertificateSubjectName { get; set; } = string.Empty;
        public string CertificateKeyVaultUri { get; set; } = string.Empty;
        public string CertificateKeyVaultSecretName { get; set; } = string.Empty;
        public string ManagedIdentityClientId { get; set; } = string.Empty;
        public string IcmMSIResource { get; set; } = "api://icmapi-prod";
        public string UserToken { get; set; } = string.Empty;
        public string OwningServiceId { get; set; } = string.Empty;
        public bool ReadOnly { get; set; } = false;
    }

    public class ServiceNowAPISettings
    {
        public string Endpoint { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
