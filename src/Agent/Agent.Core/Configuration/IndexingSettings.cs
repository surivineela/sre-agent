// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Azure.Core;

namespace Agent.Core.Configuration
{
    public class IndexingSettings
    {
        /// <summary>
        /// The endpoint for the Azure AI Search service
        /// </summary>
        public string SearchEndpoint { get; init; } = string.Empty;

        /// <summary>
        /// The resource ID of the Azure Blob Storage account used to hold content that will be indexed
        /// </summary>
        public string BlobStorageResourceId { get; init; } = string.Empty;

        /// <summary>
        /// The resource ID of the managed identity that is assigned to the Azure AI Search service
        /// </summary>
        public string ManagedIdentityResourceId { get; init; } = string.Empty;
    }
}
