// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Agent.Core.Helpers;
using Agent.Logging;
using Agent.Plugins.Interface;
using Agent.Plugins.Models;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Implementation;

/// <summary>
/// Implementation of the Function App Configuration Checks Plugin
/// </summary>
public class FunctionAppConfigurationChecksPlugin : IFunctionAppConfigurationChecksPlugin
{
    private readonly ILogger<FunctionAppConfigurationChecksPlugin> _logger;
    private readonly ArmHelper _armHelper;

    /// <summary>
    /// Gets or sets the thread ID
    /// </summary>
    public Guid? ThreadId { get; set; }

    /// <summary>
    /// Constructor for FunctionAppConfigurationChecksPlugin
    /// </summary>
    /// <param name="logger">Logger for the plugin</param>
    /// <param name="armHelper">ARM helper for interacting with Azure resources</param>
    public FunctionAppConfigurationChecksPlugin(
        ILogger<FunctionAppConfigurationChecksPlugin> logger,
        ArmHelper armHelper)
    {
        _logger = logger;
        _armHelper = armHelper;
    }

    /// <summary>
    /// Gets Function App configuration checks for a Function App
    /// </summary>
    /// <param name="resourceId">The Azure resource ID of the Function App</param>
    /// <param name="startTime">Optional start time for the query (defaults to 1 hour ago)</param>
    /// <param name="endTime">Optional end time for the query (defaults to current time minus 15 minutes)</param>
    /// <returns>A summary of function app configuration checks</returns>
    public async Task<string> GetFunctionAppConfigurationChecks(string resourceId, DateTime? startTime = null, DateTime? endTime = null)
    {
        try
        {
            _logger.LogInternalInformation("Getting Function App configuration checks for {ResourceId}", resourceId);

            // Call GetAnalysisWithTime with the 'functionsettings' detector ID
            string result = await _armHelper.GetAnalysisWithTime(resourceId, "functionsettings", startTime, endTime);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error getting Function App configuration checks for {ResourceId}", resourceId);
            throw;
        }
    }

    /// <summary>
    /// Gets Event Grid subscriptions associated with a storage account used by a Function App.
    /// </summary>
    /// <param name="storageAccountResourceId">The resource ID of the storage account to check</param>
    /// <returns>A list of Event Grid subscription details</returns>
    public async Task<IReadOnlyList<EventGridSubscriptionInfo>> GetEventGridSubscriptionsAsync(string storageAccountResourceId)
    {
        _logger.LogInternalInformation($"[GetEventGridSubscriptionsAsync] Invoked with storageAccountResourceId: {storageAccountResourceId}");

        try
        {
            // Validate if the provided resource ID is well-formatted
            if (!_armHelper.IsWellFormattedResourceId(storageAccountResourceId))
            {
                _logger.LogInternalWarning($"Resource ID format is invalid: {storageAccountResourceId}");
                return new List<EventGridSubscriptionInfo>();
            }

            // Check if the storage account resource exists before attempting to get subscriptions
            bool resourceExists = await _armHelper.CheckIfResourceExistsAsync(storageAccountResourceId);
            if (!resourceExists)
            {
                _logger.LogInternalWarning($"Storage account resource not found: {storageAccountResourceId}");
                return new List<EventGridSubscriptionInfo>();
            }

            // Call the ARM helper method to get the Event Grid subscriptions
            string responseJson = await _armHelper.GetEventGridSubscriptionsAsync(storageAccountResourceId);
            
            // Check for error response
            if (responseJson.Contains("ResourceNotFound"))
            {
                _logger.LogInternalWarning($"Event Grid subscriptions retrieval failed - resource not found: {storageAccountResourceId}");
                return new List<EventGridSubscriptionInfo>();
            }
            
            // Parse the response
            return ParseEventGridSubscriptionsResponse(responseJson);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error in GetEventGridSubscriptionsAsync with resourceId {storageAccountResourceId}");
            return new List<EventGridSubscriptionInfo>();
        }
    }

