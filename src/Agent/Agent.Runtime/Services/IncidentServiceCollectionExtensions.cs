// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using System.Text.Json.Nodes;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models.ServiceNow;
using Agent.Core.Services;
using Agent.Data.DataModels;
using Agent.Data.DataModels.IncidentModel;
using Agent.Data.Interface.IncidentAPI;
using Agent.Graph.Interfaces;
using Agent.Graph.Services;
using Agent.Runtime.Helpers;
using Agent.Runtime.IncidentIndexing;
using Agent.Runtime.IncidentIndexing.Services;
using Agent.Runtime.IncidentIndexing.Validators;
using Agent.Runtime.Interfaces;
using Agent.Runtime.Services.IncidentTriggerDetection;
using Agent.Runtime.SubAgents.Scanner;
using Azure.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SREAgent.Incidents.IcM.Model;
using Microsoft.SREAgent.Incidents.IcM.Service;
using IICMAPIClient = Agent.Core.Services.IICMAPIClient;


namespace Agent.Runtime.Services;

public static class IncidentServiceCollectionExtensions
{
    private static IServiceCollection AddDefaultIncidentApiClientsAndScanner(this IServiceCollection services)
    {
        services.AddSingleton<IPagerDutyService, NullablePagerDutyService>();

        //services.AddSingleton<IICMAPIClient, NullableICMAPIClient>();
        //Register ICMService for all platforms so to support Searching team in changing incident platform UI
        services.AddICMClientRelatedServices();
        services.AddSingleton<IServiceNowAPIClient, NullableServiceNowAPIClient>();
        services.AddSingleton<IIncidentScanner, NullableIncidentScanner>();
        services.AddSingleton<IAzMonitorAlertService, NullableAzMonitorAlertService>();
        return services;
    }

