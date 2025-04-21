// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels;
using Microsoft.Azure.Cosmos;

namespace Agent.Data.Helpers;

public static class CosmosHelpers
{
    public static Container GetContainer<T>(this CosmosClient client, string databaseId) where T : ICosmosDocument
    {
        return client.GetContainer(databaseId, T.ContainerName);
    }
}
