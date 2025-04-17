// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.ObjectModel;

namespace Agent.Core.Configuration
{
    public class AzureSearchSettings
    {
        public string SearchServiceUri { get; set; } = string.Empty;
        public string UserAssignedMIClientId { get; set; } = string.Empty;
        public string SearchApiKeyOverride { get; set; } = string.Empty;
        public List<AzureSeachIndexSettings> SearchIndexes { get; set; } = new List<AzureSeachIndexSettings>();
    }

    public class AzureSeachIndexSettings
    {
        public string IndexName { get; set; } = string.Empty;
        public string FieldsToSelectCsv { get; set; } = string.Empty;

        private List<string> _fieldsToSelect = new List<string>();
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

        public bool SemanticSearchEnabled { get; set; } = false;
        public bool VectorSearchEnabled { get; set; } = false;
        public string VectorFieldNamesCsv { get; set; } = string.Empty;

        private List<string> _vectorFieldNames = new List<string>();
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