    public static IServiceCollection AddIncidentRelatedServices(this IServiceCollection services)
    {
        services.AddDefaultIncidentApiClientsAndScanner();

        var serviceProvider = services.BuildServiceProvider();
        var incidentManagementSettings = serviceProvider.GetRequiredService<IncidentManagementSettings>();

        // Register IIncidentHandlerManagementService before filter services that depend on it
        services.AddSingleton<IIncidentHandlerManagementService, IncidentHandlerManagementService>();

        // Register incident indexing configuration validators
        services.AddSingleton<IcmIncidentIndexingConfigurationValidator>();
        services.AddSingleton<PagerDutyIncidentIndexingConfigurationValidator>();
        services.AddSingleton<ServiceNowIncidentIndexingConfigurationValidator>();
        services.AddSingleton<AzMonitorIncidentIndexingConfigurationValidator>();
        services.AddSingleton<IIncidentIndexingConfigurationValidatorFactory, IncidentIndexingConfigurationValidatorFactory>();

        // Register provider-specific incident indexing configuration services
        services.AddSingleton<IIncidentIndexingConfigurationService<IcmIncidentIndexingConfigurationDocument, IcmIncidentIndexingConfigurationPayload>, IcmIncidentIndexingConfigurationService>();
        services.AddSingleton<IIncidentIndexingConfigurationService<PagerDutyIncidentIndexingConfigurationDocument, PagerDutyIncidentIndexingConfigurationPayload>, PagerDutyIncidentIndexingConfigurationService>();
        services.AddSingleton<IIncidentIndexingConfigurationService<ServiceNowIncidentIndexingConfigurationDocument, ServiceNowIncidentIndexingConfigurationPayload>, ServiceNowIncidentIndexingConfigurationService>();
        services.AddSingleton<IIncidentIndexingConfigurationService<AzMonitorIncidentIndexingConfigurationDocument, AzMonitorIncidentIndexingConfigurationPayload>, AzMonitorIncidentIndexingConfigurationService>();
        services.AddSingleton<IIncidentIndexingConfigurationServiceFactory, IncidentIndexingConfigurationServiceFactory>();

        //Overwrite ApiClient and IIncidentScanner
        switch (incidentManagementSettings.Type)
        {
            case IncidentManagementType.AzMonitor:
                services.AddSingleton<IAzMonitorAlertService, AzMonitorAlertService>();
                services.AddSingleton<IIncidentScanner, AzMonitorScanner>();

                services.AddSingleton<IIncidentHandlingService<AzMonitorIncidentFilterDocumentPayload>, AzMonitorIncidentHandlingService>();
                services.AddSingleton<IIncidentManagementService<AzMonitorAlertDocument, AzMonitorIncidentFilterDocumentPayload>, AzMonitorIncidentManagementService>();
                services.AddSingleton<IIncidentFilterManagementService<AzMonitorIncidentFilterDocument, AzMonitorIncidentFilterDocumentPayload>, AzMonitorIncidentFilterManagementService>();
                services.AddSingleton<IIncidentAnalysisService<AzMonitorAlertDocument, AzMonitorIncidentFilterDocument, AzMonitorIncidentFilterDocumentPayload, AlertItem>, AzMonitorIncidentAnalysisService>();
                break;

            case IncidentManagementType.PagerDuty:
                services.AddSingleton<IPagerDutyService, PagerDutyService>();
                services.AddSingleton<IIncidentScanner, PagerDutyScanner>();

                services.AddSingleton<IIncidentHandlingService<PagerDutyIncidentFilterDocumentPayload>, PagerDutyIncidentHandlingService>();
                services.AddSingleton<IIncidentManagementService<PagerDutyIncidentDocument, PagerDutyIncidentFilterDocumentPayload>, PagerDutyIncidentManagementService>();
                services.AddSingleton<IIncidentFilterManagementService<PagerDutyIncidentFilterDocument, PagerDutyIncidentFilterDocumentPayload>, PagerDutyIncidentFilterManagementService>();
                services.AddSingleton<IIncidentAnalysisService<PagerDutyIncidentDocument, PagerDutyIncidentFilterDocument, PagerDutyIncidentFilterDocumentPayload, PagerDutyIncident>, PagerDutyIncidentAnalysisService>();
                break;

            case IncidentManagementType.Icm:
                services.AddICMClientRelatedServices();
                services.AddSingleton<IIncidentScanner, IcmScanner>();

                // Register ICM-specific trigger detection services
                services.AddSingleton<IIncidentEventDetector, IncidentEventDetector>();
                services.AddSingleton<IIncidentThreadLookupService, IncidentThreadLookupService>();

                services.AddSingleton<IIncidentHandlingService<IcmIncidentFilterDocumentPayload>, IcmIncidentHandlingService>();
                services.AddSingleton<IIncidentManagementService<IcmIncidentDocument, IcmIncidentFilterDocumentPayload>, IcmIncidentManagementService>();
                services.AddSingleton<IIncidentFilterManagementService<IcmIncidentFilterDocument, IcmIncidentFilterDocumentPayload>, IcmIncidentFilterManagementService>();
                services.AddSingleton<IIncidentAnalysisService<IcmIncidentDocument, IcmIncidentFilterDocument, IcmIncidentFilterDocumentPayload, ICMIncident>, IcmIncidentAnalysisService>();
                break;

            case IncidentManagementType.ServiceNow:
                services.AddSingleton<IServiceNowAPIClient, ServiceNowAPIClient>();
                services.AddSingleton<IIncidentScanner, ServiceNowScanner>();

                services.AddSingleton<IIncidentHandlingService<ServiceNowIncidentFilterDocumentPayload>, ServiceNowIncidentHandlingService>();
                services.AddSingleton<IIncidentManagementService<ServiceNowIncidentDocument, ServiceNowIncidentFilterDocumentPayload>, ServiceNowIncidentManagementService>();
                services.AddSingleton<IIncidentFilterManagementService<ServiceNowIncidentFilterDocument, ServiceNowIncidentFilterDocumentPayload>, ServiceNowIncidentFilterManagementService>();
                services.AddSingleton<IIncidentAnalysisService<ServiceNowIncidentDocument, ServiceNowIncidentFilterDocument, ServiceNowIncidentFilterDocumentPayload, ServiceNowIncident>, ServiceNowIncidentAnalysisService>();
                break;

            case IncidentManagementType.None:
                services.AddSingleton<IIncidentFilterManagementService<NullableIncidentFilterDocument, IncidentFilterDocumentPayload>, NullableIncidentFilterManagementService>();
                break;

            default:
                break;
        }

        services.AddSingleton<IFirstPartyTenantProvider, FirstPartyTenantProvider>();
        services.AddSingleton<IInstructionGenerationService, InstructionGenerationService>();
        services.AddSingleton<IIncidentStatusMetricsService, IncidentStatusMetricsService>();
        services.AddSingleton<IPublishedToolsService, PublishedToolsService>();

        services.AddSingleton<IIncidentFilterManagementServiceFactory, IncidentFilterManagementServiceFactory>();
        services.AddSingleton<IIncidentManagementServiceFactory, IncidentManagementServiceFactory>();
        services.AddSingleton<IIncidentHandlingServiceFactory, IncidentHandlingServiceFactory>();

        return services;
    }

