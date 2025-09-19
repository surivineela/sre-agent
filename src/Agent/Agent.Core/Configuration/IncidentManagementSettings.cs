// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

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
    }

    public class AutomatedRCASettings
    {
        public bool Enabled { get; set; } = false;
        public string WebBaseUrl { get; set; } = string.Empty;
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
