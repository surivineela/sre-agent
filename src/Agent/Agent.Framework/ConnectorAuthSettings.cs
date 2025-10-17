// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;
using YamlDotNet.Serialization;

namespace Agent.Framework
{
    public class ConnectorAuthSettings
    {
        [Required]
        [YamlMember(Alias = "authentication_type")]
        public ConnectorAuthType AuthenticationType { get; set; }

        [YamlMember(Alias = "authority")]
        public string Authority { get; set; } = string.Empty;

        [YamlMember(Alias = "authority_host")]
        public string AuthorityHost { get; set; } = string.Empty;
        [YamlMember(Alias = "application_client_id")]
        public string ApplicationClientId { get; set; } = string.Empty;
        [YamlMember(Alias = "application_certificate")]
        public string ApplicationCertificate { get; set; } = string.Empty;
        [YamlMember(Alias = "managed_identity_client_id")]
        public string ManagedIdentityClientId { get; set; } = string.Empty;
        [YamlMember(Alias = "managed_identity_resource_id")]
        public string ManagedIdentityResourceId { get; set; } = string.Empty;
    }
}
