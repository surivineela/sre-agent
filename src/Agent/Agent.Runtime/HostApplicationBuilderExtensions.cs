// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MiniValidation;

namespace Agent.Runtime
{
    public static class HostApplicationBuilderExtensions
    {
        private static bool _localConfigLoaded = false;

        public static void LoadLocalAppSettings(this IHostApplicationBuilder builder, bool isDevelopment = true)
        {
            builder.Configuration.SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true); // load base settings

            if (isDevelopment)
            {
                builder.Configuration.AddJsonFile("appsettings.development.json", optional: true, reloadOnChange: true); // load local dev settings one more time to override Azure App Configuration
            }
            else
            {
                var envName = builder.Environment.EnvironmentName.ToLower();
                builder.Configuration.AddJsonFile($"appsettings.{envName}.json", optional: true, reloadOnChange: true);
            }

            builder.Configuration.AddEnvironmentVariables();

            _localConfigLoaded = true;
        }

        public static void LoadAppSettings(this IHostApplicationBuilder builder, bool isDevelopment = true)
        {
            if (!_localConfigLoaded)
            {
                builder.LoadLocalAppSettings(isDevelopment);
            }

            if (isDevelopment)
            {
                AddFromAppConfig(builder);
                builder.Configuration.AddJsonFile("appsettings.development.json", optional: true, reloadOnChange: true); // load local dev settings one more time to override Azure App Configuration
            }
            builder.Configuration.AddEnvironmentVariables();
        }

        private static void AddFromAppConfig(IHostApplicationBuilder builder)
        {
            string? envPrefix = builder.Configuration.GetValue<string>("AppSettings:EnvPrefix");
            string? ManagedIdentityClientId = builder.Configuration.GetValue<string>("AppSettings:ManagedIdentityClientId");

            if (string.IsNullOrEmpty(envPrefix))
            {
                // Log warning instead of throwing exception as AppConfig should not be mandatory for local dev especially for 1P scenarios
                Console.WriteLine("WARNING: AppSettings:EnvPrefix not set. Azure App Configuration will not be loaded. For more info, check readme.");
                return;
            }

            string endpoint = $"https://{envPrefix}-appconfig.azconfig.io";
            var credOptions = new DefaultAzureCredentialOptions()
            {
                ExcludeInteractiveBrowserCredential = !builder.Environment.IsDevelopment()
            };
            if (!string.IsNullOrEmpty(ManagedIdentityClientId))
            {
                credOptions.ManagedIdentityClientId = ManagedIdentityClientId;
            }
#pragma warning disable CUSTOM003 // App config is only used in local development
            DefaultAzureCredential cred = new DefaultAzureCredential(credOptions); // CodeQL [SM05137] This is not used in production code and only used in local development.
#pragma warning restore CUSTOM003

            builder.Configuration.AddAzureAppConfiguration(options =>
            {
                options.Connect(new Uri(endpoint), cred);
                options.ConfigureKeyVault(options =>
                {
                    options.SetCredential(cred);
                });
            });
        }

        public static void RegisterAppSettingsNoValidation<TAppSettings>(this IHostApplicationBuilder builder)
            where TAppSettings : AppSettings
        {
            builder.Services.AddOptionsWithValidateOnStart<TAppSettings>()
                .BindConfiguration("AppSettings");

            builder.Services.AddSingleton(sp =>
            {
                return sp.GetRequiredService<IOptions<TAppSettings>>().Value;
            });
            builder.Services.RegisterInnerAppSettings<TAppSettings>(builder.Configuration);
        }

        public static void ValidateAndRegisterAppSettings<TAppSettings>(this IHostApplicationBuilder builder)
            where TAppSettings : AppSettings
        {
            builder.Services.AddOptionsWithValidateOnStart<TAppSettings>()
                .BindConfiguration("AppSettings")
                .ValidateDataAnnotations();

            builder.Services.AddSingleton(sp =>
            {
                var settings = sp.GetRequiredService<IOptions<TAppSettings>>().Value;
                if (!MiniValidator.TryValidate(settings, out var validationErrors))
                {
                    List<string> stringErrors = [];
                    foreach ((string member, string[] memberErrors) in validationErrors)
                    {
                        var memberError = string.Join(", ", memberErrors);
                        stringErrors.Add(member + ": " + memberError);
                    }
                    throw new Exception(
                        @$"AppSettings validation failed: {string.Join("\n", stringErrors)}
If you have not already, try running the private environment deployment script again in case any settings have been added.
Otherwise, there may be required settings which are not auto-populated by the private deployment script."
                    );
                }

                return settings;
            });
            builder.Services.RegisterInnerAppSettings<TAppSettings>(builder.Configuration);
        }