    private static IServiceCollection AddICMClientRelatedServices(this IServiceCollection services)
    {

        var serviceProvider = services.BuildServiceProvider();
        var incidentManagementSettings = serviceProvider.GetRequiredService<IncidentManagementSettings>();
        var icmAppsettings = incidentManagementSettings.ICMAPI;
        var dataConnectorInstanceSettings = serviceProvider.GetRequiredService<CoreSettings>().DataConnectors;

        bool IsDevelopment = serviceProvider.GetRequiredService<IHostEnvironment>().IsDevelopment();

        // Check if there's an AgentSpace ICM data connector configured, get first one since we only support one for now
        var agentSpaceIcmConnector = dataConnectorInstanceSettings?
            .FirstOrDefault(dc => dc.Source == DataConnectorSource.AgentSpace &&
                                 dc.DataConnectorType.Equals("icm", StringComparison.OrdinalIgnoreCase));

        // Check if there's an Agent source ICM data connector with KeyVaultUri for certificate auth
        var agentIcmConnector = dataConnectorInstanceSettings?
            .FirstOrDefault(dc => dc.Source == DataConnectorSource.Agent &&
                                 dc.DataConnectorType.Equals("icm", StringComparison.OrdinalIgnoreCase) &&
                                 !string.IsNullOrWhiteSpace(dc.KeyVaultUri));


        string agentSpaceEndpoint = serviceProvider.GetRequiredService<AzureSettings>().AgentSpaceProxy?.Endpoint ?? string.Empty;
        string agentSpaceResourceId = agentSpaceIcmConnector?.Identity ?? string.Empty;

        string scope = icmAppsettings.IcmMSIResource;
        string apiEndpoint = icmAppsettings.APIEndpoint;

        //For E2E test, overwrite apiEndpoint with scope and disable Agent Space Proxy
        if (!string.IsNullOrEmpty(incidentManagementSettings.ConnectionUrl))
        {
            apiEndpoint = incidentManagementSettings.ConnectionUrl ?? string.Empty;
            agentSpaceEndpoint = string.Empty;
            scope = incidentManagementSettings.ConnectionName ?? string.Empty;
        }

        var icmAgentSpaceAuthOptions = new ICMAgentSpaceAuthOptions()
        {
            AgentSpaceProxyEndpoint = agentSpaceEndpoint,
            ResourceId = agentSpaceResourceId,
            Scope = $"{icmAppsettings.IcmMSIResource}/.default",
        };

        Func<IServiceProvider, Action<LogLevel, string>> loggerProvider = sp =>
        {
            var logger = sp.GetRequiredService<ILogger<ICMAPIClient>>();

            return (LogLevel logLevel, string message) =>
            {
                switch (logLevel)
                {
                    case LogLevel.Error:
                        logger.LogInternalError($"[ICMSDK]{message}");
                        break;
                    case LogLevel.Warning:
                        logger.LogInternalWarning($"[ICMSDK]{message}");
                        break;
                    case LogLevel.Information:
                    default:
                        logger.LogInternalInformation($"[ICMSDK]{message}");
                        break;
                }
            };
        };

        IHttpClientBuilder httpClientBuilder;

        // if UserToken is not null -> local development, use UserToken/Cert from appsettings
        // else if Agent Space Proxy is configured, use ICMAPIAgentSpaceTokenService
        // else if cert auth is configured, use ICMAPICertAuthService
        // else use Managed Identity
        if (IsDevelopment)
        {
            if (!string.IsNullOrWhiteSpace(icmAppsettings.UserToken))
            {
                httpClientBuilder = services.AddIcmApiClient(options =>
                {
                    options.ApiEndpoint = apiEndpoint;
                    options.TokenProvider = sp =>
                    {
                        return ValueTask.FromResult(new AccessToken(
                            icmAppsettings.UserToken,
                            DateTimeOffset.MaxValue
                        ));
                    };
                    options.LoggerProvider = loggerProvider;
                });
            }
            else if (IcmDataConnectorCertAuthService.IsCertAuthConfigured(agentIcmConnector))
            {
                // Use Data Connector configuration for certificate auth (uses DefaultAzureCredential to load cert from KeyVault)
                var connectorApiEndpoint = !string.IsNullOrWhiteSpace(agentIcmConnector!.DataSource)
                    ? agentIcmConnector.DataSource
                    : apiEndpoint;

                services.AddSingleton(agentIcmConnector);
                services.AddSingleton<IIcmCertAuthService, IcmDataConnectorCertAuthService>();
                httpClientBuilder = services.AddIcmApiClient(options =>
                {
                    options.ApiEndpoint = connectorApiEndpoint;
                    options.CertificateProvider = sp =>
                    {
                        var certAuthService = sp.GetRequiredService<IIcmCertAuthService>();
                        return certAuthService.GetClientCertificate();
                    };
                });
            }
            else if (IcmApiCertAuthService.IsCertAuthConfigured(icmAppsettings))
            {
                services.AddSingleton<IIcmCertAuthService, IcmApiCertAuthService>();
                httpClientBuilder = services.AddIcmApiClient(options =>
                {
                    options.ApiEndpoint = apiEndpoint;
                    options.CertificateProvider = sp =>
                    {
                        var certAuthService = sp.GetRequiredService<IIcmCertAuthService>();
                        return certAuthService.GetClientCertificate();
                    };
                    options.LoggerProvider = loggerProvider;
                });
            }
            else
            {
                // icm not configured for local env
                services.AddSingleton<IICMAPIClient, NullableICMAPIClient>();
                return services;
            }
        }
        else if (IcmApiAgentSpaceTokenAuthService.IsAgentSpaceProxyConfigured(icmAgentSpaceAuthOptions))
        {
            services.AddSingleton(icmAgentSpaceAuthOptions);
            services.AddSingleton<IIcmTokenAuthService, IcmApiAgentSpaceTokenAuthService>();
            httpClientBuilder = services.AddIcmApiClient(options =>
            {
                options.ApiEndpoint = apiEndpoint;
                options.TokenProvider = async sp =>
                {
                    var agentSpaceTokenService = sp.GetRequiredService<IIcmTokenAuthService>();
                    return await agentSpaceTokenService.GetAccessTokenAsync();
                };
                options.LoggerProvider = loggerProvider;
            });
        }
        else if (IcmDataConnectorCertAuthService.IsCertAuthConfigured(agentIcmConnector))
        {
            // Use Data Connector configuration for certificate auth
            // Override apiEndpoint with DataSource from connector if provided
            var connectorApiEndpoint = !string.IsNullOrWhiteSpace(agentIcmConnector!.DataSource)
                ? agentIcmConnector.DataSource
                : apiEndpoint;

            services.AddSingleton(agentIcmConnector);
            services.AddSingleton<IIcmCertAuthService, IcmDataConnectorCertAuthService>();
            httpClientBuilder = services.AddIcmApiClient(options =>
            {
                options.ApiEndpoint = connectorApiEndpoint;
                options.CertificateProvider = sp =>
                {
                    var certAuthService = sp.GetRequiredService<IIcmCertAuthService>();
                    return certAuthService.GetClientCertificate();
                };
            });
        }
        else if (IcmApiCertAuthService.IsCertAuthConfigured(icmAppsettings))
        {
            services.AddSingleton<IIcmCertAuthService, IcmApiCertAuthService>();
            httpClientBuilder = services.AddIcmApiClient(options =>
            {
                options.ApiEndpoint = apiEndpoint;
                options.CertificateProvider = sp =>
                {
                    var certAuthService = sp.GetRequiredService<IIcmCertAuthService>();
                    return certAuthService.GetClientCertificate();
                };
                options.LoggerProvider = loggerProvider;
            });
        }
        else
        {
            var authService = serviceProvider.GetRequiredService<IAuthenticationService>();
            if (string.IsNullOrWhiteSpace(scope))
            {
                throw new InvalidOperationException("ICM API scope is not configured.");
            }
            httpClientBuilder = services.AddIcmApiClient(options =>
            {
                options.ApiEndpoint = apiEndpoint;
                options.TokenProvider = async sp =>
                {
                    var tokenCredential = authService.GetIcmApiCredential();
                    var tokenRequestContext = new TokenRequestContext(new[] { scope });
                    var accessToken = await tokenCredential.GetTokenAsync(tokenRequestContext, CancellationToken.None);
                    return accessToken;
                };
                options.LoggerProvider = loggerProvider;
            });
        }

        services.AddTransient<LoggingHttpMessageHandler>();
        httpClientBuilder.AddHttpMessageHandler<LoggingHttpMessageHandler>();
        services.AddSingleton<IICMAPIClient, ICMAPIClient>();

        return services;
    }
}

