// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace Agent.Core.Configuration
{
    public class DataConnectorSettings
    {
        /// <summary>
        /// Settings for each data connector types. These settings apply across all instances of each data connector type.
        /// </summary>
        [Required]
        public Dictionary<string, DataConnectorTypeSettings> Types { get; init; } = [];

        /// <summary>
        /// Instances of data connectors to be used by the agent. These are provided by the user and can be configured to connect to different data sources.
        /// </summary>
        [Required]
        public List<DataConnectorInstanceSettings> Connectors { get; init; } = [];
    }

    /// <summary>
    /// Settings for each type of data connector. These settings apply across all instances of each data connector type.
    /// </summary>
    public class DataConnectorTypeSettings
    {
        public DataConnectorSearchSettings? Search { get; init; }

        public DataConnectorStorageSettings? Storage { get; init; }
    }

    /// <summary>
    /// Settings for each type of data connector. These settings apply across all instances of each data connector type.
    /// </summary>
    public class DataConnectorSearchSettings
    {
        [Required]
        public string IndexName { get; init; } = string.Empty;

        [Required]
        public string IndexerName { get; init; } = string.Empty;

        [Required]
        public string SkillsetName { get; init; } = string.Empty;

        [Required]
        public string DataSourceName { get; init; } = string.Empty;
    }

    /// <summary>
    /// Settings for each type of data connector. These settings apply across all instances of each data connector type.
    /// </summary>
    public class DataConnectorStorageSettings
    {
        [Required]
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
        public string DataSource { get; init; } = string.Empty;

        /// <summary>
        /// The resource ID of the managed identity to use for authentication.
        /// </summary>
        public string Identity { get; init; } = string.Empty;
    }
}

