// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace Agent.Core.Configuration
{
    public class DataConnectorSettings
    {
        /// <summary>
        /// Each data connector instance must have a unique name.
        /// </summary>
        [Required]
        public string DataConnectorName { get; init; } = string.Empty;

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
        public string IdentityResourceId { get; init; } = string.Empty;
    }
}