    private IReadOnlyList<EventGridSubscriptionInfo> ParseEventGridSubscriptionsResponse(string responseJson)
    {
        var result = new List<EventGridSubscriptionInfo>();

        try
        {
            var responseDoc = JsonDocument.Parse(responseJson);
            
            // Check if the response has the expected structure
            if (responseDoc.RootElement.TryGetProperty("responses", out var responsesElement) && 
                responsesElement.ValueKind == JsonValueKind.Array)
            {
                // Process each response in the array
                foreach (var response in responsesElement.EnumerateArray())
                {
                    // Check for successful HTTP status code
                    if (response.TryGetProperty("httpStatusCode", out var statusCodeElement) && 
                        statusCodeElement.GetInt32() == 200 &&
                        response.TryGetProperty("content", out var contentElement))
                    {
                        // Process the content if it contains value array
                        if (contentElement.TryGetProperty("value", out var valueArray) && 
                            valueArray.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var subscription in valueArray.EnumerateArray())
                            {
                                var subscriptionInfo = ParseEventGridSubscription(subscription);
                                if (subscriptionInfo != null)
                                {
                                    result.Add(subscriptionInfo);
                                }
                            }
                        }
                    }
                }
            }
            else if (responseDoc.RootElement.TryGetProperty("value", out var valueArray) && 
                     valueArray.ValueKind == JsonValueKind.Array)
            {
                // Alternative structure where response might be direct without the 'responses' wrapper
                foreach (var subscription in valueArray.EnumerateArray())
                {
                    var subscriptionInfo = ParseEventGridSubscription(subscription);
                    if (subscriptionInfo != null)
                    {
                        result.Add(subscriptionInfo);
                    }
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogInternalError(ex, $"Error parsing Event Grid subscriptions response: {ex.Message}");
        }

        return result;
    }

    private EventGridSubscriptionInfo ParseEventGridSubscription(JsonElement subscription)
    {
        try
        {
            var info = new EventGridSubscriptionInfo
            {
                Id = subscription.TryGetProperty("id", out var id) ? id.GetString() : null,
                Name = subscription.TryGetProperty("name", out var name) ? name.GetString() : null,
                Type = subscription.TryGetProperty("type", out var type) ? type.GetString() : null
            };

            // Extract properties
            if (subscription.TryGetProperty("properties", out var properties))
            {
                // Extract topic
                info.Topic = properties.TryGetProperty("topic", out var topic) ? topic.GetString() : null;
                
                // Extract provisioningState
                info.ProvisioningState = properties.TryGetProperty("provisioningState", out var state) ? state.GetString() : null;
                
                // Extract destination information
                if (properties.TryGetProperty("destination", out var destination))
                {
                    info.DestinationType = destination.TryGetProperty("endpointType", out var endpointType) ? endpointType.GetString() : null;
                    
                    if (destination.TryGetProperty("properties", out var destProps))
                    {
                        info.EndpointUrl = destProps.TryGetProperty("endpointBaseUrl", out var url) ? url.GetString() : null;
                        
                        // TLS version
                        if (destProps.TryGetProperty("minimumTlsVersionAllowed", out var tlsVersion))
                        {
                            info.MinimumTlsVersion = tlsVersion.GetString();
                        }
                    }
                }
                
                // Extract filter information
                if (properties.TryGetProperty("filter", out var filter))
                {
                    info.SubjectBeginsWith = filter.TryGetProperty("subjectBeginsWith", out var begins) ? begins.GetString() : null;
                    info.SubjectEndsWith = filter.TryGetProperty("subjectEndsWith", out var ends) ? ends.GetString() : null;
                    
                    // Extract included event types
                    if (filter.TryGetProperty("includedEventTypes", out var eventTypes) && 
                        eventTypes.ValueKind == JsonValueKind.Array)
                    {
                        info.IncludedEventTypes = new List<string>();
                        foreach (var eventType in eventTypes.EnumerateArray())
                        {
                            if (eventType.ValueKind == JsonValueKind.String)
                            {
                                info.IncludedEventTypes.Add(eventType.GetString());
                            }
                        }
                    }
                }
                
                // Extract retry policy
                if (properties.TryGetProperty("retryPolicy", out var retryPolicy))
                {
                    if (retryPolicy.TryGetProperty("maxDeliveryAttempts", out var attempts))
                    {
                        info.MaxDeliveryAttempts = attempts.GetInt32();
                    }
                    
                    if (retryPolicy.TryGetProperty("eventTimeToLiveInMinutes", out var ttl))
                    {
                        info.EventTimeToLiveInMinutes = ttl.GetInt32();
                    }
                }
            }

            return info;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error parsing Event Grid subscription: {ex.Message}");
            return null;
        }
    }
}
