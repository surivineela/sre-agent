// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Data.DataModels;
using Agent.Framework;
using Agent.Runtime.IncidentIndexing;
using Agent.Runtime.IncidentIndexing.Services;
using Agent.Runtime.IncidentIndexing.Validators;
using Agent.Web.Authorization;
using Microsoft.AspNetCore.Mvc;
using ArmOperations = Agent.Core.Constants.ArmOperations;

namespace Agent.Web.Controllers.v2;

/// <summary>
/// API controller for incident indexing configuration.
/// Provides endpoints to configure incident platform settings for each provider.
///
/// Configuration endpoints: GET/PUT/DELETE /{providerType}/configuration
/// </summary>
[ApiController]
[Route("api/v2/incidents/indexing")]
public class IncidentIndexingController : ControllerBase
{
    private readonly ILogger<IncidentIndexingController> _logger;
    private readonly IIncidentIndexingConfigurationValidatorFactory _validatorFactory;
    private readonly IIncidentIndexingConfigurationServiceFactory _configServiceFactory;

    public IncidentIndexingController(
        ILogger<IncidentIndexingController> logger,
        IIncidentIndexingConfigurationValidatorFactory validatorFactory,
        IIncidentIndexingConfigurationServiceFactory configServiceFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _validatorFactory = validatorFactory ?? throw new ArgumentNullException(nameof(validatorFactory));
        _configServiceFactory = configServiceFactory ?? throw new ArgumentNullException(nameof(configServiceFactory));
    }

    #region Configuration Endpoints

