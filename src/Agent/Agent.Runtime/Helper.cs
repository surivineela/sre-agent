using Agent.Core.Configuration;
using Azure.Identity;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MiniValidation;

namespace Agent.Runtime
{
    public static class Helper
    {
        public static void LoadAppSettings(this IHostApplicationBuilder builder)
        {
            builder.Configuration.SetBasePath(builder.Environment.ContentRootPath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true) //load base settings
            .AddJsonFile("appsettings.development.json", optional: true, reloadOnChange: true) //load local settings
            .AddAzureAppConfiguration(options =>
            {
                string envPrefix = builder.Configuration.GetValue<string>("AppSettings:EnvPrefix");
                if (string.IsNullOrEmpty(envPrefix))
                {
                    throw new Exception("AppSettings:EnvPrefix not set. Please set so we can automatically fetch private environment settings. For more info, check readme.");
                }

                string endpoint = $"https://{builder.Configuration.GetValue<string>("AppSettings:EnvPrefix")}-appconfig.azconfig.io";
                DefaultAzureCredential cred = new DefaultAzureCredential(new DefaultAzureCredentialOptions() { ExcludeInteractiveBrowserCredential = !builder.Environment.IsDevelopment() });
                options.Connect(new Uri(endpoint), cred);
                options.ConfigureKeyVault(options =>
                {
                    options.SetCredential(cred);
                });
            })
            .AddJsonFile("appsettings.development.json", optional: true, reloadOnChange: true) //load local dev settings one more time to override Azure App Configuration
            .AddEnvironmentVariables();
        }

        public static void ValidateAndRegisterAppSettings<TAppSettings>(this IHostApplicationBuilder builder)
            where TAppSettings : AppSettings
        {
            builder.Services.AddOptionsWithValidateOnStart<TAppSettings>()
            .BindConfiguration("AppSettings")
            .ValidateDataAnnotations();
            builder.Services.AddSingleton(sp => {
                var settings = sp.GetRequiredService<IOptions<TAppSettings>>().Value;
                if (!MiniValidator.TryValidate(settings, out var validationErrors))
                {
                    List<string> stringErrors = [];
                    foreach ((string member, string[] memberErrors) in validationErrors)
                    {
                        var memberError = string.Join(", ", memberErrors);
                        stringErrors.Add(memberError);
                    }
                    throw new Exception($"AppSettings validation failed: {string.Join("\n", stringErrors)}");
                }

                return settings;
            });

            builder.Services.RegisterInnerAppSettings<TAppSettings>();
        }

        public static void RegisterInnerAppSettings<TAppSettings>(this IServiceCollection sc)
            where TAppSettings : AppSettings
        {

            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core.Azure);
            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core.Azure.OpenAI);
            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core.Azure.CosmosDB);
            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core.Azure.CosmosDB.Graph);

            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core.External);
            sc.AddSingleton(sp => sp.GetRequiredService<TAppSettings>().Core.External.GitHub);
        }
    }
}
