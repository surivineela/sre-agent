// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

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
        public AzureSettings AzureSettings { get; } = new();
        public HostApplicationBuilder Builder { get; }

        public ConfigFixture()
        {
            Builder = Host.CreateApplicationBuilder();
            Builder.LoadAppSettings();
            Builder.ValidateAndRegisterAppSettings<AppSettings>();
            Builder.Services.ConfigureAzureOpenAIClient();
            Builder.Services.ConfigureIChatClient();

            var sp = Builder.Services.BuildServiceProvider();

            Configuration = Builder.Configuration;
            AzureSettings = sp.GetRequiredService<AzureSettings>();
        }
    }
}