    /// <summary>
    /// Get incident indexing configuration for a specific provider.
    /// </summary>
    /// <param name="providerType">The incident management provider (icm, pagerduty, servicenow, azmonitor).</param>
    /// <returns>The current configuration, or 404 if not configured.</returns>
    [HttpGet("{providerType}/configuration")]
    [AuthorizeArmOperation(ArmOperations.AgentIncidentManagementReadActionId)]
    [ProducesResponseType(typeof(IncidentIndexingConfigurationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetConfigurationAsync(IncidentManagementType providerType)
    {
        _logger.LogInternalInformation("GetConfiguration requested for provider: {Provider}", providerType);

        if (!IsValidProvider(providerType))
        {
            return BadRequest(new { Error = "Invalid provider", Details = $"Provider '{providerType}' is not supported." });
        }

        try
        {
            var result = await GetConfigurationByProviderAsync(providerType);
            if (result == null)
            {
                _logger.LogInternalInformation("GetConfiguration: No configuration found for provider {Provider}", providerType);
                return NotFound();
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "GetConfiguration failed for provider {Provider}", providerType);
            return StatusCode(StatusCodes.Status500InternalServerError, new { Error = "Internal server error", Details = ex.Message });
        }
    }

    /// <summary>
    /// Create or update incident indexing configuration for a specific provider.
    /// </summary>
    /// <param name="providerType">The incident management provider (icm, pagerduty, servicenow, azmonitor).</param>
    /// <param name="request">The configuration to save (must match provider type).</param>
    /// <returns>The saved configuration.</returns>
    [HttpPut("{providerType}/configuration")]
    [AuthorizeArmOperation(ArmOperations.AgentIncidentManagementWriteActionId)]
    [ProducesResponseType(typeof(IncidentIndexingConfigurationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateOrUpdateConfigurationAsync(
        IncidentManagementType providerType,
        [FromBody] IncidentIndexingConfigurationPayload request)
    {
        _logger.LogInternalInformation("CreateOrUpdateConfiguration requested for provider: {Provider}", providerType);

        if (!IsValidProvider(providerType))
        {
            return BadRequest(new { Error = "Invalid provider", Details = $"Provider '{providerType}' is not supported." });
        }

        // Validate payload type matches route provider
        if (!ValidatePayloadType(providerType, request, out var typeError))
        {
            return BadRequest(new { Error = "Payload type mismatch", Details = typeError });
        }

        try
        {
            var validator = _validatorFactory.GetValidator(providerType);
            if (validator == null)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { Error = "Internal server error", Details = $"{providerType} validator not configured." });
            }

            var validationResult = await validator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                return BadRequest(new { Error = validationResult.Error, Details = validationResult.Details });
            }

            var result = await CreateOrUpdateConfigurationByProviderAsync(providerType, request, validationResult);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "CreateOrUpdateConfiguration failed for provider {Provider}", providerType);
            return StatusCode(StatusCodes.Status500InternalServerError, new { Error = "Internal server error", Details = ex.Message });
        }
    }

    /// <summary>
    /// Delete incident indexing configuration for a specific provider.
    /// </summary>
    /// <param name="providerType">The incident management provider (icm, pagerduty, servicenow, azmonitor).</param>
    /// <returns>204 No Content on success, 404 if not found.</returns>
    [HttpDelete("{providerType}/configuration")]
    [AuthorizeArmOperation(ArmOperations.AgentIncidentManagementDeleteActionId)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteConfigurationAsync(IncidentManagementType providerType)
    {
        _logger.LogInternalInformation("DeleteConfiguration requested for provider: {Provider}", providerType);

        if (!IsValidProvider(providerType))
        {
            return BadRequest(new { Error = "Invalid provider", Details = $"Provider '{providerType}' is not supported." });
        }

        try
        {
            var success = await DeleteConfigurationByProviderAsync(providerType);
            if (!success)
            {
                _logger.LogInternalWarning("DeleteConfiguration: Configuration not found for provider {Provider}", providerType);
                return NotFound();
            }

            _logger.LogInternalInformation("DeleteConfiguration: Configuration deleted for provider {Provider}", providerType);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "DeleteConfiguration failed for provider {Provider}", providerType);
            return StatusCode(StatusCodes.Status500InternalServerError, new { Error = "Internal server error", Details = ex.Message });
        }
    }

    #endregion

    #region Provider-Specific Helpers

    private static bool IsValidProvider(IncidentManagementType providerType)
    {
        return providerType is IncidentManagementType.Icm
            or IncidentManagementType.PagerDuty
            or IncidentManagementType.ServiceNow
            or IncidentManagementType.AzMonitor;
    }

    private static bool ValidatePayloadType(
        IncidentManagementType providerType,
        IncidentIndexingConfigurationPayload request,
        out string? error)
    {
        var expectedType = providerType switch
        {
            IncidentManagementType.Icm => typeof(IcmIncidentIndexingConfigurationPayload),
            IncidentManagementType.PagerDuty => typeof(PagerDutyIncidentIndexingConfigurationPayload),
            IncidentManagementType.ServiceNow => typeof(ServiceNowIncidentIndexingConfigurationPayload),
            IncidentManagementType.AzMonitor => typeof(AzMonitorIncidentIndexingConfigurationPayload),
            _ => null
        };

        if (expectedType == null)
        {
            error = $"Provider {providerType} is not supported";
            return false;
        }

        if (request.GetType() != expectedType)
        {
            error = $"Request body type '{request.GetType().Name}' doesn't match provider '{providerType}'. Expected '{expectedType.Name}'.";
            return false;
        }

        error = null;
        return true;
    }

    private async Task<IncidentIndexingConfigurationResponse?> GetConfigurationByProviderAsync(IncidentManagementType providerType)
    {
        return providerType switch
        {
            IncidentManagementType.Icm => await GetIcmConfigurationResponse(),
            IncidentManagementType.PagerDuty => await GetPagerDutyConfigurationResponse(),
            IncidentManagementType.ServiceNow => await GetServiceNowConfigurationResponse(),
            IncidentManagementType.AzMonitor => await GetAzMonitorConfigurationResponse(),
            _ => null
        };
    }

    private async Task<IcmIncidentIndexingConfigurationResponse?> GetIcmConfigurationResponse()
    {
        var service = _configServiceFactory.GetService<IcmIncidentIndexingConfigurationDocument, IcmIncidentIndexingConfigurationPayload>(IncidentManagementType.Icm);
        var doc = service == null ? null : await service.GetConfigurationAsync();
        return doc == null ? null : IcmIncidentIndexingConfigurationResponse.FromDocument(doc);
    }

    private async Task<PagerDutyIncidentIndexingConfigurationResponse?> GetPagerDutyConfigurationResponse()
    {
        var service = _configServiceFactory.GetService<PagerDutyIncidentIndexingConfigurationDocument, PagerDutyIncidentIndexingConfigurationPayload>(IncidentManagementType.PagerDuty);
        var doc = service == null ? null : await service.GetConfigurationAsync();
        return doc == null ? null : PagerDutyIncidentIndexingConfigurationResponse.FromDocument(doc);
    }

    private async Task<ServiceNowIncidentIndexingConfigurationResponse?> GetServiceNowConfigurationResponse()
    {
        var service = _configServiceFactory.GetService<ServiceNowIncidentIndexingConfigurationDocument, ServiceNowIncidentIndexingConfigurationPayload>(IncidentManagementType.ServiceNow);
        var doc = service == null ? null : await service.GetConfigurationAsync();
        return doc == null ? null : ServiceNowIncidentIndexingConfigurationResponse.FromDocument(doc);
    }

    private async Task<AzMonitorIncidentIndexingConfigurationResponse?> GetAzMonitorConfigurationResponse()
    {
        var service = _configServiceFactory.GetService<AzMonitorIncidentIndexingConfigurationDocument, AzMonitorIncidentIndexingConfigurationPayload>(IncidentManagementType.AzMonitor);
        var doc = service == null ? null : await service.GetConfigurationAsync();
        return doc == null ? null : AzMonitorIncidentIndexingConfigurationResponse.FromDocument(doc);
    }

    private async Task<IncidentIndexingConfigurationResponse?> CreateOrUpdateConfigurationByProviderAsync(
        IncidentManagementType providerType,
        IncidentIndexingConfigurationPayload request,
        IncidentIndexingValidationResult validationResult)
    {
        return providerType switch
        {
            IncidentManagementType.Icm => await CreateOrUpdateIcmConfiguration((IcmIncidentIndexingConfigurationPayload)request, validationResult),
            IncidentManagementType.PagerDuty => await CreateOrUpdatePagerDutyConfiguration((PagerDutyIncidentIndexingConfigurationPayload)request, validationResult),
            IncidentManagementType.ServiceNow => await CreateOrUpdateServiceNowConfiguration((ServiceNowIncidentIndexingConfigurationPayload)request, validationResult),
            IncidentManagementType.AzMonitor => await CreateOrUpdateAzMonitorConfiguration((AzMonitorIncidentIndexingConfigurationPayload)request, validationResult),
            _ => null
        };
    }

    private async Task<IcmIncidentIndexingConfigurationResponse?> CreateOrUpdateIcmConfiguration(
        IcmIncidentIndexingConfigurationPayload request,
        IncidentIndexingValidationResult validationResult)
    {
        var service = _configServiceFactory.GetService<IcmIncidentIndexingConfigurationDocument, IcmIncidentIndexingConfigurationPayload>(IncidentManagementType.Icm);
        var doc = service == null ? null : await service.CreateOrUpdateConfigurationAsync(request, validationResult);
        return doc == null ? null : IcmIncidentIndexingConfigurationResponse.FromDocument(doc);
    }

    private async Task<PagerDutyIncidentIndexingConfigurationResponse?> CreateOrUpdatePagerDutyConfiguration(
        PagerDutyIncidentIndexingConfigurationPayload request,
        IncidentIndexingValidationResult validationResult)
    {
        var service = _configServiceFactory.GetService<PagerDutyIncidentIndexingConfigurationDocument, PagerDutyIncidentIndexingConfigurationPayload>(IncidentManagementType.PagerDuty);
        var doc = service == null ? null : await service.CreateOrUpdateConfigurationAsync(request, validationResult);
        return doc == null ? null : PagerDutyIncidentIndexingConfigurationResponse.FromDocument(doc);
    }

    private async Task<ServiceNowIncidentIndexingConfigurationResponse?> CreateOrUpdateServiceNowConfiguration(
        ServiceNowIncidentIndexingConfigurationPayload request,
        IncidentIndexingValidationResult validationResult)
    {
        var service = _configServiceFactory.GetService<ServiceNowIncidentIndexingConfigurationDocument, ServiceNowIncidentIndexingConfigurationPayload>(IncidentManagementType.ServiceNow);
        var doc = service == null ? null : await service.CreateOrUpdateConfigurationAsync(request, validationResult);
        return doc == null ? null : ServiceNowIncidentIndexingConfigurationResponse.FromDocument(doc);
    }

    private async Task<AzMonitorIncidentIndexingConfigurationResponse?> CreateOrUpdateAzMonitorConfiguration(
        AzMonitorIncidentIndexingConfigurationPayload request,
        IncidentIndexingValidationResult validationResult)
    {
        var service = _configServiceFactory.GetService<AzMonitorIncidentIndexingConfigurationDocument, AzMonitorIncidentIndexingConfigurationPayload>(IncidentManagementType.AzMonitor);
        var doc = service == null ? null : await service.CreateOrUpdateConfigurationAsync(request, validationResult);
        return doc == null ? null : AzMonitorIncidentIndexingConfigurationResponse.FromDocument(doc);
    }

    private async Task<bool> DeleteConfigurationByProviderAsync(IncidentManagementType providerType)
    {
        return providerType switch
        {
            IncidentManagementType.Icm => await DeleteIcmConfiguration(),
            IncidentManagementType.PagerDuty => await DeletePagerDutyConfiguration(),
            IncidentManagementType.ServiceNow => await DeleteServiceNowConfiguration(),
            IncidentManagementType.AzMonitor => await DeleteAzMonitorConfiguration(),
            _ => false
        };
    }

    private async Task<bool> DeleteIcmConfiguration()
    {
        var service = _configServiceFactory.GetService<IcmIncidentIndexingConfigurationDocument, IcmIncidentIndexingConfigurationPayload>(IncidentManagementType.Icm);
        return service != null && await service.DeleteConfigurationAsync();
    }

    private async Task<bool> DeletePagerDutyConfiguration()
    {
        var service = _configServiceFactory.GetService<PagerDutyIncidentIndexingConfigurationDocument, PagerDutyIncidentIndexingConfigurationPayload>(IncidentManagementType.PagerDuty);
        return service != null && await service.DeleteConfigurationAsync();
    }

    private async Task<bool> DeleteServiceNowConfiguration()
    {
        var service = _configServiceFactory.GetService<ServiceNowIncidentIndexingConfigurationDocument, ServiceNowIncidentIndexingConfigurationPayload>(IncidentManagementType.ServiceNow);
        return service != null && await service.DeleteConfigurationAsync();
    }

    private async Task<bool> DeleteAzMonitorConfiguration()
    {
        var service = _configServiceFactory.GetService<AzMonitorIncidentIndexingConfigurationDocument, AzMonitorIncidentIndexingConfigurationPayload>(IncidentManagementType.AzMonitor);
        return service != null && await service.DeleteConfigurationAsync();
    }

    #endregion
}
