using Azure.AI.OpenAI;
using Azure.Identity;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Xunit.Abstractions;

using E2ETests.Models;
using OpenAI.Chat;
using Microsoft.Extensions.Configuration;
using Agent.Tests.Common;
using E2ETests;
using Agent.Core.Configuration;

namespace Agent.Tests.End2End.Fixtures
{
    /// <summary>
    /// Makes sure that a Web App exists for the tests to run against
    /// </summary>
    public class WebAppFixture
    {
        private readonly IMessageSink _sink;
        private WebApp _webApp;

        public ConfigFixture ConfigFixture { get; }


        public WebAppFixture(IMessageSink sink)
        {
            _sink = sink;
            TestSettings testSettings = ConfigFixture.Configuration.GetRequiredSection("TestSettings").Get<TestSettings>();

            _webApp = new WebApp(testSettings, sink);
            _webApp.EnsureWebAppExists().GetAwaiter().GetResult();
        }
    }
}