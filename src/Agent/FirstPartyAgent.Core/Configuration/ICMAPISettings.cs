// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace FirstPartyAgent.Core.Configuration
{
    public class ICMAPISettings
    {
        [Required]
        public string APIEndpoint { get; set; } = string.Empty;
        public string CertificateSubjectName { get; set; } = string.Empty;
        public string UserToken { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
        public bool ReadOnly { get; set; } = false;
    }
}

