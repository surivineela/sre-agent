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
                services.AddSingleton<IIncidentManagementService<PagerDutyIncidentDocument>, PagerDutyIncidentManagementService>();
                services.AddSingleton<IIncidentFilterManagementService<PagerDutyIncidentFilterDocument>, PagerDutyIncidentFilterManagementService>();
                break;

            case IncidentManagementType.Icm:
                services.AddSingleton<LoggingHttpMessageHandler>();
                services.AddSingleton<IICMAPIClient, ICMAPIClient>();
                services.AddSingleton<IIncidentScanner, IcmScanner>();

                var logger = serviceProvider.GetRequiredService<ILogger<ICMAPITokenService>>();
                var actionSettings = serviceProvider.GetRequiredService<ActionSettings>();
                ICMAPITokenService.Instance.Initialize(actionSettings, incidentManagementSettings.ICMAPI, logger);

                services.AddSingleton<IIncidentHandlingService<IcmIncidentFilterDocumentPayload>, IcmIncidentHandlingService>();
                services.AddSingleton<IIncidentManagementService<IcmIncidentDocument>, IcmIncidentManagementService>();
                services.AddSingleton<IIncidentFilterManagementService<IcmIncidentFilterDocument>, IcmIncidentFilterManagementService>();
                break;

            case IncidentManagementType.ServiceNow:
                services.AddSingleton<IServiceNowAPIClient, ServiceNowAPIClient>();
                services.AddSingleton<IIncidentScanner, ServiceNowScanner>();

                services.AddSingleton<IIncidentHandlingService<ServiceNowIncidentFilterDocumentPayload>, ServiceNowIncidentHandlingService>();
                services.AddSingleton<IIncidentManagementService<ServiceNowIncidentDocument>, ServiceNowIncidentManagementService>();
                services.AddSingleton<IIncidentFilterManagementService<ServiceNowIncidentFilterDocument>, ServiceNowIncidentFilterManagementService>();
                break;

            default:
                break;
        }

        services.AddSingleton<IIncidentHandlerManagementService, IncidentHandlerManagementService>();
        services.AddSingleton<IInstructionGenerationService, InstructionGenerationService>();

        services.AddSingleton<IIncidentFilterManagementServiceFactory, IncidentFilterManagementServiceFactory>();
        services.AddSingleton<IIncidentManagementServiceFactory, IncidentManagementServiceFactory>();
        services.AddSingleton<IIncidentHandlingServiceFactory, IncidentHandlingServiceFactory>();

        return services;
    }
}

public interface IServiceFactory
{
    dynamic GetServiceDynamic();
}

internal static class JsonExtensions
{
    public static T? DeserializeJson<T>(this JsonNode jsonNode, JsonSerializerOptions? options = null) {
        if(jsonNode is null)
        {
            return default(T);
        }
        if(options is null)
        {
            options = new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
        }

        return JsonSerializer.Deserialize<T>(jsonNode, options);
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
public interface IIncidentFilterManagementServiceFactory : IServiceFactory {
    public IIncidentFilterManagementService<T>? GetService<T>() where T : IncidentFilterDocument;

    public Task<object> SaveIncidentFilter(JsonNode incidentFilterDocument);
}

public class IncidentFilterManagementServiceFactory : IncidentServiceFactoryBase, IIncidentFilterManagementServiceFactory
{
    public IncidentFilterManagementServiceFactory(IServiceProvider serviceProvider) : base(serviceProvider) { }

    public IIncidentFilterManagementService<T>? GetService<T>() where T : IncidentFilterDocument
    {
        try { 
            return _serviceProvider.GetRequiredService<IIncidentFilterManagementService<T>>();
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
            IncidentManagementType.PagerDuty => _serviceProvider.GetRequiredService<IIncidentFilterManagementService<PagerDutyIncidentFilterDocument>>(),
            IncidentManagementType.Icm => _serviceProvider.GetRequiredService<IIncidentFilterManagementService<IcmIncidentFilterDocument>>(),
            IncidentManagementType.ServiceNow => _serviceProvider.GetRequiredService<IIncidentFilterManagementService<ServiceNowIncidentFilterDocument>>(),
            _ => throw new NotSupportedException($"Unsupported incident management type: {_incidentManagementType}")
        };
    }

    public async Task<object> SaveIncidentFilter(JsonNode incidentFilterDocument)
    {
        return _incidentManagementType switch
        {
            IncidentManagementType.PagerDuty => await _serviceProvider.GetRequiredService<IIncidentFilterManagementService<PagerDutyIncidentFilterDocument>>().SaveIncidentFilter(incidentFilterDocument.DeserializeJson<PagerDutyIncidentFilterDocument>()),
            IncidentManagementType.Icm => await _serviceProvider.GetRequiredService<IIncidentFilterManagementService<IcmIncidentFilterDocument>>().SaveIncidentFilter(incidentFilterDocument.DeserializeJson<IcmIncidentFilterDocument>()),
            IncidentManagementType.ServiceNow => await _serviceProvider.GetRequiredService<IIncidentFilterManagementService<ServiceNowIncidentFilterDocument>>().SaveIncidentFilter(incidentFilterDocument.DeserializeJson<ServiceNowIncidentFilterDocument>()),
            _ => throw new NotSupportedException($"Unsupported incident management type: {_incidentManagementType}")
        };
    }
}
#endregion

#region IncidentManagementServiceFactory
public interface IIncidentManagementServiceFactory : IServiceFactory
{
    public IIncidentManagementService<T>? GetService<T>() where T : IIncidentDocument;
    public Task<object?> SaveDocument(JsonNode? incidentDocument);
}

public class IncidentManagementServiceFactory : IncidentServiceFactoryBase, IIncidentManagementServiceFactory
{
    public IncidentManagementServiceFactory(IServiceProvider serviceProvider) : base(serviceProvider) { }
    public IIncidentManagementService<T>? GetService<T>() where T : IIncidentDocument
    {
        try
        {
            return _serviceProvider.GetRequiredService<IIncidentManagementService<T>>();
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
            IncidentManagementType.PagerDuty => _serviceProvider.GetRequiredService<IIncidentManagementService<PagerDutyIncidentDocument>>(),
            IncidentManagementType.Icm => _serviceProvider.GetRequiredService<IIncidentManagementService<IcmIncidentDocument>>(),
            IncidentManagementType.ServiceNow => _serviceProvider.GetRequiredService<IIncidentManagementService<ServiceNowIncidentDocument>>(),
            _ => throw new NotSupportedException($"Unsupported incident management type: {_incidentManagementType}")
        };
    }
    public async Task<object?> SaveDocument(JsonNode? incidentDocument)
    {
        if(incidentDocument == null)
        {
            throw new ArgumentNullException(nameof(incidentDocument), "Incident document cannot be null.");
        }
        return _incidentManagementType switch
        {
            IncidentManagementType.PagerDuty => await _serviceProvider.GetRequiredService<IIncidentManagementService<PagerDutyIncidentDocument>>().SaveDocument(JsonSerializer.Deserialize<PagerDutyIncidentDocument>(incidentDocument)),
            IncidentManagementType.Icm => await _serviceProvider.GetRequiredService<IIncidentManagementService<IcmIncidentDocument>>().SaveDocument(JsonSerializer.Deserialize<IcmIncidentDocument>(incidentDocument)),
            IncidentManagementType.ServiceNow => await _serviceProvider.GetRequiredService<IIncidentManagementService<ServiceNowIncidentDocument>>().SaveDocument(JsonSerializer.Deserialize<ServiceNowIncidentDocument>(incidentDocument)),
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