public interface IServiceFactory
{
    dynamic GetServiceDynamic();
}

internal static class JsonExtensions
{
    public static T DeserializeJson<T>(this JsonNode jsonNode, JsonSerializerOptions? options = null)
    {
        if (options is null)
        {
            options = new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
        }

        T? res = JsonSerializer.Deserialize<T>(jsonNode, options);
        if (res is null)
        {
            throw new JsonException($"Failed to deserialize JSON node to type {typeof(T).FullName}");
        }
        return res;
    }
}

public abstract class IncidentServiceFactoryBase : IServiceFactory
{
    protected readonly IServiceProvider _serviceProvider;
    protected IncidentManagementType _incidentManagementType;
    public IncidentServiceFactoryBase(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        var incidentManagementSettings = _serviceProvider.GetRequiredService<IOptionsMonitor<IncidentManagementSettings>>();
        _incidentManagementType = incidentManagementSettings.CurrentValue.Type ?? IncidentManagementType.None;
    }
    public abstract dynamic GetServiceDynamic();
}

#region Incident Filter Management Service Factory
public interface IIncidentFilterManagementServiceFactory : IServiceFactory
{
    public IIncidentFilterManagementService<TIncidentFilterDocument, TIncidentFilterDocumentPayload>? GetService<TIncidentFilterDocument, TIncidentFilterDocumentPayload>()
        where TIncidentFilterDocument : TIncidentFilterDocumentPayload, IIncidentFilterDocument
        where TIncidentFilterDocumentPayload : IncidentFilterDocumentPayload;

