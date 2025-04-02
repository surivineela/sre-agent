using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Agent.Core.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Agent.Core.Extensions;
public static class HostApplicationBuilderExtensions
{
    public static string ResolveDtsConnectionString(this IHostApplicationBuilder builder)
    {
        var azureSettings = builder.Configuration.GetSection("AppSettings")
            .GetSection("Core")
            .GetSection("Azure")
            .Get<AzureSettings>();

        string? durableConnectionString = azureSettings?.DTS.ConnectionString;

        if (string.IsNullOrEmpty(durableConnectionString) && builder.Environment.IsDevelopment())
        {
            durableConnectionString = "Endpoint=http://localhost:14280;TaskHub=default;Authentication=None";
        }

        if (string.IsNullOrEmpty(durableConnectionString))
        {
            throw new InvalidOperationException("Durable Task Scheduler connection string is not configured.");
        }

        return durableConnectionString;
    }
}
