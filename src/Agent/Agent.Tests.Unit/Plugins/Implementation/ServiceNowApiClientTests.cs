// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Agent.Core.Configuration;
using Agent.Core.Models.ServiceNow;
using Agent.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Agent.Tests.Unit.Plugins.Implementation
{
    public class ServiceNowApiClientTests
    {
        private readonly Mock<ILogger<ServiceNowAPIClient>> _mockLogger;
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly HttpClient _httpClient;
        private readonly IncidentManagementSettings _validSettings;

        public ServiceNowApiClientTests()
        {
            _mockLogger = new Mock<ILogger<ServiceNowAPIClient>>();
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockHttpMessageHandler.Object);

            _validSettings = new IncidentManagementSettings
            {
                Type = IncidentManagementType.ServiceNow,
                ConnectionUrl = "https://test.service-now.com",
                ConnectionKey = JsonSerializer.Serialize(new ServiceNowAPISettings
                {
                    Username = "testuser",
                    Password = "testpass"
                })
            };
        }



        [Fact]
        public async Task GetIncidentAsync_WithValidIncidentId_ReturnsIncident()
        {
            // Arrange
            var incidentId = "test-incident-123";
            var expectedIncident = new ServiceNowIncident
            {
                IncidentId = incidentId,
                Number = "INC0001234",
                Title = "Test Incident",
                Description = "Test incident description",
                State = "New",
                Priority = "2"
            };

            var responseData = new ServiceNowResponse<ServiceNowIncident>
            {
                Result = expectedIncident
            };

            var jsonResponse = JsonSerializer.Serialize(responseData);
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
            };

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            var client = new ServiceNowAPIClient(_httpClient, _mockLogger.Object, _validSettings);

            // Act
            var result = await client.GetIncidentAsync(incidentId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedIncident.IncidentId, result.IncidentId);
            Assert.Equal(expectedIncident.Number, result.Number);
            Assert.Equal(expectedIncident.Title, result.Title);
        }

        [Fact]
        public async Task GetIncidentAsync_WithEmptyIncidentId_ThrowsArgumentException()
        {
            // Arrange
            var client = new ServiceNowAPIClient(_httpClient, _mockLogger.Object, _validSettings);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                client.GetIncidentAsync(string.Empty));
        }

        [Fact]
        public async Task GetIncidentsAsync_WithValidParameters_ReturnsIncidentList()
        {
            // Arrange
            var incidents = new List<ServiceNowIncident>
            {
                new ServiceNowIncident { IncidentId = "inc1", Number = "INC001", Title = "Incident 1" },
                new ServiceNowIncident { IncidentId = "inc2", Number = "INC002", Title = "Incident 2" }
            };

            var responseData = new ServiceNowListResponse<ServiceNowIncident>
            {
                Result = incidents
            };

            var jsonResponse = JsonSerializer.Serialize(responseData);
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
            };

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            var client = new ServiceNowAPIClient(_httpClient, _mockLogger.Object, _validSettings);

            // Act
            var result = await client.GetIncidentsAsync(10, 0, null, null, null);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Equal("inc1", result[0].IncidentId);
            Assert.Equal("inc2", result[1].IncidentId);
        }

        [Fact]
        public async Task GetIncidentsAsync_WithNotFoundResponse_ReturnsEmptyList()
        {
            // Arrange
            var httpResponse = new HttpResponseMessage(HttpStatusCode.NotFound);

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            var client = new ServiceNowAPIClient(_httpClient, _mockLogger.Object, _validSettings);

            // Act
            var result = await client.GetIncidentsAsync(10, 0, null, null, null);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task PostDiscussionEntryAsync_WithValidParameters_ReturnsSuccessMessage()
        {
            // Arrange
            var incidentId = "test-incident-123";
            var discussionEntry = "This is a test discussion entry";
            var expectedResponse = "SUCCESS";

            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(expectedResponse, Encoding.UTF8, "application/json")
            };

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            var client = new ServiceNowAPIClient(_httpClient, _mockLogger.Object, _validSettings);

            // Act
            var result = await client.PostDiscussionEntryAsync(incidentId, discussionEntry);

            // Assert
            Assert.Equal(expectedResponse, result);
        }


        [Fact]
        public async Task ResolveIncidentAsync_WithValidParameters_ReturnsSuccessMessage()
        {
            // Arrange
            var incidentId = "test-incident-123";
            var resolutionNotes = "Issue resolved successfully";
            var expectedResponse = "SUCCESS";

            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(expectedResponse, Encoding.UTF8, "application/json")
            };

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            var client = new ServiceNowAPIClient(_httpClient, _mockLogger.Object, _validSettings);

            // Act
            var result = await client.ResolveIncidentAsync(incidentId, resolutionNotes);

            // Assert
            Assert.Equal(expectedResponse, result);
        }

        [Fact]
        public async Task ChangePriorityAsync_WithValidParameters_ReturnsSuccessMessage()
        {
            // Arrange
            var incidentId = "test-incident-123";
            var priority = 1;
            var discussionEntry = "Priority changed to high";
            var expectedResponse = "SUCCESS";

            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(expectedResponse, Encoding.UTF8, "application/json")
            };

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            var client = new ServiceNowAPIClient(_httpClient, _mockLogger.Object, _validSettings);

            // Act
            var result = await client.ChangePriorityAsync(incidentId, priority, discussionEntry);

            // Assert
            Assert.Equal(expectedResponse, result);
        }

        [Fact]
        public async Task GetIncidentDiscussionEntriesAsync_WithValidIncidentId_ReturnsDiscussionEntries()
        {
            // Arrange
            var incidentId = "test-incident-123";
            var discussionEntries = new List<ServiceNowDiscussionEntry>
            {
                new ServiceNowDiscussionEntry { Text = "First comment", Date = DateTime.UtcNow.AddDays(-1) },
                new ServiceNowDiscussionEntry { Text = "Second comment", Date = DateTime.UtcNow }
            };

            var responseData = new ServiceNowListResponse<ServiceNowDiscussionEntry>
            {
                Result = discussionEntries
            };

            var jsonResponse = JsonSerializer.Serialize(responseData);
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
            };

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            var client = new ServiceNowAPIClient(_httpClient, _mockLogger.Object, _validSettings);

            // Act
            var result = await client.GetIncidentDiscussionEntriesAsync(incidentId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Equal("First comment", result[0].Text);
            Assert.Equal("Second comment", result[1].Text);
        }

        [Fact]
        public async Task GetIncidentDiscussionEntriesAsync_WithHttpError_ReturnsEmptyList()
        {
            // Arrange
            var incidentId = "test-incident-123";

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Network error"));

            var client = new ServiceNowAPIClient(_httpClient, _mockLogger.Object, _validSettings);

            // Act
            var result = await client.GetIncidentDiscussionEntriesAsync(incidentId);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }


    }
}
