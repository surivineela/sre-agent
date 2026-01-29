// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;

namespace Agent.Runtime.IncidentIndexing.Validators;

/// <summary>
/// Factory interface for resolving incident indexing configuration validators.
/// </summary>
public interface IIncidentIndexingConfigurationValidatorFactory
{
    /// <summary>
    /// Gets the validator for a specific provider type.
    /// </summary>
    /// <param name="providerType">The incident management provider type.</param>
    /// <returns>The validator for the provider, or null if not supported.</returns>
    IIncidentIndexingConfigurationValidator? GetValidator(IncidentManagementType providerType);
}

/// <summary>
/// Factory for resolving incident indexing configuration validators.
/// </summary>
public class IncidentIndexingConfigurationValidatorFactory : IIncidentIndexingConfigurationValidatorFactory
{
    private readonly IServiceProvider _serviceProvider;

    public IncidentIndexingConfigurationValidatorFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public IIncidentIndexingConfigurationValidator? GetValidator(IncidentManagementType providerType)
    {
        return providerType switch
        {
            IncidentManagementType.Icm => _serviceProvider.GetService(typeof(IcmIncidentIndexingConfigurationValidator)) as IIncidentIndexingConfigurationValidator,
            IncidentManagementType.PagerDuty => _serviceProvider.GetService(typeof(PagerDutyIncidentIndexingConfigurationValidator)) as IIncidentIndexingConfigurationValidator,
            IncidentManagementType.ServiceNow => _serviceProvider.GetService(typeof(ServiceNowIncidentIndexingConfigurationValidator)) as IIncidentIndexingConfigurationValidator,
            IncidentManagementType.AzMonitor => _serviceProvider.GetService(typeof(AzMonitorIncidentIndexingConfigurationValidator)) as IIncidentIndexingConfigurationValidator,
            IncidentManagementType.None => null,
            _ => null
        };
    }
}
