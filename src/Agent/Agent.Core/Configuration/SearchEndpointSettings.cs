// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------


namespace Agent.Core.Configuration
{
    public class SearchEndpointSettings
    {
        public string SearchEndpointUrl { get; set; } = string.Empty;
        public bool EnableDocumentRetrieval { get; set; }
        public bool EnableVectorSearch { get; set; }
        public int VectorDimensions { get; set; }
    }
}

