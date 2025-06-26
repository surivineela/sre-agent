// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Configuration
{
    public class SearchSettings
    {
        /// <summary>
        /// The endpoint for the Azure AI Search service
        /// </summary>
        public string SearchServiceEndpoint { get; set; }

        /// <summary>
        /// The default index name to use if not specified
        /// </summary>
        public string IndexName { get; set; }
    }
}