        public static void RegisterInnerAppSettings<TAppSettings>(this IServiceCollection sc, IConfiguration configuration)
            where TAppSettings : AppSettings
        {
            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core);
            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core.Azure);
            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core.Azure.Crawler);
            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core.Azure.Action);
            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core.Azure.CosmosDB);
            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core.Azure.CosmosDB.Graph);
            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core.Azure.CosmosDB.Docs);
            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core.Azure.OpenAI);
            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core.Azure.Federation);
            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core.Azure.Kusto);
            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core.Azure.Indexing);
            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core.Azure.SessionPool);
            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core.Azure.AgentSpaceProxy);

            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core.Experimental);

            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core.External);
            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core.External.GitHub);
            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core.External.MCP);

            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core.External.TeamsBot);
            ConvertSettingsForTeamsBot(sc, configuration);
            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core.External.Dashboard);
            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core.AgentModel);
            sc.AddSingleton<IPluginSettingsTypeRegistry>(sp =>
            {
                var registry = new PluginSettingsTypeRegistry();

                // Register all known plugin settings types
                registry.Register<IncidentManagementSettings>("IncidentManagement");
                registry.Register<DGrepSettings>("DGrep");

                return registry;
            });


            sc.AddSingleton<IReloadableSettingsStore, ReloadableSettingsStore>();
            sc.AddSingleton<ReloadableOptionsChangeTokenSource<IncidentManagementSettings>>();
            sc.AddSingleton<IOptionsChangeTokenSource<IncidentManagementSettings>>(sp =>
                sp.GetRequiredService<ReloadableOptionsChangeTokenSource<IncidentManagementSettings>>());

            // Get fallback from core configuration
            sc.AddSingleton<IConfigureOptions<IncidentManagementSettings>>(sp =>
            {
                var store = sp.GetRequiredService<IReloadableSettingsStore>();
                var registry = sp.GetRequiredService<IPluginSettingsTypeRegistry>();

                // The fallback: injected from your application's strongly typed configuration
                var fallback = sp.GetRequiredService<TAppSettings>().Core.External.IncidentManagement;

                return new ReloadableOptionsConfigurator<IncidentManagementSettings>(store, registry, fallback);
            });



            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core.External.IncidentManagement);

            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core.External.GenevaActions);
            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core.External.MdmMetrics);
            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core.External.ICMWorkflows);
            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core.External.OneBranchApprovalService);
            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core.External.AgentHelper);
            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core.External.KeyVault);
            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core.External.ObserverClient);
            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core.External.OneBranchApprovalService);
            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core.External.AgentHelper);


            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core.Timer);

            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core.InstanceManagement);
            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core.AgentMemory);
        }

        private static void ConvertSettingsForTeamsBot(this IServiceCollection sc, IConfiguration configuration)
        {
            var serviceProvider = sc.BuildServiceProvider();
            var teamsBotConfig = serviceProvider.GetRequiredService<TeamsBotSettings>();
            configuration["MicrosoftAppType"] = teamsBotConfig.AppType;
            configuration["MicrosoftAppId"] = teamsBotConfig.AppId;
            configuration["MicrosoftAppPassword"] = teamsBotConfig.PasswordKey;
            configuration["MicrosoftAppTenantId"] = teamsBotConfig.TenantId;
            sc.AddSingleton(configuration);
        }

        public static IHostApplicationBuilder RegisterAzureSearchSettings(this IHostApplicationBuilder builder)
        {
            builder.Services.AddSingleton(GetAzureSearchSettings);
            return builder;
        }

        private static AzureSearchSettings GetAzureSearchSettings(IServiceProvider serviceProvider)
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();

            // try azure settings
            var settings = configuration
                .GetSection("AppSettings:Core:Azure:AzureSearch")
                .Get<AzureSearchSettings>();

            // try external if not found
            settings ??= configuration
                .GetSection("AppSettings:Core:External:AzureSearch")
                .Get<AzureSearchSettings>();

            // create new as last default
            settings ??= new AzureSearchSettings();

            return settings;
        }
    }
}
