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
        /// </summary>
        public Dictionary<string, JsonElement>? ExtendedProperties { get; set; }

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
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum DataConnectorSource
    {
        Agent,
        AgentSpace
    }
}

