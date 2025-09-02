using System.Text.Json;
using System.Text.Json.Nodes;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Services;
using Agent.Core.Services.TokenService;
using Agent.Data.DataModels;
using Agent.Graph.Interfaces;
using Agent.Graph.Services;
using Agent.Runtime.Interfaces;
using Agent.Runtime.SubAgents.IcmScanner;
using Agent.Runtime.SubAgents.PagerDutyAgent;
using Agent.Runtime.SubAgents.ServiceNowScanner;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
namespace Agent.Runtime.Services;

public static class IncidentServiceCollectionExtensions
{
    private static IServiceCollection AddDefaultIncidentApiClientsAndScanner(this IServiceCollection services)
    {
        services.AddSingleton<IPagerDutyService, NullablePagerDutyService>();
        services.AddSingleton<IICMAPIClient, NullableICMAPIClient>();
        services.AddSingleton<IServiceNowAPIClient, NullableServiceNowAPIClient>();
        services.AddSingleton<IIncidentScanner, NullableIncidentScanner>();
        return services;
    }

    public static IServiceCollection AddIncidentRelatedServices(this IServiceCollection services)
    {
        services.AddDefaultIncidentApiClientsAndScanner();

        var serviceProvider = services.BuildServiceProvider();
        var incidentManagementSettings = serviceProvider.GetRequiredService<IncidentManagementSettings>();
        //Overwrite ApiClient and IIncidentScanner
        switch (incidentManagementSettings.Type)
        {
            case IncidentManagementType.PagerDuty:
                services.AddSingleton<IPagerDutyService, PagerDutyService>();
                services.AddSingleton<IIncidentScanner, PagerDutyScanner>();

                services.AddSingleton<IIncidentHandlingService<PagerDutyIncidentFilterDocumentPayload>, PagerDutyIncidentHandlingService>();
                services.AddSingleton<IIncidentManagementService<PagerDutyIncidentDocument, PagerDutyIncidentFilterDocumentPayload>, PagerDutyIncidentManagementService>();
                services.AddSingleton<IIncidentFilterManagementService<PagerDutyIncidentFilterDocument, PagerDutyIncidentFilterDocumentPayload>, PagerDutyIncidentFilterManagementService>();
                break;

            case IncidentManagementType.Icm:
                services.AddSingleton<LoggingHttpMessageHandler>();
                services.AddSingleton<IICMAPIClient, ICMAPIClient>();
                services.AddSingleton<IIncidentScanner, IcmScanner>();

                var logger = serviceProvider.GetRequiredService<ILogger<ICMAPITokenService>>();
                var actionSettings = serviceProvider.GetRequiredService<ActionSettings>();
                var authService = serviceProvider.GetRequiredService<IAuthenticationService>();
                ICMAPITokenService.Instance.Initialize(authService, actionSettings, incidentManagementSettings.ICMAPI, logger);

                services.AddSingleton<IIncidentHandlingService<IcmIncidentFilterDocumentPayload>, IcmIncidentHandlingService>();
                services.AddSingleton<IIncidentManagementService<IcmIncidentDocument, IcmIncidentFilterDocumentPayload>, IcmIncidentManagementService>();
                services.AddSingleton<IIncidentFilterManagementService<IcmIncidentFilterDocument, IcmIncidentFilterDocumentPayload>, IcmIncidentFilterManagementService>();
                break;

            case IncidentManagementType.ServiceNow:
                services.AddSingleton<IServiceNowAPIClient, ServiceNowAPIClient>();
                services.AddSingleton<IIncidentScanner, ServiceNowScanner>();

                services.AddSingleton<IIncidentHandlingService<ServiceNowIncidentFilterDocumentPayload>, ServiceNowIncidentHandlingService>();
                services.AddSingleton<IIncidentManagementService<ServiceNowIncidentDocument, ServiceNowIncidentFilterDocumentPayload>, ServiceNowIncidentManagementService>();
                services.AddSingleton<IIncidentFilterManagementService<ServiceNowIncidentFilterDocument, ServiceNowIncidentFilterDocumentPayload>, ServiceNowIncidentFilterManagementService>();
                break;

            default:
                break;
        }

        services.AddSingleton<IIncidentHandlerManagementService, IncidentHandlerManagementService>();
        services.AddSingleton<IInstructionGenerationService, InstructionGenerationService>();
        services.AddSingleton<IIncidentStatusMetricsService, IncidentStatusMetricsService>();

        services.AddSingleton<IIncidentFilterManagementServiceFactory, IncidentFilterManagementServiceFactory>();
        services.AddSingleton<IIncidentManagementServiceFactory, IncidentManagementServiceFactory>();
        services.AddSingleton<IIncidentHandlingServiceFactory, IncidentHandlingServiceFactory>();
        services.AddSingleton<IIncidentAnalysisService, IncidentAnalysisService>(); // To Do: allow for the handling of Az Monitor


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
        var incidentManagementSettings = _serviceProvider.GetRequiredService<IncidentManagementSettings>();
        _incidentManagementType = incidentManagementSettings.Type ?? throw new ArgumentNullException(nameof(incidentManagementSettings.Type), "Incident management type must be specified.");
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
            IncidentManagementType.PagerDuty => await _serviceProvider.GetRequiredService<IIncidentManagementService<PagerDutyIncidentDocument, PagerDutyIncidentFilterDocumentPayload>>().QueryIncidents(request.DeserializeJson<IncidentQueryRequest<PagerDutyIncidentFilterDocumentPayload>>()),
            IncidentManagementType.Icm => await _serviceProvider.GetRequiredService<IIncidentManagementService<IcmIncidentDocument, IcmIncidentFilterDocumentPayload>>().QueryIncidents(request.DeserializeJson<IncidentQueryRequest<IcmIncidentFilterDocumentPayload>>()),
            IncidentManagementType.ServiceNow => await _serviceProvider.GetRequiredService<IIncidentManagementService<ServiceNowIncidentDocument, ServiceNowIncidentFilterDocumentPayload>>().QueryIncidents(request.DeserializeJson<IncidentQueryRequest<ServiceNowIncidentFilterDocumentPayload>>()),
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
            IncidentManagementType.PagerDuty => await _serviceProvider.GetRequiredService<IIncidentHandlingService<PagerDutyIncidentFilterDocumentPayload>>().HandleIncidentAsync(incidentDocument?.DeserializeJson<IncidentHandlingRequestModel<PagerDutyIncidentFilterDocumentPayload>>()),
            IncidentManagementType.Icm => await _serviceProvider.GetRequiredService<IIncidentHandlingService<IcmIncidentFilterDocumentPayload>>().HandleIncidentAsync(incidentDocument?.DeserializeJson<IncidentHandlingRequestModel<IcmIncidentFilterDocumentPayload>>()),
            IncidentManagementType.ServiceNow => await _serviceProvider.GetRequiredService<IIncidentHandlingService<ServiceNowIncidentFilterDocumentPayload>>().HandleIncidentAsync(incidentDocument?.DeserializeJson<IncidentHandlingRequestModel<ServiceNowIncidentFilterDocumentPayload>>()),
            _ => throw new NotSupportedException($"Unsupported incident management type: {_incidentManagementType}")
        };
    }
}
#endregion
