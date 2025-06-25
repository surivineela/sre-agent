// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

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
        /// The resource ID of the managed identity that is assigned to the Azure AI Search service and has access to resources that AI Search depends on, such as the Azure Blob Storage account, Cosmos DB, and AOAI.
        /// Note: this is not necessarily the same identity that the Agent uses to authenticate with Azure AI Search.
        /// </summary>
        public string ManagedIdentityResourceId { get; init; } = string.Empty;
    }
}
