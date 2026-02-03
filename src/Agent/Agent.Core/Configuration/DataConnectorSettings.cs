// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agent.Core.Configuration
{
    public class DataConnectorSettings
    {
        public DataConnectorSearchSettings Search { get; init; } = new();

        public DataConnectorStorageSettings Storage { get; init; } = new();
    }

    /// <summary>
    /// Settings for each type of data connector. These settings apply across all instances of each data connector type.
    /// </summary>
    public class DataConnectorSearchSettings
    {
        public string IndexName { get; init; } = string.Empty;

        public string IndexerName { get; init; } = string.Empty;

        public string SkillsetName { get; init; } = string.Empty;

        public string DataSourceName { get; init; } = string.Empty;
    }

    /// <summary>
    /// Settings for each type of data connector. These settings apply across all instances of each data connector type.
    /// </summary>
    public class DataConnectorStorageSettings
    {
        public string BlobStorageContainerName { get; init; } = string.Empty;
    }

    public class DataConnectorInstanceSettings
    {
        private Dictionary<string, JsonElement>? _extendedProperties;
        private Dictionary<string, JsonElement>? _parsedJsonCache;

        /// <summary>
        /// Each data connector instance must have a unique name.
        /// </summary>
        [Required]
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// The type of the data connector, which should match the DataConnectorAttribute on the implementation class.
        /// </summary>
        [Required]
        public string DataConnectorType { get; init; } = string.Empty;

        /// <summary>
        /// An arbitrary connection string that can be used to connect to a data source. This can be a URI, connection string, etc.
        /// </summary>
        [Required]
        public string DataSource { get; set; } = string.Empty;

        /// <summary>
        /// Extended properties for data connectors. This is used to replace the plain string DataSource for MCP connectors.
        /// Use JsonElement for configuration binding.
        /// Priority: ExtendedPropertiesJson > backing field (from configuration)
        /// </summary>
        public Dictionary<string, JsonElement>? ExtendedProperties
        {
            get
            {
                // Priority: ExtendedPropertiesJson > backing field
                if (!string.IsNullOrWhiteSpace(ExtendedPropertiesJson))
                {
                    // Cache the parsed result to avoid re-parsing on every access
                    return _parsedJsonCache ??= ParseJsonToExtendedProperties(ExtendedPropertiesJson);
                }
                return _extendedProperties;
            }
            set
            {
                _extendedProperties = value;
                _parsedJsonCache = null; // Clear cache when explicitly set
            }
        }

        /// <summary>
        /// Extended properties as JSON string. This is parsed into ExtendedProperties if provided.
        /// Useful for environment variable configuration where special characters in keys are problematic.
        /// </summary>
        public string? ExtendedPropertiesJson { get; init; }

        /// <summary>
        /// The resource ID of the managed identity to use for authentication.
        /// </summary>
        public string Identity { get; init; } = string.Empty;

        /// <summary>
        /// The Key Vault URI for certificate-based authentication.
        /// This can be a full certificate URI (e.g., https://myvault.vault.azure.net/certificates/mycert/version)
        /// or just the vault URI if CertificateSecretName is specified separately.
        /// </summary>
        public string? KeyVaultUri { get; init; }

        /// <summary>
        /// The source of the data connector authentication.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public DataConnectorSource Source { get; init; } = DataConnectorSource.Agent;

        /// <summary>
        /// Indicates whether to use Managed Identity as Federated Identity Credential (FIC).
        /// Parsed from ExtendedProperties["useManagedIdentityAsFic"].
        /// </summary>
        [JsonIgnore]
        public bool UseManagedIdentityAsFic
        {
            get
            {
                if (ExtendedProperties != null &&
                    ExtendedProperties.TryGetValue("useManagedIdentityAsFic", out var useFicElement))
                {
                    var useFicString = useFicElement.ToString();
                    return bool.TryParse(useFicString, out var parsedValue) && parsedValue;
                }
                return false;
            }
        }

        /// <summary>
        /// The federated client ID for FIC authentication.
        /// Parsed from ExtendedProperties["federatedClientId"] when UseManagedIdentityAsFic is true.
        /// </summary>
        [JsonIgnore]
        public string? FederatedClientId
        {
            get
            {
                if (ExtendedProperties != null &&
                    ExtendedProperties.TryGetValue("federatedClientId", out var clientIdElement))
                {
                    return clientIdElement.GetString();
                }
                return null;
            }
        }

        /// <summary>
        /// The federated tenant ID for FIC authentication.
        /// Parsed from ExtendedProperties["federatedTenantId"] when UseManagedIdentityAsFic is true.
        /// </summary>
        [JsonIgnore]
        public string? FederatedTenantId
        {
            get
            {
                if (ExtendedProperties != null &&
                    ExtendedProperties.TryGetValue("federatedTenantId", out var tenantIdElement))
                {
                    return tenantIdElement.GetString();
                }
                return null;
            }
        }

        /// <summary>
        /// Parses a JSON string into a dictionary of JsonElement values for use as ExtendedProperties.
        /// </summary>
        /// <param name="json">The JSON string to parse.</param>
        /// <returns>A dictionary mapping property names to JsonElement values.</returns>
        /// <exception cref="ArgumentException">Thrown if the JSON is null, empty, or invalid.</exception>
        private static Dictionary<string, JsonElement> ParseJsonToExtendedProperties(string json)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(json);

            try
            {
                using var doc = JsonDocument.Parse(json);
                var result = new Dictionary<string, JsonElement>();
                foreach (var property in doc.RootElement.EnumerateObject())
                {
                    result[property.Name] = property.Value.Clone();
                }
                return result;
            }
            catch (JsonException ex)
            {
                throw new ArgumentException("ExtendedPropertiesJson is not valid JSON.", ex);
            }
        }
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum DataConnectorSource
    {
        Agent,
        AgentSpace
    }
}