    public Task<object> SaveIncidentFilter(JsonNode incidentFilterDocument);
}

public class IncidentFilterManagementServiceFactory : IncidentServiceFactoryBase, IIncidentFilterManagementServiceFactory
{
    public IncidentFilterManagementServiceFactory(IServiceProvider serviceProvider) : base(serviceProvider) { }

    public IIncidentFilterManagementService<TIncidentFilterDocument, TIncidentFilterDocumentPayload>? GetService<TIncidentFilterDocument, TIncidentFilterDocumentPayload>()
        where TIncidentFilterDocument : TIncidentFilterDocumentPayload, IIncidentFilterDocument
        where TIncidentFilterDocumentPayload : IncidentFilterDocumentPayload
    {
        try
        {
            return _serviceProvider.GetRequiredService<IIncidentFilterManagementService<TIncidentFilterDocument, TIncidentFilterDocumentPayload>>();
        }
        catch (Exception)
        {
            return null;
        }
    }

    public override dynamic GetServiceDynamic()
    {
        return _incidentManagementType switch
        {
            IncidentManagementType.None => _serviceProvider.GetRequiredService<IIncidentFilterManagementService<NullableIncidentFilterDocument, IncidentFilterDocumentPayload>>(),
            IncidentManagementType.AzMonitor => _serviceProvider.GetRequiredService<IIncidentFilterManagementService<AzMonitorIncidentFilterDocument, AzMonitorIncidentFilterDocumentPayload>>(),
            IncidentManagementType.PagerDuty => _serviceProvider.GetRequiredService<IIncidentFilterManagementService<PagerDutyIncidentFilterDocument, PagerDutyIncidentFilterDocumentPayload>>(),
            IncidentManagementType.Icm => _serviceProvider.GetRequiredService<IIncidentFilterManagementService<IcmIncidentFilterDocument, IcmIncidentFilterDocumentPayload>>(),
            IncidentManagementType.ServiceNow => _serviceProvider.GetRequiredService<IIncidentFilterManagementService<ServiceNowIncidentFilterDocument, ServiceNowIncidentFilterDocumentPayload>>(),
            _ => throw new NotSupportedException($"Unsupported incident management type: {_incidentManagementType}")
        };
    }

