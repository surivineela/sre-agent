// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;
using System.Text.Json;
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

        // Managed Identity as FIC mode properties
        [YamlMember(Alias = "use_managed_identity_as_fic")]
        public bool UseManagedIdentityAsFic { get; set; } = false;
        [YamlMember(Alias = "federated_client_id")]
        public string? FederatedClientId { get; set; }
        [YamlMember(Alias = "federated_tenant_id")]
        public string? FederatedTenantId { get; set; }

        public static ConnectorAuthSettings CreateFromManagedIdentity(
            string? managedIdentityResourceId,
            bool isDevelopment,
            bool useManagedIdentityAsFic = false,
            string? federatedClientId = null,
            string? federatedTenantId = null)
        {
            if (isDevelopment)
            {
                return new ConnectorAuthSettings()
                {
                    AuthenticationType = ConnectorAuthType.User
                };
            }

            // Build ConnectorAuthSettings
            var authSettings = new ConnectorAuthSettings()
            {
                AuthenticationType = string.IsNullOrEmpty(managedIdentityResourceId) ? ConnectorAuthType.ManagedIdentity : ConnectorAuthType.UAMI,
                ManagedIdentityResourceId = managedIdentityResourceId ?? string.Empty
            };

            // Add Managed Identity as FIC configuration if enabled
            if (useManagedIdentityAsFic)
            {
                if (string.IsNullOrEmpty(federatedClientId) || string.IsNullOrEmpty(federatedTenantId))
                {
                    throw new InvalidOperationException("Managed Identity as FIC mode requires both FederatedClientId and FederatedTenantId in ExtendedProperties");
                }

                authSettings.UseManagedIdentityAsFic = true;
                authSettings.FederatedClientId = federatedClientId;
                authSettings.FederatedTenantId = federatedTenantId;
            }

            return authSettings;
        }
    }
}
