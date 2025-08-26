// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using Agent.Plugins.Implementation.AzureApplicationInsightsPlugin.Models;
using Azure.Core;
using Azure.Monitor.Query;

namespace Agent.Plugins.Implementation.AzureApplicationInsightsPlugin.Services
{
    public interface IAppLogsQueryClient
    {
        Task<IReadOnlyList<AppLogsQueryRow<T>>> QueryResourceAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(ResourceIdentifier resourceId, string kql, QueryTimeRange timeRange) where T : new();
    }
}