    public async Task<object> SaveIncidentFilter(JsonNode incidentFilterDocument)
    {
        return _incidentManagementType switch
        {
            IncidentManagementType.None => throw new InvalidOperationException("Cannot save incident filter when incident management type is None"),
            IncidentManagementType.AzMonitor => await _serviceProvider.GetRequiredService<IIncidentFilterManagementService<AzMonitorIncidentFilterDocument, AzMonitorIncidentFilterDocumentPayload>>().SaveIncidentFilter(incidentFilterDocument.DeserializeJson<AzMonitorIncidentFilterDocument>()),
            IncidentManagementType.PagerDuty => await _serviceProvider.GetRequiredService<IIncidentFilterManagementService<PagerDutyIncidentFilterDocument, PagerDutyIncidentFilterDocumentPayload>>().SaveIncidentFilter(incidentFilterDocument.DeserializeJson<PagerDutyIncidentFilterDocument>()),
            IncidentManagementType.Icm => await _serviceProvider.GetRequiredService<IIncidentFilterManagementService<IcmIncidentFilterDocument, IcmIncidentFilterDocumentPayload>>().SaveIncidentFilter(incidentFilterDocument.DeserializeJson<IcmIncidentFilterDocument>()),
            IncidentManagementType.ServiceNow => await _serviceProvider.GetRequiredService<IIncidentFilterManagementService<ServiceNowIncidentFilterDocument, ServiceNowIncidentFilterDocumentPayload>>().SaveIncidentFilter(incidentFilterDocument.DeserializeJson<ServiceNowIncidentFilterDocument>()),
            _ => throw new NotSupportedException($"Unsupported incident management type: {_incidentManagementType}")
        };
    }
}
#endregion

#region IncidentManagementServiceFactory
public interface IIncidentManagementServiceFactory : IServiceFactory
{
    public IIncidentManagementService<TIncidentDocument, TIncidentFilterDocumentPayload>? GetService<TIncidentDocument, TIncidentFilterDocumentPayload>() where TIncidentDocument : IIncidentDocument where TIncidentFilterDocumentPayload : IncidentFilterDocumentPayload;
    public Task<object?> SaveDocument(JsonNode? incidentDocument);

    public Task<object> QueryIncidents(JsonNode request);
}

public class IncidentManagementServiceFactory : IncidentServiceFactoryBase, IIncidentManagementServiceFactory
{
    public IncidentManagementServiceFactory(IServiceProvider serviceProvider) : base(serviceProvider) { }
    public IIncidentManagementService<TIncidentDocument, TIncidentFilterDocumentPayload>? GetService<TIncidentDocument, TIncidentFilterDocumentPayload>() where TIncidentDocument : IIncidentDocument where TIncidentFilterDocumentPayload : IncidentFilterDocumentPayload
    {
        try
        {
            return _serviceProvider.GetRequiredService<IIncidentManagementService<TIncidentDocument, TIncidentFilterDocumentPayload>>();
        }
        catch (Exception)
        {
            return null;
        }
    }
    public override dynamic GetServiceDynamic()
    {
        return _incidentManagementType switch
        {
            IncidentManagementType.None => null!,
            IncidentManagementType.AzMonitor => _serviceProvider.GetRequiredService<IIncidentManagementService<AzMonitorAlertDocument, AzMonitorIncidentFilterDocumentPayload>>(),
            IncidentManagementType.PagerDuty => _serviceProvider.GetRequiredService<IIncidentManagementService<PagerDutyIncidentDocument, PagerDutyIncidentFilterDocumentPayload>>(),
            IncidentManagementType.Icm => _serviceProvider.GetRequiredService<IIncidentManagementService<IcmIncidentDocument, IcmIncidentFilterDocumentPayload>>(),
            IncidentManagementType.ServiceNow => _serviceProvider.GetRequiredService<IIncidentManagementService<ServiceNowIncidentDocument, ServiceNowIncidentFilterDocumentPayload>>(),
            _ => throw new NotSupportedException($"Unsupported incident management type: {_incidentManagementType}")
        };
    }

