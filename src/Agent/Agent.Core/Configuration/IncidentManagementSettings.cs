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
        AzMonitor
    }

    public class IncidentManagementSettings
    {

        [Required]
        public IncidentManagementType? Type { get; set; }

        public string? ConnectionName  { get; set; }

        public string? ConnectionUrl { get; set; }

        public string? ConnectionKey { get; set; }

        // Write actions taken by this agent will be on behalf of this user. 
        // For PagerDuty, this is the email address of a valid user.
        public string? OboUser { get; set; }

        public ICMAPISettings ICMAPI { get; set; } = new();
    }

    public class ICMAPISettings
    {
        [Required]
        public string APIEndpoint { get; set; } = string.Empty;
        public string CertificateSubjectName { get; set; } = string.Empty;
        public bool ManagedIdentityEnabled { get; set; } = false;
        public string ManagedIdentityClientId { get; set; } = string.Empty;
        public string IcmMSIResource { get; set; } = "api://icmapi-prod";
        public string UserToken { get; set; } = string.Empty;
        //public bool Enabled { get; set; } = true;
        public bool ReadOnly { get; set; } = false;
    }
}
