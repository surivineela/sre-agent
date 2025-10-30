// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Azure.Monitoring.DGrep.DataContracts.External;

namespace Agent.Plugins.Interface;

public interface IDGrepPluginClient
{
    Task<string> ExecuteDGrepQuery(string nameSpace, string eventName, string serverQuery, string clientQuery, string filters, QueryType queryType, DateTime startTime, DateTime endTime, int maxResults = 10, CancellationToken cancellationToken = default);
}
