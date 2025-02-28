using Agent.Core.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Agent.Runtime;

namespace Agent.Tests.Common
{
    public class ConfigFixture
    {
        public IConfiguration Configuration { get; }
        public AzureSettings AzureSettings { get; }

        public ConfigFixture()
        {
            IHostApplicationBuilder builder = Host.CreateApplicationBuilder();
            builder.LoadAppSettings();
            builder.ValidateAndRegisterAppSettings<AppSettings>();

            var sp = builder.Services.BuildServiceProvider();

            AzureSettings = sp.GetRequiredService<AzureSettings>();
        }
    }
}
