// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Data.DataModels
{
    public interface ICosmosDocument
    {
        string Id { get; }
        string DocumentType { get; }
        string PartitionKey { get; } // Defines the partition key value
    }
}
