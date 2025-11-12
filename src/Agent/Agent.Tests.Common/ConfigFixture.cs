// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
            Builder.Services.ConfigureIChatClient(Builder.Configuration);

            var sp = Builder.Services.BuildServiceProvider();

            Configuration = Builder.Configuration;
            AzureSettings = sp.GetRequiredService<AzureSettings>();
        }
    }
}

