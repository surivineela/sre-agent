// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace Agent.Core.Configuration
{
    /// <summary>
    /// Configuration settings for TSG crawling operations
    /// </summary>
    public class TsgCrawlerSettings
    {
        /// <summary>
        /// Whether TSG crawling is enabled
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Type of TSG crawler
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Name of the indexer to use
        /// </summary>
        public string IndexerName { get; set; } = string.Empty;

        /// <summary>
        /// Root path for TSG content
        /// </summary>
        public string TsgRootPath { get; set; } = string.Empty;

        /// <summary>
        /// Azure DevOps repository settings
        /// </summary>
        public AzureDevOpsSettings DevOpsRepoSettings { get; set; } = new();

        /// <summary>
        /// Azure Search settings for TSG indexing
        /// </summary>
        public AzureSearchSettings AiSearchSettings { get; set; } = new();

        /// <summary>
        /// Storage account settings for TSG content
        /// </summary>
        public StorageAccountSettings TsgStorageSettings { get; set; } = new();
    }
}
