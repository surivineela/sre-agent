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
    }
}
