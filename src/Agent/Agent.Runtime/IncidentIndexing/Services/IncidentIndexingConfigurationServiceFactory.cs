// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Data.DataModels;
using Microsoft.Extensions.DependencyInjection;

namespace Agent.Runtime.IncidentIndexing.Services;

/// <summary>
/// Factory interface for resolving incident indexing configuration services.
/// </summary>
public interface IIncidentIndexingConfigurationServiceFactory
{
    /// <summary>
    /// Gets the typed service for a specific provider.
    /// </summary>
    /// <typeparam name="TDocument">The document type.</typeparam>
    /// <typeparam name="TPayload">The payload type.</typeparam>
    /// <param name="providerType">The provider type to get service for.</param>
    /// <returns>The typed service, or null if not found.</returns>
    IIncidentIndexingConfigurationService<TDocument, TPayload>? GetService<TDocument, TPayload>(IncidentManagementType providerType)
        where TDocument : IIncidentIndexingConfigurationDocument
        where TPayload : IncidentIndexingConfigurationPayload;

    /// <summary>
    /// Gets the service dynamically for a provider type.
    /// Returns the service as an object that must be cast to the appropriate type.
    /// </summary>
    /// <param name="providerType">The provider type.</param>
    /// <returns>The service object, or null if not supported.</returns>
    object? GetServiceDynamic(IncidentManagementType providerType);
}

/// <summary>
/// Factory for resolving incident indexing configuration services.
/// </summary>
public class IncidentIndexingConfigurationServiceFactory : IIncidentIndexingConfigurationServiceFactory
{
    private readonly IServiceProvider _serviceProvider;

    public IncidentIndexingConfigurationServiceFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public IIncidentIndexingConfigurationService<TDocument, TPayload>? GetService<TDocument, TPayload>(IncidentManagementType providerType)
        where TDocument : IIncidentIndexingConfigurationDocument
        where TPayload : IncidentIndexingConfigurationPayload
    {
        return providerType switch
        {
            IncidentManagementType.Icm =>
                _serviceProvider.GetService<IIncidentIndexingConfigurationService<IcmIncidentIndexingConfigurationDocument, IcmIncidentIndexingConfigurationPayload>>()
                    as IIncidentIndexingConfigurationService<TDocument, TPayload>,

            IncidentManagementType.PagerDuty =>
                _serviceProvider.GetService<IIncidentIndexingConfigurationService<PagerDutyIncidentIndexingConfigurationDocument, PagerDutyIncidentIndexingConfigurationPayload>>()
                    as IIncidentIndexingConfigurationService<TDocument, TPayload>,

            IncidentManagementType.ServiceNow =>
                _serviceProvider.GetService<IIncidentIndexingConfigurationService<ServiceNowIncidentIndexingConfigurationDocument, ServiceNowIncidentIndexingConfigurationPayload>>()
                    as IIncidentIndexingConfigurationService<TDocument, TPayload>,

            IncidentManagementType.AzMonitor =>
                _serviceProvider.GetService<IIncidentIndexingConfigurationService<AzMonitorIncidentIndexingConfigurationDocument, AzMonitorIncidentIndexingConfigurationPayload>>()
                    as IIncidentIndexingConfigurationService<TDocument, TPayload>,

            _ => null
        };
    }

    public object? GetServiceDynamic(IncidentManagementType providerType)
    {
        return providerType switch
        {
            IncidentManagementType.Icm =>
                _serviceProvider.GetService<IIncidentIndexingConfigurationService<IcmIncidentIndexingConfigurationDocument, IcmIncidentIndexingConfigurationPayload>>(),

            IncidentManagementType.PagerDuty =>
                _serviceProvider.GetService<IIncidentIndexingConfigurationService<PagerDutyIncidentIndexingConfigurationDocument, PagerDutyIncidentIndexingConfigurationPayload>>(),

            IncidentManagementType.ServiceNow =>
                _serviceProvider.GetService<IIncidentIndexingConfigurationService<ServiceNowIncidentIndexingConfigurationDocument, ServiceNowIncidentIndexingConfigurationPayload>>(),

            IncidentManagementType.AzMonitor =>
                _serviceProvider.GetService<IIncidentIndexingConfigurationService<AzMonitorIncidentIndexingConfigurationDocument, AzMonitorIncidentIndexingConfigurationPayload>>(),

            _ => null
        };
    }
}