    public async Task<object> QueryIncidents(JsonNode request)
    {
        return _incidentManagementType switch
        {
            IncidentManagementType.None => throw new InvalidOperationException("Cannot query incidents when incident management type is None"),
            IncidentManagementType.PagerDuty => await _serviceProvider.GetRequiredService<IIncidentManagementService<PagerDutyIncidentDocument, PagerDutyIncidentFilterDocumentPayload>>().QueryIncidents(request.DeserializeJson<IncidentQueryRequest<PagerDutyIncidentFilterDocumentPayload>>()),
            IncidentManagementType.Icm => await _serviceProvider.GetRequiredService<IIncidentManagementService<IcmIncidentDocument, IcmIncidentFilterDocumentPayload>>().QueryIncidents(request.DeserializeJson<IncidentQueryRequest<IcmIncidentFilterDocumentPayload>>()),
            IncidentManagementType.ServiceNow => await _serviceProvider.GetRequiredService<IIncidentManagementService<ServiceNowIncidentDocument, ServiceNowIncidentFilterDocumentPayload>>().QueryIncidents(request.DeserializeJson<IncidentQueryRequest<ServiceNowIncidentFilterDocumentPayload>>()),
            IncidentManagementType.AzMonitor => await _serviceProvider.GetRequiredService<IIncidentManagementService<AzMonitorAlertDocument, AzMonitorIncidentFilterDocumentPayload>>().QueryIncidents(request.DeserializeJson<IncidentQueryRequest<AzMonitorIncidentFilterDocumentPayload>>()),
            _ => throw new NotSupportedException($"Unsupported incident management type: {_incidentManagementType}")
        };
    }

    public async Task<object?> SaveDocument(JsonNode? incidentDocument)
    {
        if (incidentDocument == null)
        {
            throw new ArgumentNullException(nameof(incidentDocument), "Incident document cannot be null.");
        }
        return _incidentManagementType switch
        {
            IncidentManagementType.None => throw new InvalidOperationException("Cannot save document when incident management type is None"),
            IncidentManagementType.AzMonitor => await _serviceProvider.GetRequiredService<IIncidentManagementService<AzMonitorAlertDocument, AzMonitorIncidentFilterDocumentPayload>>().SaveDocument(JsonSerializer.Deserialize<AzMonitorAlertDocument>(incidentDocument)),
            IncidentManagementType.PagerDuty => await _serviceProvider.GetRequiredService<IIncidentManagementService<PagerDutyIncidentDocument, PagerDutyIncidentFilterDocumentPayload>>().SaveDocument(JsonSerializer.Deserialize<PagerDutyIncidentDocument>(incidentDocument)),
            IncidentManagementType.Icm => await _serviceProvider.GetRequiredService<IIncidentManagementService<IcmIncidentDocument, IcmIncidentFilterDocumentPayload>>().SaveDocument(JsonSerializer.Deserialize<IcmIncidentDocument>(incidentDocument)),
            IncidentManagementType.ServiceNow => await _serviceProvider.GetRequiredService<IIncidentManagementService<ServiceNowIncidentDocument, ServiceNowIncidentFilterDocumentPayload>>().SaveDocument(JsonSerializer.Deserialize<ServiceNowIncidentDocument>(incidentDocument)),
            _ => throw new NotSupportedException($"Unsupported incident management type: {_incidentManagementType}")
        };
    }
}
#endregion

#region IncidentHandlingServiceFactory
public interface IIncidentHandlingServiceFactory : IServiceFactory
{
    public IIncidentHandlingService<T>? GetService<T>() where T : IncidentFilterDocumentPayload;
    public Task<IncidentHandlingResponseModel> HandleIncidentAsync(JsonNode? incidentDocument);
    public Task<List<IncidentHandlingResponseModel>> HandleIncidentsAsync(IEnumerable<JsonNode> request);
}

