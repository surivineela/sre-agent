using System.Configuration;
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

        public static void LoadLocalAppSettings(this IHostApplicationBuilder builder, bool isDevelopment)
        {
            builder.Configuration.SetBasePath(builder.Environment.ContentRootPath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true); //load base settings

            if (isDevelopment)
            {
                builder.Configuration.AddJsonFile("appsettings.development.json", optional: true, reloadOnChange: true); // load local dev settings one more time to override Azure App Configuration
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
                builder.Configuration.AddAzureAppConfiguration(options =>
                {
                    string envPrefix = builder.Configuration.GetValue<string>("AppSettings:EnvPrefix");

                    if (string.IsNullOrEmpty(envPrefix))
                    {
                        throw new ConfigurationErrorsException("AppSettings:EnvPrefix not set. Please set so we can automatically fetch private environment settings. For more info, check readme.");
                    }

                    string endpoint = $"https://{builder.Configuration.GetValue<string>("AppSettings:EnvPrefix")}-appconfig.azconfig.io";
                    DefaultAzureCredential cred = new DefaultAzureCredential(new DefaultAzureCredentialOptions() { ExcludeInteractiveBrowserCredential = !builder.Environment.IsDevelopment() });
                    options.Connect(new Uri(endpoint), cred);
                    options.ConfigureKeyVault(options =>
                    {
                        options.SetCredential(cred);
                    });
                });
            }

            if (isDevelopment)
            {
                builder.Configuration.AddJsonFile("appsettings.development.json", optional: true, reloadOnChange: true); // load local dev settings one more time to override Azure App Configuration
            }
            builder.Configuration.AddEnvironmentVariables();
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
            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core.Azure);
            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core.Azure.Crawler);
            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core.Azure.CosmosDB);
            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core.Azure.CosmosDB.Graph);
            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core.Azure.CosmosDB.Docs);
            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core.Azure.OpenAI);

            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core.External);
            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core.External.GitHub);
            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core.External.MCP);

            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core.External.TeamsBot);
            ConvertSettingsForTeamsBot(sc, configuration);

            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core.Timer);
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
    }
}
