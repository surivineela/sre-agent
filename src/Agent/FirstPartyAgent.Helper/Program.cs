
using System.Text.Json;
using Agent.Core.Configuration;
using Agent.Runtime;
using FirstPartyAgent.Core.Configuration;
using FirstPartyAgent.Core.Services;
using FirstPartyAgent.Helper.Services;
using Microsoft.Extensions.Options;

namespace FirstPartyAgent.Helper;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.LoadLocalAppSettings();

        var config = builder.Configuration;
        var configBuilder = config.SetBasePath(builder.Environment.ContentRootPath)
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true); //load base settings;
                    

        if (config.GetValue("ASPNETCORE_ENVIRONMENT", "Production").Equals("Development", StringComparison.CurrentCultureIgnoreCase))
        {
            builder.Logging.ClearProviders();
            builder.Logging.SetMinimumLevel(LogLevel.Trace);
            builder.Logging.AddConsole();
            builder.Logging.AddDebug();

            configBuilder.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true); //load development settings
        }


        configBuilder.AddEnvironmentVariables();

        //Microsoft.IdentityModel.Logging.LogHelper.WriteEntry

        // builder.Services.RegisterServiceDependencies(builder.Environment);
        bool isDevelopment = builder.Environment.IsDevelopment();
        builder.Services.AddBearerAuthFlow(config, isDevelopment);

        builder.Services.AddOptionsWithValidateOnStart<AzureSettings>()
            .BindConfiguration("AppSettings:Core:Azure")
            .ValidateDataAnnotations();

        builder.Services.AddOptionsWithValidateOnStart<FirstPartyAgentExternalSettings>()
            .BindConfiguration("AppSettings:Core:External")
            .ValidateDataAnnotations();

        builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<FirstPartyAgentExternalSettings>>().Value.OneBranchApprovalService);
        builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<FirstPartyAgentExternalSettings>>().Value.Storage);


        builder.Services.AddSingleton<ICosmosDBService, CosmosDBService>();
        builder.Services.AddSingleton<IStorageService, StorageService>();
        builder.Services.AddSingleton<IApprovalAuditEventLogger, AppInsightsApprovalAuditEventLogger>();

        builder.Services.AddControllers();

        builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 100 * 1024 * 1024);

        builder.Services.AddApplicationInsightsTelemetry(builder.Configuration);

        var app = builder.Build();

        app.UseHttpsRedirection();
        app.UseMiddleware<ExceptionHandlingMiddleware>();

        // Map controllers for API endpoints
        app.MapControllers();


        app.Run();

    }

    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        public ExceptionHandlingMiddleware(RequestDelegate next) => _next = next;

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                context.Response.ContentType = "application/json";
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { ex.Message, ex.StackTrace }));
            }
        }
    }
}
