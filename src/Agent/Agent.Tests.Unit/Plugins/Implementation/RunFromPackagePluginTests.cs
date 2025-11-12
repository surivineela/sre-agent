// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Agent.Core.Clients.Storage;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Services;
using Agent.Framework;
using Agent.Logging;
using Agent.Plugins.Implementation;
using Agent.Plugins.Models.RunFromPackage;
using Azure.Core;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Agent.Tests.Unit.Plugins.Implementation
{
    public class RunFromPackagePluginTests
    {
        private readonly Mock<ILogger<RunFromPackagePlugin>> _mockLogger;
        private readonly ArmHelper _armHelper;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<IAuthenticationService> _mockAuthService;
        private readonly Mock<IAzureBlobStorageClient> _mockBlobStorageClient;
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly RunFromPackagePlugin _plugin;

        private const string TestResourceId = "/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/sites/test-function-app";
        private const string TestSanitizedUrl = "https://teststorageaccount.blob.core.windows.net/deployments/package.zip";
        private const string TestUnsanitizedUrl = "https://teststorageaccount.blob.core.windows.net/deployments/package.zip?sig=secrettoken&st=2024-01-01&se=2024-01-02";

        public RunFromPackagePluginTests()
        {
            _mockLogger = new Mock<ILogger<RunFromPackagePlugin>>();
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockAuthService = new Mock<IAuthenticationService>();
            _mockBlobStorageClient = new Mock<IAzureBlobStorageClient>();
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();

            // Create real ArmHelper with all required dependencies like in PostgreSQLPlaybookTests
            var mockArmLogger = new Mock<ILogger<ArmHelper>>();
            var mockCustomerLogger = new Mock<CustomerLogger>();
            var mockArmClientFactory = new Mock<IArmClientFactory>();
            var mockAzureSettings = new AzureSettings();
            var mockHostEnvironment = new Mock<IHostEnvironment>();
            var mockCrawlerTriggerService = new Mock<ICrawlerTriggerService>();
            var mockSessionPoolService = new Mock<ISessionPoolService>();
            var mockChatClient = new Mock<IChatClientProvider>();

            _armHelper = new ArmHelper(
                mockArmLogger.Object,
                mockCustomerLogger.Object,
                _mockHttpClientFactory.Object,
                mockArmClientFactory.Object,
                _mockAuthService.Object,
                mockAzureSettings,
                mockHostEnvironment.Object,
                mockCrawlerTriggerService.Object,
                mockSessionPoolService.Object,
                mockChatClient.Object);

            // Setup HttpClientFactory to return a new HttpClient instance each time
            _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(() => new HttpClient(_mockHttpMessageHandler.Object));

            _plugin = new RunFromPackagePlugin(
                _mockLogger.Object,
                _armHelper,
                _mockHttpClientFactory.Object,
                _mockAuthService.Object,
                _mockBlobStorageClient.Object);
        }

        #region GetRunFromPackageConfigurationAsync Tests

        [Fact]
        public void Constructor_WithValidParameters_SetsThreadId()
        {
            // Arrange & Act
            var testThreadId = Guid.NewGuid();
            _plugin.ThreadId = testThreadId;

            // Assert
            Assert.Equal(testThreadId, _plugin.ThreadId);
        }

        [Fact]
        public async Task GetRunFromPackageConfigurationAsync_UrlConfiguration_ReturnsCorrectConfig()
        {
            // Arrange
            var appSettingsJson = @"{
                ""properties"": {
                    ""WEBSITE_RUN_FROM_PACKAGE"": """ + TestUnsanitizedUrl + @"""
                }
            }";

            SetupAppSettingsResponse(appSettingsJson);
            SetupResourceDetailsResponse("Windows"); // Mock OS detection
            SetupAppServicePlanResponse("Consumption"); // Mock SKU detection

            // Act
            var result = await _plugin.GetRunFromPackageConfigurationAsync(TestResourceId);

            // Assert
            Assert.True(result.SettingExists);
            Assert.Equal(TestUnsanitizedUrl, result.CurrentValue);
            Assert.Equal(RunFromPackageMode.ExternalUrl, result.Mode);
            Assert.True(result.IsValid);
        }

        [Fact]
        public async Task GetRunFromPackageConfigurationAsync_MissingSetting_ReturnsInvalidConfig()
        {
            // Arrange
            var appSettingsJson = @"{
                ""properties"": {}
            }";

            SetupAppSettingsResponse(appSettingsJson);

            // Act
            var result = await _plugin.GetRunFromPackageConfigurationAsync(TestResourceId);

            // Assert
            Assert.False(result.SettingExists);
            Assert.False(result.IsValid);
            Assert.Equal(RunFromPackageMode.None, result.Mode);
        }

        [Fact]
        public async Task GetRunFromPackageConfigurationAsync_InvalidValue_ReturnsInvalidConfig()
        {
            // Arrange
            var appSettingsJson = @"{
                ""properties"": {
                    ""WEBSITE_RUN_FROM_PACKAGE"": ""invalid-value""
                }
            }";

            SetupAppSettingsResponse(appSettingsJson);
            SetupResourceDetailsResponse("Windows"); // Mock OS detection
            SetupAppServicePlanResponse("Consumption"); // Mock SKU detection

            // Act
            var result = await _plugin.GetRunFromPackageConfigurationAsync(TestResourceId);

            // Assert
            Assert.True(result.SettingExists);
            Assert.Equal("invalid-value", result.CurrentValue);
            Assert.Equal(RunFromPackageMode.Invalid, result.Mode);
            Assert.False(result.IsValid);
        }

        #endregion

        #region VerifyRunFromPackageConfigurationAsync Tests

        [Fact]
        public async Task VerifyRunFromPackageConfigurationAsync_ValidLocalPackage_ReturnsValid()
        {
            // Arrange
            var appSettingsJson = @"{
                ""properties"": {
                    ""WEBSITE_RUN_FROM_PACKAGE"": ""1""
                }
            }";

            SetupAppSettingsResponse(appSettingsJson);
            // Note: For Windows OS, we'll need to handle this separately in the ARM helper

            // Act
            var result = await _plugin.VerifyRunFromPackageConfigurationAsync(TestResourceId);

            // Assert
            Assert.Equal(ConfigurationStatus.Valid, result.Status);
            Assert.True(result.IsSuccessful);
            Assert.True(result.IsSupported);
            Assert.Equal("1", result.CurrentValue);
        }

        [Fact]
        public async Task VerifyRunFromPackageConfigurationAsync_MissingConfiguration_ReturnsMissing()
        {
            // Arrange
            var appSettingsJson = @"{
                ""properties"": {}
            }";

            SetupAppSettingsResponse(appSettingsJson);

            // Act
            var result = await _plugin.VerifyRunFromPackageConfigurationAsync(TestResourceId);

            // Assert
            Assert.Equal(ConfigurationStatus.Missing, result.Status);
            Assert.False(result.IsSuccessful);
            Assert.Contains("WEBSITE_RUN_FROM_PACKAGE setting is missing", result.Issues);
        }

        #endregion

        #region SKU Validation Tests

        [Theory]
        [InlineData("Consumption", "Windows", true, true, RunFromPackageMode.LocalPackage, "1")]
        [InlineData("Consumption", "Linux", false, true, RunFromPackageMode.ExternalUrl, "<URL>")]
        [InlineData("Premium", "Windows", true, true, RunFromPackageMode.LocalPackage, "1")]
        [InlineData("Premium", "Linux", true, true, RunFromPackageMode.LocalPackage, "1")]
        [InlineData("FlexConsumption", "Windows", true, true, RunFromPackageMode.None, "")]
        [InlineData("FlexConsumption", "Linux", false, true, RunFromPackageMode.None, "")]
        public void SkuCapabilities_GetForSku_ReturnsCorrectCapabilities(
            string sku, string os, bool expectedLocalSupport, bool expectedUrlSupport,
            RunFromPackageMode expectedMode, string expectedValue)
        {
            // Act
            var capabilities = SkuCapabilities.GetForSku(sku, os);

            // Assert
            Assert.Equal(sku, capabilities.SkuName);
            Assert.Equal(os, capabilities.OperatingSystem);
            Assert.Equal(expectedLocalSupport, capabilities.SupportsLocalPackage);
            Assert.Equal(expectedUrlSupport, capabilities.SupportsExternalUrl);
            Assert.Equal(expectedMode, capabilities.RecommendedMode);
            Assert.Equal(expectedValue, capabilities.RecommendedValue);
        }

        [Theory]
        [InlineData("Consumption", "Linux", RunFromPackageMode.LocalPackage, false)]
        [InlineData("Consumption", "Linux", RunFromPackageMode.ExternalUrl, true)]
        [InlineData("Premium", "Windows", RunFromPackageMode.LocalPackage, true)]
        [InlineData("Premium", "Windows", RunFromPackageMode.ExternalUrl, true)]
        [InlineData("Premium", "Linux", RunFromPackageMode.LocalPackage, true)]
        [InlineData("Premium", "Linux", RunFromPackageMode.ExternalUrl, true)]
        [InlineData("FlexConsumption", "Windows", RunFromPackageMode.LocalPackage, true)]
        [InlineData("FlexConsumption", "Linux", RunFromPackageMode.ExternalUrl, true)]
        public void SkuCapabilities_SupportsMode_ReturnsCorrectSupport(
            string sku, string os, RunFromPackageMode mode, bool expectedSupport)
        {
            // Arrange
            var capabilities = SkuCapabilities.GetForSku(sku, os);

            // Act
            var supportsMode = capabilities.SupportsMode(mode);

            // Assert
            Assert.Equal(expectedSupport, supportsMode);
        }

        #endregion

        #region Secret Sanitization Tests

        [Theory]
        [InlineData("https://test.blob.core.windows.net/container/blob.zip", "https://test.blob.core.windows.net/container/blob.zip")]
        [InlineData("https://test.blob.core.windows.net/container/blob.zip?sig=secret", "https://test.blob.core.windows.net/container/blob.zip")]
        [InlineData("https://test.blob.core.windows.net/container/blob.zip?sig=secret&st=start&se=end", "https://test.blob.core.windows.net/container/blob.zip")]
        [InlineData("https://test.blob.core.windows.net/container/blob.zip?param=value&sig=secret", "https://test.blob.core.windows.net/container/blob.zip?param=value")]
        [InlineData("invalid-url", "[SANITIZED_URL]")]
        [InlineData("", "")]
        public void SanitizeUrl_VariousInputs_ReturnsSanitizedOutput(string input, string expected)
        {
            // Act
            var result = InvokePrivateMethod<string>(_plugin, "SanitizeUrl", input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void SanitizeUrl_NullInput_ReturnsEmpty()
        {
            // Act
            var result = InvokePrivateMethod<string>(_plugin, "SanitizeUrl", (string?)null);

            // Assert
            Assert.Equal("", result);
        }

        [Theory]
        [InlineData("AccountKey=AbCdEfGhIjKlMnOpQrStUvWxYz0123456789+/AbCdEfGhIjKlMnOpQrStUvWxYz0123456789+/==", "AccountKey=****")]
        [InlineData("sig=secretsignature123", "sig=****")]
        [InlineData("AbCdEfGhIjKlMnOpQrStUvWxYz0123456789+/AbCdEfGhIjKlMnOpQrStUvWxYz0123456789+/==", "****")]
        [InlineData("DefaultEndpointsProtocol=https;AccountName=test;AccountKey=secretkey123;EndpointSuffix=core.windows.net", "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=****;EndpointSuffix=core.windows.net")]
        [InlineData("", "")]
        public void RedactStorageKeys_VariousInputs_ReturnsRedactedOutput(string input, string expected)
        {
            // Act
            var result = InvokePrivateMethod<string>(_plugin, "RedactStorageKeys", input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void RedactStorageKeys_NullInput_ReturnsEmpty()
        {
            // Act
            var result = InvokePrivateMethod<string>(_plugin, "RedactStorageKeys", (string?)null);

            // Assert
            Assert.Equal("", result);
        }

        #endregion

        #region HasRunFromPackageIssuesAsync Tests

        [Fact]
        public async Task HasRunFromPackageIssuesAsync_ValidConfiguration_ReturnsFalse()
        {
            // Arrange
            var appSettingsJson = @"{
                ""properties"": {
                    ""WEBSITE_RUN_FROM_PACKAGE"": ""1""
                }
            }";

            SetupAppSettingsResponse(appSettingsJson);
            // Note: For Windows OS, we'll need to handle this separately in the ARM helper

            // Act
            var result = await _plugin.HasRunFromPackageIssuesAsync(TestResourceId);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task HasRunFromPackageIssuesAsync_InvalidConfiguration_ReturnsTrue()
        {
            // Arrange
            var appSettingsJson = @"{
                ""properties"": {
                    ""WEBSITE_RUN_FROM_PACKAGE"": ""invalid-value""
                }
            }";

            SetupAppSettingsResponse(appSettingsJson);

            // Act
            var result = await _plugin.HasRunFromPackageIssuesAsync(TestResourceId);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task HasRunFromPackageIssuesAsync_MissingConfiguration_ReturnsTrue()
        {
            // Arrange
            var appSettingsJson = @"{
                ""properties"": {}
            }";

            SetupAppSettingsResponse(appSettingsJson);

            // Act
            var result = await _plugin.HasRunFromPackageIssuesAsync(TestResourceId);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task HasRunFromPackageIssuesAsync_LinuxConsumptionWithLocalPackage_ReturnsTrue()
        {
            // Arrange
            var appSettingsJson = @"{
                ""properties"": {
                    ""WEBSITE_RUN_FROM_PACKAGE"": ""1""
                }
            }";

            SetupAppSettingsResponse(appSettingsJson);

            // Setup Linux OS response
            SetupResourceDetailsResponse("Linux");

            // Setup Consumption SKU response
            SetupAppServicePlanResponse("Consumption");

            // Act
            var result = await _plugin.HasRunFromPackageIssuesAsync(TestResourceId);

            // Assert
            Assert.True(result); // Linux Consumption doesn't support local package mode
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task GetRunFromPackageConfigurationAsync_ArmHelperThrows_ReturnsErrorResult()
        {
            // Arrange
            SetupArmApiResponse("/config/appSettings/list", HttpStatusCode.InternalServerError, "ARM API error");

            // Act
            var result = await _plugin.GetRunFromPackageConfigurationAsync(TestResourceId);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains("An error occurred while getting WEBSITE_RUN_FROM_PACKAGE configuration", result.Details);
        }

        [Fact]
        public async Task VerifyRunFromPackageConfigurationAsync_Exception_ReturnsErrorResult()
        {
            // Arrange
            SetupArmApiResponse("/config/appSettings/list", HttpStatusCode.InternalServerError, "Network error");

            // Act
            var result = await _plugin.VerifyRunFromPackageConfigurationAsync(TestResourceId);

            // Assert
            Assert.False(result.IsSuccessful);
            Assert.Contains("An error occurred during verification", result.ErrorMessage);
        }

        [Fact]
        public async Task HasRunFromPackageIssuesAsync_Exception_ReturnsTrue()
        {
            // Arrange
            SetupArmApiResponse("/config/appSettings/list", HttpStatusCode.InternalServerError, "Service unavailable");

            // Act
            var result = await _plugin.HasRunFromPackageIssuesAsync(TestResourceId);

            // Assert
            Assert.True(result); // Should return true to trigger handoff when errors occur
        }

        #endregion

        #region Diagnostic Tests

        [Fact]
        public async Task DiagnoseRunFromPackageIssuesAsync_ValidConfiguration_ReturnsHealthyStatus()
        {
            // Arrange
            var appSettingsJson = @"{
                ""properties"": {
                    ""WEBSITE_RUN_FROM_PACKAGE"": ""1""
                }
            }";

            SetupAppSettingsResponse(appSettingsJson);
            SetupResourceDetailsResponse("Windows"); // Mock OS detection
            SetupAppServicePlanResponse("Consumption"); // Mock SKU detection

            // Act
            var result = await _plugin.DiagnoseRunFromPackageIssuesAsync(TestResourceId);

            // Assert
            Assert.True(result.IsSuccessful);
            Assert.Equal(HealthStatus.Healthy, result.OverallStatus);
            Assert.Empty(result.ConfigurationIssues);
        }

        [Fact]
        public async Task DiagnoseRunFromPackageIssuesAsync_InvalidConfiguration_ReturnsUnhealthyStatus()
        {
            // Arrange
            var appSettingsJson = @"{
                ""properties"": {
                    ""WEBSITE_RUN_FROM_PACKAGE"": ""invalid-value""
                }
            }";

            SetupAppSettingsResponse(appSettingsJson);
            SetupResourceDetailsResponse("Windows"); // Mock OS detection
            SetupAppServicePlanResponse("Consumption"); // Mock SKU detection

            // Act
            var result = await _plugin.DiagnoseRunFromPackageIssuesAsync(TestResourceId);

            // Assert
            Assert.True(result.IsSuccessful);
            Assert.Equal(HealthStatus.Unhealthy, result.OverallStatus);
            Assert.NotEmpty(result.ConfigurationIssues);
            Assert.Contains(result.ConfigurationIssues, issue => issue.Type == IssueType.Configuration);
        }

        #endregion

        #region Repair Tests

        [Fact]
        public async Task RepairRunFromPackageConfigurationAsync_SetToOne_ReturnsSuccessfulResult()
        {
            // Arrange
            var appSettings = new Dictionary<string, string> { { "WEBSITE_RUN_FROM_PACKAGE", "1" } };
            SetupUpdateAppSettingsResponse(true);

            // Act
            var result = await _plugin.RepairRunFromPackageConfigurationAsync(TestResourceId, RunFromPackageRepairAction.SetToOne);

            // Assert
            Assert.True(result.IsSuccessful);
            Assert.Equal(RunFromPackageRepairAction.SetToOne, result.RepairAction);
            Assert.Equal("1", result.NewValue);
        }

        [Fact]
        public async Task RepairRunFromPackageConfigurationAsync_RemoveSetting_ReturnsSuccessfulResult()
        {
            // Arrange
            var appSettings = new Dictionary<string, string>();
            SetupUpdateAppSettingsResponse(true);

            // Act
            var result = await _plugin.RepairRunFromPackageConfigurationAsync(TestResourceId, RunFromPackageRepairAction.RemoveSetting);

            // Assert
            Assert.True(result.IsSuccessful);
            Assert.Equal(RunFromPackageRepairAction.RemoveSetting, result.RepairAction);
            Assert.Equal("", result.NewValue);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Sets up HTTP response for mocked HTTP calls to ARM API
        /// </summary>
        private void SetupArmApiResponse(string apiPath, HttpStatusCode statusCode, string responseContent)
        {
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseContent, System.Text.Encoding.UTF8, "application/json")
            };

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.PathAndQuery.Contains(apiPath)),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(response);
        }

        /// <summary>
        /// Sets up HTTP response for app settings API call
        /// </summary>
        private void SetupAppSettingsResponse(string appSettingsJson)
        {
            SetupArmApiResponse("/config/appSettings/list", HttpStatusCode.OK, appSettingsJson);
        }

        /// <summary>
        /// Sets up HTTP response for resource details API call (for OS detection)
        /// </summary>
        private void SetupResourceDetailsResponse(string osType = "Windows")
        {
            var resourceDetails = $@"{{
                ""properties"": {{
                    ""siteConfig"": {{
                        ""linuxFxVersion"": ""{(osType == "Linux" ? "DOCKER|someimage" : "")}""
                    }},
                    ""kind"": ""functionapp"",
                    ""serverFarmId"": ""/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/serverfarms/test-plan""
                }}
            }}";

            // Set up mock for resource details call (for OS detection and App Service Plan ID)
            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.RequestUri!.ToString().Contains(TestResourceId) &&
                        req.RequestUri!.ToString().Contains("?api-version=2022-03-01") &&
                        !req.RequestUri!.ToString().Contains("config") &&
                        !req.RequestUri!.ToString().Contains("serverfarms")),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(resourceDetails, System.Text.Encoding.UTF8, "application/json")
                });
        }

        /// <summary>
        /// Sets up HTTP response for App Service Plan detection
        /// </summary>
        private void SetupAppServicePlanResponse(string skuName = "Consumption")
        {
            // Mock the SKU details response for App Service Plan
            var skuDetails = $@"{{
                ""sku"": {{
                    ""name"": ""{skuName}""
                }}
            }}";

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.RequestUri!.ToString().Contains("/serverfarms/test-plan") &&
                        req.RequestUri!.ToString().Contains("?api-version=2022-03-01")),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(skuDetails, System.Text.Encoding.UTF8, "application/json")
                });
        }

        /// <summary>
        /// Sets up HTTP response for app settings update API call
        /// </summary>
        private void SetupUpdateAppSettingsResponse(bool success = true)
        {
            // Mock the POST request for GetAppSettings (used by RemoveSetting) - api-version=2022-03-01
            var existingSettingsForGet = @"{
                ""properties"": {
                    ""WEBSITE_RUN_FROM_PACKAGE"": ""existing-value"",
                    ""AzureWebJobsStorage"": ""DefaultEndpointsProtocol=https;AccountName=test;AccountKey=fake-key;EndpointSuffix=core.windows.net""
                }
            }";

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Post &&
                        req.RequestUri!.ToString().Contains("/config/appSettings/list") &&
                        req.RequestUri!.ToString().Contains("api-version=2022-03-01")),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(existingSettingsForGet, System.Text.Encoding.UTF8, "application/json")
                });

            // Mock the POST request for UpdateAppSettingsAsync (get existing settings) - api-version=2024-04-01
            var existingSettingsForUpdate = @"{
                ""properties"": {
                    ""WEBSITE_RUN_FROM_PACKAGE"": ""existing-value"",
                    ""AzureWebJobsStorage"": ""DefaultEndpointsProtocol=https;AccountName=test;AccountKey=fake-key;EndpointSuffix=core.windows.net""
                }
            }";

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Post &&
                        req.RequestUri!.ToString().Contains("/config/appsettings/list") &&
                        req.RequestUri!.ToString().Contains("api-version=2024-04-01")),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(existingSettingsForUpdate, System.Text.Encoding.UTF8, "application/json")
                });

            // Mock the PUT request to update app settings (UpdateAppSettingsAsync) - api-version=2024-04-01
            var statusCode = success ? HttpStatusCode.OK : HttpStatusCode.BadRequest;
            var response = success ? @"{""properties"":{""WEBSITE_RUN_FROM_PACKAGE"":""1""}}" : @"{""error"":""Update failed""}";

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Put &&
                        req.RequestUri!.ToString().Contains("/config/appsettings") &&
                        req.RequestUri!.ToString().Contains("api-version=2024-04-01") &&
                        !req.RequestUri!.ToString().Contains("/config/appsettings/list")),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(response, System.Text.Encoding.UTF8, "application/json")
                });
        }

        [Fact]
        public async Task GetSkuViaHttpAsync_ShouldReturnTierOverName_WhenBothPresent()
        {
            // Arrange
            const string testResourceId = "/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/sites/test-function-app";
            const string testAppServicePlanId = "/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/serverfarms/test-plan";

            // Setup resource details response to return the App Service Plan ID
            var resourceDetails = $@"{{
                ""properties"": {{
                    ""serverFarmId"": ""{testAppServicePlanId}""
                }}
            }}";

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.RequestUri!.ToString().Contains(testResourceId) &&
                        req.RequestUri!.ToString().Contains("?api-version=2022-03-01")),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(resourceDetails, System.Text.Encoding.UTF8, "application/json")
                });

            // Setup App Service Plan response with both name and tier
            var skuDetails = @"{
                ""sku"": {
                    ""name"": ""EP1"",
                    ""tier"": ""ElasticPremium"",
                    ""size"": ""EP1"",
                    ""family"": ""EP"",
                    ""capacity"": 1
                }
            }";

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.RequestUri!.ToString().Contains(testAppServicePlanId) &&
                        req.RequestUri!.ToString().Contains("?api-version=2022-03-01")),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(skuDetails, System.Text.Encoding.UTF8, "application/json")
                });

            // Act
            var result = await InvokePrivateMethod<Task<string>>(_plugin, "GetSkuViaHttpAsync", testResourceId);

            // Assert
            Assert.Equal("ElasticPremium", result); // Should return tier, not name
        }

        [Fact]
        public async Task GetSkuViaHttpAsync_ShouldReturnName_WhenTierNotPresent()
        {
            // Arrange
            const string testResourceId = "/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/sites/test-function-app";
            const string testAppServicePlanId = "/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/serverfarms/test-plan";

            // Setup resource details response
            var resourceDetails = $@"{{
                ""properties"": {{
                    ""serverFarmId"": ""{testAppServicePlanId}""
                }}
            }}";

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.RequestUri!.ToString().Contains(testResourceId) &&
                        req.RequestUri!.ToString().Contains("?api-version=2022-03-01")),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(resourceDetails, System.Text.Encoding.UTF8, "application/json")
                });

            // Setup App Service Plan response with only name (no tier)
            var skuDetails = @"{
                ""sku"": {
                    ""name"": ""B1"",
                    ""size"": ""B1"",
                    ""family"": ""B"",
                    ""capacity"": 1
                }
            }";

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.RequestUri!.ToString().Contains(testAppServicePlanId) &&
                        req.RequestUri!.ToString().Contains("?api-version=2022-03-01")),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(skuDetails, System.Text.Encoding.UTF8, "application/json")
                });

            // Act
            var result = await InvokePrivateMethod<Task<string>>(_plugin, "GetSkuViaHttpAsync", testResourceId);

            // Assert
            Assert.Equal("B1", result); // Should fallback to name when tier is not available
        }

        /// <summary>
        /// Invokes a private method on an object using reflection
        /// </summary>
        private T InvokePrivateMethod<T>(object obj, string methodName, params object?[] parameters)
        {
            var method = obj.GetType().GetMethod(methodName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (method == null)
            {
                throw new ArgumentException($"Method '{methodName}' not found on type '{obj.GetType().Name}'");
            }

            var result = method.Invoke(obj, parameters);
            return result != null ? (T)result : default(T)!;
        }

        #endregion

        #region Runtime Detection Tests

        [Fact]
        public void DetectFunctionAppRuntime_DotNetIsolatedPackage_ReturnsCorrectRuntime()
        {
            // Arrange
            var fileNames = new List<string>
            {
                "host.json",
                "MyApp.dll",
                "MyApp.deps.json",
                "MyApp.runtimeconfig.json",
                "Microsoft.Azure.Functions.Worker.dll",
                "System.Text.Json.dll"
            };

            // Act
            var runtime = InvokePrivateMethod<string>(_plugin, "DetectFunctionAppRuntime", fileNames);

            // Assert
            Assert.Equal(".NET Isolated", runtime);
        }

        [Fact]
        public void DetectFunctionAppRuntime_DotNetInProcessPackage_ReturnsCorrectRuntime()
        {
            // Arrange
            var fileNames = new List<string>
            {
                "host.json",
                "MyApp.dll",
                "MyApp.deps.json",
                "Microsoft.Azure.WebJobs.dll",
                "Microsoft.Azure.Functions.Extensions.dll"
            };

            // Act
            var runtime = InvokePrivateMethod<string>(_plugin, "DetectFunctionAppRuntime", fileNames);

            // Assert
            Assert.Equal(".NET In-Process", runtime);
        }

        [Fact]
        public void DetectFunctionAppRuntime_NodeJsPackage_ReturnsCorrectRuntime()
        {
            // Arrange
            var fileNames = new List<string>
            {
                "host.json",
                "package.json",
                "index.js",
                "HttpTrigger/function.json",
                "HttpTrigger/index.js"
            };

            // Act
            var runtime = InvokePrivateMethod<string>(_plugin, "DetectFunctionAppRuntime", fileNames);

            // Assert
            Assert.Equal("Node.js", runtime);
        }

        #endregion

        #region Validation Tests

        [Fact]
        public void ValidateStructure_DotNetIsolatedPackage_ReturnsValidStructure()
        {
            // Arrange
            var fileNames = new List<string>
            {
                "host.json",
                "MyApp.dll",
                "MyApp.deps.json",
                "MyApp.runtimeconfig.json",
                "Microsoft.Azure.Functions.Worker.dll"
            };
            var rootFiles = new List<string>
            {
                "host.json",
                "MyApp.dll",
                "MyApp.deps.json",
                "MyApp.runtimeconfig.json",
                "Microsoft.Azure.Functions.Worker.dll"
            };
            var functionFolders = new List<string>(); // No function folders for .NET isolated

            var report = new PackageStructureReport();

            // Set the detected runtime first using the detection method
            report.DetectedRuntime = InvokePrivateMethod<string>(_plugin, "DetectFunctionAppRuntime", fileNames);

            // Act
            InvokePrivateMethod<object>(_plugin, "ValidateStructure", fileNames, rootFiles, functionFolders, report);

            // Assert
            Assert.True(report.HasValidStructure, "Package structure should be valid for .NET isolated Functions");
            Assert.True(report.HasHostJson, "Should detect host.json");
            Assert.Equal(".NET Isolated", report.DetectedRuntime);
            Assert.DoesNotContain(report.StructureIssues, issue => issue.Contains("No function folders detected") && !issue.Contains("This is expected"));
        }

        [Fact]
        public void ValidateStructure_TraditionalFunctionPackage_RequiresFunctionFolders()
        {
            // Arrange
            var fileNames = new List<string>
            {
                "host.json",
                "HttpTrigger/function.json",
                "HttpTrigger/index.js"
            };
            var rootFiles = new List<string>
            {
                "host.json"
            };
            var functionFolders = new List<string>
            {
                "HttpTrigger"
            };

            var report = new PackageStructureReport();

            // Act
            InvokePrivateMethod<object>(_plugin, "ValidateStructure", fileNames, rootFiles, functionFolders, report);

            // Assert
            Assert.True(report.HasValidStructure, "Package structure should be valid for traditional Functions");
            Assert.True(report.HasHostJson, "Should detect host.json");
            Assert.Single(functionFolders);
        }

        [Fact]
        public void ValidateStructure_DotNetIsolatedWithoutDlls_ReturnsInvalidStructure()
        {
            // Arrange
            var fileNames = new List<string>
            {
                "host.json",
                "MyApp.deps.json",
                "MyApp.runtimeconfig.json"
            };
            var rootFiles = new List<string>
            {
                "host.json",
                "MyApp.deps.json",
                "MyApp.runtimeconfig.json"
            };
            var functionFolders = new List<string>();

            var report = new PackageStructureReport();

            // Act
            InvokePrivateMethod<object>(_plugin, "ValidateStructure", fileNames, rootFiles, functionFolders, report);

            // Assert
            Assert.False(report.HasValidStructure, "Package structure should be invalid without DLLs for .NET isolated Functions");
        }

        #endregion
    }
}
