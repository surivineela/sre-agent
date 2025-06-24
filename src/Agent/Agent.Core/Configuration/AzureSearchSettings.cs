// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Agent.Core.Configuration
{
    /// <summary>
    /// Configuration settings for Azure Cognitive Search services
    /// </summary>
    public class AzureSearchSettings
    {
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// URI for the Azure Search service
        /// </summary>
        public string SearchServiceUri { get; set; } = string.Empty;

        /// <summary>
        /// Client ID for user-assigned Managed Identity authentication
        /// </summary>
        public string UserAssignedMIClientId { get; set; } = string.Empty;

        /// <summary>
        /// API key override for direct authentication with Azure Search
        /// </summary>
        public string SearchApiKeyOverride { get; set; } = string.Empty;

        /// <summary>
        /// Collection of search index configurations
        /// </summary>
        public List<AzureSeachIndexSettings> SearchIndexes { get; set; } = new List<AzureSeachIndexSettings>();
    }

    /// <summary>
    /// Configuration settings for a specific Azure Search index
    /// </summary>
    public class AzureSeachIndexSettings
    {
        /// <summary>
        /// Name of the search index
        /// </summary>
        public string IndexName { get; set; } = string.Empty;

        /// <summary>
        /// Comma-separated list of fields to select in search queries
        /// </summary>
        public string FieldsToSelectCsv { get; set; } = string.Empty;

        private List<string> _fieldsToSelect = new List<string>();

        /// <summary>
        /// Collection of fields to select in search queries, parsed from FieldsToSelectCsv
        /// </summary>
        public ReadOnlyCollection<string> FieldsToSelect
        {
            get
            {
                if (_fieldsToSelect.Count == 0 && !string.IsNullOrWhiteSpace(FieldsToSelectCsv))
                {
                    _fieldsToSelect = FieldsToSelectCsv.Split(',').Where(f => !string.IsNullOrWhiteSpace(f)).Select(f => f.Trim()).ToList();
                }
                return _fieldsToSelect.AsReadOnly();
            }
        }

        /// <summary>
        /// Flag indicating if semantic search capabilities are enabled for this index
        /// </summary>
        public bool SemanticSearchEnabled { get; set; } = false;

        /// <summary>
        /// Flag indicating if vector search capabilities are enabled for this index
        /// </summary>
        public bool VectorSearchEnabled { get; set; } = false;

        /// <summary>
        /// Comma-separated list of vector field names for vector search
        /// </summary>
        public string VectorFieldNamesCsv { get; set; } = string.Empty;

        private List<string> _vectorFieldNames = new List<string>();

        /// <summary>
        /// Collection of vector field names, parsed from VectorFieldNamesCsv
        /// </summary>
        public ReadOnlyCollection<string> VectorFieldNames
        {
            get
            {
                if (_vectorFieldNames.Count == 0 && !string.IsNullOrWhiteSpace(VectorFieldNamesCsv))
                {
                    _vectorFieldNames = VectorFieldNamesCsv.Split(',').Where(f => !string.IsNullOrWhiteSpace(f)).Select(f => f.Trim()).ToList();
                }
                return _vectorFieldNames.AsReadOnly();
            }
        }
    }
}
