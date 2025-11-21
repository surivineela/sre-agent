using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Agent.Core.Interfaces;
using Agent.Framework;
using Agent.Logging;
using Agent.Plugins.Implementation;
using Azure.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Agent.Tests.Unit.Plugins.Implementation
{
    public class CannotConnectToVmPluginTests
    {
        private static readonly string ResourceId =
            "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Compute/virtualMachines/vm1";

        private static CannotConnectToVmPlugin CreatePlugin(HttpMessageHandler handler)
        {
            var client = new HttpClient(handler);
            var factory = new TestHttpClientFactory(client);
            var auth = new StubAuthenticationService();
            return new CannotConnectToVmPlugin(factory, NullLogger<CannotConnectToVmPlugin>.Instance, auth)
            {
                ThreadId = Guid.NewGuid()
            };
        }

        private static string BuildTerminalBody(string completionTextRawJsonOrPlain)
        {
            // completionText must be a JSON string containing either raw text or JSON.
            var completionTextJsonString = JsonSerializer.Serialize(completionTextRawJsonOrPlain);
            return $"{{\"properties\":{{\"provisioningState\":\"Succeeded\",\"completionText\":{completionTextJsonString}}}}}";
        }

        #region Tests

        [Fact]
        public async Task DiagnoseVmConnectivityIssues_Linux_ReturnsCombinedScreenshotAndSerial()
        {
            // Screenshot: RuleMatched true + Answer
            var screenshotInner = "{\"RuleMatched\":true,\"Answer\":\"Screenshot analysis details\"}";
            // Serial: RuleMatched true + Llm_response
            var serialInner = "{\"RuleMatched\":true,\"Llm_response\":\"Serial analysis details\"}";

            var handler = new SequenceHttpMessageHandler(new[]
            {
                // Screenshot invocation (PUT + GET)
                new HttpResponseMessage(HttpStatusCode.OK), // PUT
                new HttpResponseMessage(HttpStatusCode.OK){ Content = new StringContent(BuildTerminalBody(screenshotInner), Encoding.UTF8, "application/json") },
                // Serial invocation (PUT + GET)
                new HttpResponseMessage(HttpStatusCode.OK),
                new HttpResponseMessage(HttpStatusCode.OK){ Content = new StringContent(BuildTerminalBody(serialInner), Encoding.UTF8, "application/json") },
            });

            var plugin = CreatePlugin(handler);

            var result = await plugin.DiagnoseVmConnectivityIssuesAsync(ResourceId, "Linux", null);

            Assert.Contains("Result from Screen Shot Analysis Plugin: Screenshot analysis details", result);
            Assert.Contains("Result from Serial Log Analyzer Plugin: Serial analysis details", result);
            Assert.Equal(4, handler.Requests.Count);
        }

        [Fact]
        public async Task DiagnoseVmConnectivityIssues_Windows_ScreenshotOnly()
        {
            var screenshotInner = "{\"RuleMatched\":true,\"Answer\":\"Win screenshot details\"}";
            var handler = new SequenceHttpMessageHandler(new[]
            {
                new HttpResponseMessage(HttpStatusCode.OK),
                new HttpResponseMessage(HttpStatusCode.OK){ Content = new StringContent(BuildTerminalBody(screenshotInner), Encoding.UTF8, "application/json") }
            });

            var plugin = CreatePlugin(handler);

            var result = await plugin.DiagnoseVmConnectivityIssuesAsync(ResourceId, "Windows", null);

            Assert.Equal("Result from Screen Shot Analysis Plugin: Win screenshot details", result);
            Assert.Equal(2, handler.Requests.Count);
        }

        [Fact]
        public async Task DiagnoseVmConnectivityIssues_WithTsgFileName_KnownIssueShortCircuits()
        {
            var handler = new SequenceHttpMessageHandler(Array.Empty<HttpResponseMessage>());
            var plugin = CreatePlugin(handler);

            var result = await plugin.DiagnoseVmConnectivityIssuesAsync(ResourceId, "Windows", "nonexistent-tsg");

            Assert.Equal("Known issue: nonexistent-tsg", result);
            Assert.Empty(handler.Requests); // No network calls
        }

        [Fact]
        public async Task DiagnoseVmConnectivityIssues_ThrowsIfThreadIdMissing()
        {
            var handler = new SequenceHttpMessageHandler(Array.Empty<HttpResponseMessage>());
            var plugin = CreatePlugin(handler);
            plugin.ThreadId = null;

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                plugin.DiagnoseVmConnectivityIssuesAsync(ResourceId, "Windows", null));
        }

        [Fact]
        public async Task AnalyzeVmScreenshotAsync_RetryOnConflictRegeneratesPluginId()
        {
            var inner = "{\"RuleMatched\":true,\"Answer\":\"Conflict resolution screenshot\"}";
            var handler = new SequenceHttpMessageHandler(new[]
            {
                // First PUT -> Conflict (body contains "Not unique")
                new HttpResponseMessage(HttpStatusCode.Conflict)
                {
                    Content = new StringContent("Not unique plugin name")
                },
                // Second PUT -> success
                new HttpResponseMessage(HttpStatusCode.OK),
                // GET -> terminal result
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(BuildTerminalBody(inner), Encoding.UTF8, "application/json")
                }
            });

            var plugin = CreatePlugin(handler);

            var result = await plugin.AnalyzeVmScreenshotAsync(ResourceId);

            Assert.Equal("Result from Screen Shot Analysis Plugin: Conflict resolution screenshot", result);
            // Expect 3 requests: PUT(conflict), PUT(success), GET
            Assert.Equal(3, handler.Requests.Count);
            Assert.Equal(HttpMethod.Put, handler.Requests[0].Method);
            Assert.Equal(HttpMethod.Put, handler.Requests[1].Method);
            Assert.Equal(HttpMethod.Get, handler.Requests[2].Method);
        }

        [Fact]
        public async Task AnalyzeVmSerialLog_RuleMatchedFalse_ReturnsGenericMessage()
        {
            var inner = "{\"RuleMatched\":false}";
            var handler = new SequenceHttpMessageHandler(new[]
            {
                new HttpResponseMessage(HttpStatusCode.OK),
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(BuildTerminalBody(inner), Encoding.UTF8, "application/json")
                }
            });

            var plugin = CreatePlugin(handler);

            var result = await plugin.AnalyzeVmSerialLogAsync(ResourceId);

            Assert.Equal("No issues were found with the VM. HANDOFF: meta_agent (Plugin did not detect a connectivity cause; continue broader investigation).", result);
        }

        [Fact]
        public async Task AnalyzeVmSerialLog_BackCompat_NoRuleMatched_UsesLegacyPrefix()
        {
            var inner = "{\"Llm_response\":\"Legacy analysis details\"}";
            var handler = new SequenceHttpMessageHandler(new[]
            {
                new HttpResponseMessage(HttpStatusCode.OK),
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(BuildTerminalBody(inner), Encoding.UTF8, "application/json")
                }
            });

            var plugin = CreatePlugin(handler);

            var result = await plugin.AnalyzeVmSerialLogAsync(ResourceId);

            Assert.Equal("Result from CannotConnectToVmPlugin: Legacy analysis details", result);
        }

        [Fact]
        public async Task AnalyzeVmScreenshot_RuleMatchedTrue_NoAnswerOrLlmResponse_ReturnsRawInnerJson()
        {
            var inner = "{\"RuleMatched\":true,\"UnexpectedField\":\"Data\"}";
            var handler = new SequenceHttpMessageHandler(new[]
            {
                new HttpResponseMessage(HttpStatusCode.OK),
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(BuildTerminalBody(inner), Encoding.UTF8, "application/json")
                }
            });

            var plugin = CreatePlugin(handler);

            var result = await plugin.AnalyzeVmScreenshotAsync(ResourceId);

            // Since no Llm_response / Answer -> raw completionText (inner string)
            Assert.Equal(inner, result);
        }

        [Fact]
        public async Task AnalyzeVmScreenshot_CompletionTextPlain_ReturnsPlain()
        {
            var plain = "Plain text outcome";
            var handler = new SequenceHttpMessageHandler(new[]
            {
                new HttpResponseMessage(HttpStatusCode.OK),
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(BuildTerminalBody(plain), Encoding.UTF8, "application/json")
                }
            });

            var plugin = CreatePlugin(handler);

            var result = await plugin.AnalyzeVmScreenshotAsync(ResourceId);

            Assert.Equal(plain, result);
        }

        #endregion
    }

    #region Test Infrastructure

    internal sealed class SequenceHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;
        public List<HttpRequestMessage> Requests { get; } = new();

        public SequenceHttpMessageHandler(IEnumerable<HttpResponseMessage> responses)
        {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (_responses.Count == 0)
                throw new InvalidOperationException("No more responses configured for HTTP handler.");

            return Task.FromResult(_responses.Dequeue());
        }
    }

    internal sealed class TestHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;
        public TestHttpClientFactory(HttpClient client) => _client = client;
        public HttpClient CreateClient(string name) => _client;
    }

    internal sealed class StubAuthenticationService : IAuthenticationService
    {
        private readonly TokenCredential _credential = new FakeTokenCredential();
        public TokenCredential GetPostgresSqlCredential() => _credential;

        public TokenCredential GetDocumentDbCredential() => _credential;
        public TokenCredential GetGraphDbCredential() => _credential;
        public Task<AccessToken> GetTokenFromAgentSpaceProxy(string scope, string resourceId)
            => Task.FromResult(new AccessToken("fake-token", DateTimeOffset.UtcNow.AddHours(1)));

        public TokenCredential GetDtsCredential() => _credential;
        public TokenCredential GetSearchEndpointCredential() => _credential;
        public TokenCredential GetSearchPluginCredential() => _credential;
        public TokenCredential GetSessionPoolCredential() => _credential;
        public TokenCredential GetCrawlerCredential() => _credential;
        public Task<TokenCredential> GetArmOperationCredential() => Task.FromResult<TokenCredential>(_credential);
        public TokenCredential GetAzureMonitorWorkspaceCredential() => _credential;
        public Task<string> GetGrafanaAccessToken() => Task.FromResult("grafana-token");
        public TokenCredential GetAzureOpenAICredential() => _credential;
        public TokenCredential GetAppInsightsCredential() => _credential;
        public TokenCredential GetStorageCredential() => _credential;
        public TokenCredential GetLogAnalyticsCredential() => _credential;
        public Task<TokenCredential> GetKubernetesOperationCredential() => Task.FromResult<TokenCredential>(_credential);
        public string? GetActionIdentity() => "action-id";
        public TokenCredential GetAgentMemoryBlobStorageCredential() => _credential;
        public TokenCredential GetAgentMemoryAzureAISearchCredential() => _credential;
        public TokenCredential GetObserverCredential() => _credential;
        public TokenCredential GetAgentHelperCredential() => _credential;
        public TokenCredential GetAzureDevOpsCredential() => _credential;
        public Task<string> GetGitHubAccessToken() => Task.FromResult("gh-token");
        public TokenCredential GetApplensCredential() => _credential;
        public string GetApplensRuntimeHostUrl() => "https://applens.test";
        public TokenCredential Get1PAgentKeyVaultCredential(string managedIdentityId) => _credential;
        public TokenCredential GetIcmApiCredential() => _credential;
        public TokenCredential GetDataConnectorCredential(ConnectorAuthSettings connectorAuthSettings) => _credential;
        public TokenCredential GetAzureSearchCredential() => _credential;
        public TokenCredential GetEventHubTraceExportCredential(EventHubTraceExporterOptions options) => _credential;

        // FIX: Implement missing interface member
        public TokenCredential GetAgentSpaceProxyCredential() => _credential;

        public TokenCredential GetMdmMetricsCredential() => _credential;

        public TokenCredential GetDiagnosticServiceCredential() => _credential;

    }

    internal sealed class FakeTokenCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
            => new("fake-token", DateTimeOffset.UtcNow.AddHours(1));

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
            => new(new AccessToken("fake-token", DateTimeOffset.UtcNow.AddHours(1)));
    }

    #endregion
}
