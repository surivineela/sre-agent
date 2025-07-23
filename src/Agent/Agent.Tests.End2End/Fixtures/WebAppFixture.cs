// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Xunit.Abstractions;

using E2ETests.Models;
using Microsoft.Extensions.Configuration;
using Agent.Tests.Common;
using Agent.Core.Configuration;

namespace Agent.Tests.End2End.Fixtures
{
    /// <summary>
    /// Makes sure that a Web App exists for the tests to run against
    /// </summary>
    public class WebAppFixture
    {
        private readonly IMessageSink _sink;
        private WebApp? _webApp;

        public ConfigFixture ConfigFixture { get; } = new ConfigFixture();


        public WebAppFixture(IMessageSink sink)
        {
            _sink = sink;
            TestSettings? testSettings = ConfigFixture.Configuration.GetRequiredSection("TestSettings").Get<TestSettings>();

            if (testSettings != null)
            {
                _webApp = new WebApp(testSettings, sink);
                _webApp.EnsureWebAppExists().GetAwaiter().GetResult();
            }
        }
    }
}
