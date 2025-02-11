using Microsoft.Extensions.Configuration;
using OperationalAgentCore;

namespace OperationAgent.Tests.Common
{
    public class ConfigFixture
    {
        public IConfiguration Configuration { get; }
        public AzureSettings AzureSettings { get; }

        public ConfigFixture()
        {
            Configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true) //load base settings
                .AddJsonFile("appsettings.development.json", optional: true, reloadOnChange: true) //load local settings
                .AddEnvironmentVariables()
                .Build();

            if (Configuration == null)
            {
                throw new InvalidOperationException($"Error: Could not find appsettings.json or appsettings.development.json at {Directory.GetCurrentDirectory()}");
            }

            AzureSettings = Configuration.GetRequiredSection("Azure").Get<AzureSettings>();
        }
    }
}
