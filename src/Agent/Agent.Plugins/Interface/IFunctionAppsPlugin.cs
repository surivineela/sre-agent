// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Plugins.Models;

namespace Agent.Plugins.Interface;
public interface IFunctionAppsPlugin
{
    Task<IReadOnlyList<FunctionAppDescriptor>> ListFunctionAppsAsync(Guid subscriptionId);

    Task<FunctionAppDescriptor?> GetFunctionAppInfoAsync(string resourceId);

    Task<List<string>> GetFunctionAppDeploymentSlotsAsync(string resourceId);

    /// <summary>
    /// Triggers a TimerTrigger Azure Function
    /// </summary>
    /// <param name="functionAppResourceId">The resource ID of the function app</param>
    /// <param name="functionName">The name of the function to trigger (must be a TimerTrigger function)</param>
    /// <returns>Result of the function trigger operation</returns>
    Task<FunctionTriggerResponse> TriggerTimerFunctionAsync(
        string functionAppResourceId, 
        string functionName);
}
