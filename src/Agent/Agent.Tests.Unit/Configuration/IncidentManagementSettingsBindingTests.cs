// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using Agent.Core.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Agent.Tests.Unit.Configuration;

public class IncidentManagementSettingsBindingTests
{
    private static readonly object EnvLock = new();

    [Fact]
    public void EnvironmentVariables_ConfigureAutomatedRcaDictionaryAndDefaults()
    {
        const string prefix = "AppSettings__Core__External__IncidentManagement__AutomatedRCA";
        var envVars = new Dictionary<string, string>
        {
            [$"{prefix}__DefaultResultTag"] = "EnvDefaultTag",
            [$"{prefix}__ResultTags__scale_controller_preflight_agent"] = "EnvScaleTag",
            [$"{prefix}__AccessNote"] = "EnvAccessNote",
            [$"{prefix}__WebBaseUrl"] = "https://example.com"
        };

        lock (EnvLock)
        {
            try
            {
                foreach (var kvp in envVars)
                {
                    Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
                }

                var configuration = new ConfigurationBuilder()
                    .AddEnvironmentVariables()
                    .Build();

                var incidentSettings = new IncidentManagementSettings();
                configuration.GetSection("AppSettings:Core:External:IncidentManagement").Bind(incidentSettings);

                Assert.Equal("EnvDefaultTag", incidentSettings.AutomatedRCA.DefaultResultTag);
                Assert.Equal("EnvScaleTag", incidentSettings.AutomatedRCA.ResultTags["scale_controller_preflight_agent"]);
                Assert.Equal("EnvAccessNote", incidentSettings.AutomatedRCA.AccessNote);
                Assert.Equal("https://example.com", incidentSettings.AutomatedRCA.WebBaseUrl);
            }
            finally
            {
                foreach (var kvp in envVars)
                {
                    Environment.SetEnvironmentVariable(kvp.Key, null);
                }
            }
        }
    }
}
