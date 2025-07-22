// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;
using System.Xml.Linq;
using Agent.Runtime.Reasoning.Models;

namespace Agent.Runtime.Reasoning.Models
{
    public class ConnectorAuthSettings
    {
        [Required]
        public ConnectorAuthType AuthenticationType { get; set; }

        public string Authority { get; set; } = string.Empty;
        public string AuthorityHost { get; set; } = string.Empty;
        public string ApplicationClientId { get; set; } = string.Empty;
        public string ApplicationCertificate { get; set; } = string.Empty;
        public string ManagedIdentityClientId { get; set; } = string.Empty;
        public string ManagedIdentityResourceId { get; set; } = string.Empty;
    }
}
