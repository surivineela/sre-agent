// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Agent.Plugins.Models;

namespace Agent.Plugins.Interface;

/// <summary>
/// Plugin for diagnosing and fixing Function App configuration issues
/// </summary>
public interface IFunctionAppConfigurationChecksPlugin
{
    /// <summary>
    /// Gets the thread ID for the plugin
    /// </summary>
    Guid? ThreadId { get; set; }

    /// <summary>
    /// Gets Function App configuration checks for a Function App
    /// </summary>
    /// <param name="resourceId">The Azure resource ID of the Function App</param>
    /// <param name="startTime">Optional start time for the query (defaults to 1 hour ago)</param>
    /// <param name="endTime">Optional end time for the query (defaults to current time minus 15 minutes)</param>
    /// <returns>A summary of function app configuration checks</returns>
    Task<string> GetFunctionAppConfigurationChecks(string resourceId, DateTime? startTime = null, DateTime? endTime = null);

    /// <summary>
    /// Gets Event Grid subscriptions for a storage account
    /// </summary>
    /// <param name="storageAccountResourceId">The Azure resource ID of the storage account</param>
    /// <returns>A read-only list of Event Grid subscription information</returns>
    Task<IReadOnlyList<EventGridSubscriptionInfo>> GetEventGridSubscriptionsAsync(string storageAccountResourceId);
}
