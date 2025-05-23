// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Agent.Framework.Examples;

public static class ConfigurationExtensions
{
    public static void LoadLocalAppSettings(this IHostApplicationBuilder builder, bool isDevelopment = true)
    {
        builder.Configuration.SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true); // load base settings
        if (isDevelopment)
        {
            builder.Configuration.AddJsonFile("appsettings.development.json", optional: true, reloadOnChange: true); // load local dev settings one more time to override Azure App Configuration
        }
        builder.Configuration.AddEnvironmentVariables();
    }
}