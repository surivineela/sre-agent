// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Agent.Data.DataModels
{
    public interface ICosmosDocument
    {
        string Id { get; }
        string DocumentType { get; }
        string PartitionKey { get; } // Defines the partition key value

        [JsonIgnore]
        public static abstract string ContainerName { get; }
    }
}
