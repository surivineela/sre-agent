// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace FirstPartyAgent.Core.Configuration
{
    public class ApplensSettings
    {
        /// <summary>
        /// Determines if the Applens integration is enabled
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Base URL for the RuntimeHost API
        /// </summary>
        public string RuntimeHost { get; set; } = string.Empty;

        /// <summary>
        /// Client ID for the MSI used to authenticate with Applens
        /// </summary>
        public string MsiClientId { get; set; } = string.Empty;

        /// <summary>
        /// Scope for the MSI used to authenticate with Applens
        /// </summary>
        public string Scope { get; set; } = string.Empty;
    }
}