public class IncidentHandlingServiceFactory : IncidentServiceFactoryBase, IIncidentHandlingServiceFactory
{
    public IncidentHandlingServiceFactory(IServiceProvider serviceProvider) : base(serviceProvider) { }
    public IIncidentHandlingService<T>? GetService<T>() where T : IncidentFilterDocumentPayload
    {
        try
        {
            return _serviceProvider.GetRequiredService<IIncidentHandlingService<T>>();
        }
        catch (Exception)
        {
            return null;
        }
    }
    public override dynamic GetServiceDynamic()
    {
        return _incidentManagementType switch
        {
            IncidentManagementType.None => null!,
            IncidentManagementType.AzMonitor => _serviceProvider.GetRequiredService<IIncidentHandlingService<AzMonitorIncidentFilterDocumentPayload>>(),
            IncidentManagementType.PagerDuty => _serviceProvider.GetRequiredService<IIncidentHandlingService<PagerDutyIncidentFilterDocumentPayload>>(),
            IncidentManagementType.Icm => _serviceProvider.GetRequiredService<IIncidentHandlingService<IcmIncidentFilterDocumentPayload>>(),
            IncidentManagementType.ServiceNow => _serviceProvider.GetRequiredService<IIncidentHandlingService<ServiceNowIncidentFilterDocumentPayload>>(),
            _ => throw new NotSupportedException($"Unsupported incident management type: {_incidentManagementType}")
        };
    }
    public async Task<IncidentHandlingResponseModel> HandleIncidentAsync(JsonNode? incidentDocument)
    {
        return _incidentManagementType switch
        {
            IncidentManagementType.None => throw new InvalidOperationException("Cannot handle incident when incident management type is None"),
            IncidentManagementType.AzMonitor => await _serviceProvider.GetRequiredService<IIncidentHandlingService<AzMonitorIncidentFilterDocumentPayload>>().HandleIncidentAsync(incidentDocument?.DeserializeJson<IncidentHandlingRequestModel<AzMonitorIncidentFilterDocumentPayload>>()),
            IncidentManagementType.PagerDuty => await _serviceProvider.GetRequiredService<IIncidentHandlingService<PagerDutyIncidentFilterDocumentPayload>>().HandleIncidentAsync(incidentDocument?.DeserializeJson<IncidentHandlingRequestModel<PagerDutyIncidentFilterDocumentPayload>>()),
            IncidentManagementType.Icm => await _serviceProvider.GetRequiredService<IIncidentHandlingService<IcmIncidentFilterDocumentPayload>>().HandleIncidentAsync(incidentDocument?.DeserializeJson<IncidentHandlingRequestModel<IcmIncidentFilterDocumentPayload>>()),
            IncidentManagementType.ServiceNow => await _serviceProvider.GetRequiredService<IIncidentHandlingService<ServiceNowIncidentFilterDocumentPayload>>().HandleIncidentAsync(incidentDocument?.DeserializeJson<IncidentHandlingRequestModel<ServiceNowIncidentFilterDocumentPayload>>()),
            _ => throw new NotSupportedException($"Unsupported incident management type: {_incidentManagementType}")
        };
    }

    public async Task<List<IncidentHandlingResponseModel>> HandleIncidentsAsync(IEnumerable<JsonNode> request)
    {
        return _incidentManagementType switch
        {
            IncidentManagementType.None => throw new InvalidOperationException("Cannot handle incidents when incident management type is None"),
            IncidentManagementType.AzMonitor => await _serviceProvider.GetRequiredService<IIncidentHandlingService<AzMonitorIncidentFilterDocumentPayload>>().HandleIncidentsAsync(request.Select(node => node.DeserializeJson<IncidentHandlingRequestModel<AzMonitorIncidentFilterDocumentPayload>>())),
            IncidentManagementType.PagerDuty => await _serviceProvider.GetRequiredService<IIncidentHandlingService<PagerDutyIncidentFilterDocumentPayload>>().HandleIncidentsAsync(request.Select(node => node.DeserializeJson<IncidentHandlingRequestModel<PagerDutyIncidentFilterDocumentPayload>>())),
            IncidentManagementType.Icm => await _serviceProvider.GetRequiredService<IIncidentHandlingService<IcmIncidentFilterDocumentPayload>>().HandleIncidentsAsync(request.Select(node => node.DeserializeJson<IncidentHandlingRequestModel<IcmIncidentFilterDocumentPayload>>())),
            IncidentManagementType.ServiceNow => await _serviceProvider.GetRequiredService<IIncidentHandlingService<ServiceNowIncidentFilterDocumentPayload>>().HandleIncidentsAsync(request.Select(node => node.DeserializeJson<IncidentHandlingRequestModel<ServiceNowIncidentFilterDocumentPayload>>())),
            _ => throw new NotSupportedException($"Unsupported incident management type: {_incidentManagementType}")
        };
    }
}
#endregion
