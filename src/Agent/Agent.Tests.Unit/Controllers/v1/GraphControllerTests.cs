// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Agent.Plugins.Services.Interfaces;
using Agent.Web.Controllers.v1;
using Agent.Web.Models.ExtendedAgents.Response;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Agent.Tests.Unit.Controllers.v1
{
    public class GraphControllerTests
    {
        private readonly Mock<IGraphService> _mockGraphService;
        private readonly GraphController _controller;

        public GraphControllerTests()
        {
            _mockGraphService = new Mock<IGraphService>();
            _controller = new GraphController(_mockGraphService.Object);
        }

        #region SearchResources Tests

        [Fact]
        public async Task SearchResources_WithValidParameters_ReturnsOkWithPaginatedResults()
        {
            // Arrange
            var resources = new List<ResourceSearchResult>
            {
                new() {
                    ResourceId = "/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Web/sites/webapp1",
                    Name = "webapp1",
                    Type = "microsoft.web/sites",
                    Kind = "app",
                    SubscriptionId = "sub1",
                    ResourceGroup = "rg1",
                    Location = "eastus"
                },
                new() {
                    ResourceId = "/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Web/sites/webapp2",
                    Name = "webapp2",
                    Type = "microsoft.web/sites",
                    Kind = "app",
                    SubscriptionId = "sub1",
                    ResourceGroup = "rg1",
                    Location = "eastus"
                }
            };

            _mockGraphService
                .Setup(x => x.SearchResourcesAsync("app", null, null, 0, 20))
                .ReturnsAsync((resources, 2));

            var result = await _controller.SearchResources("app", null, null, 0, 20);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<PaginatedResponse<ResourceSearchResult>>(okResult.Value);

            Assert.Equal(2, response.Data.Count);
            Assert.Equal(0, response.PageIndex);
            Assert.Equal(20, response.PageSize);
            Assert.Equal(2, response.TotalCount);
            Assert.Equal(1, response.TotalPages);
            Assert.False(response.HasPreviousPage);
            Assert.False(response.HasNextPage);
        }

        [Fact]
        public async Task SearchResources_WithNameFilter_ReturnsFilteredResults()
        {
            var resources = new List<ResourceSearchResult>
            {
                new() {
                    ResourceId = "/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Web/sites/myapp",
                    Name = "myapp",
                    Type = "microsoft.web/sites"
                }
            };

            _mockGraphService
                .Setup(x => x.SearchResourcesAsync("myapp", null, null, 0, 20))
                .ReturnsAsync((resources, 1));

            var result = await _controller.SearchResources("myapp", null, null, 0, 20);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<PaginatedResponse<ResourceSearchResult>>(okResult.Value);

            Assert.Single(response.Data);
            Assert.Equal("myapp", response.Data[0].Name);
        }

        [Fact]
        public async Task SearchResources_WithTypeFilter_ReturnsFilteredResults()
        {
            var resources = new List<ResourceSearchResult>
            {
                new() {
                    ResourceId = "/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.App/containerApps/app1",
                    Name = "app1",
                    Type = "microsoft.app/containerapps"
                }
            };

            _mockGraphService
                .Setup(x => x.SearchResourcesAsync(null, "microsoft.app/containerapps", null, 0, 20))
                .ReturnsAsync((resources, 1));

            var result = await _controller.SearchResources(null, "microsoft.app/containerapps", null, 0, 20);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<PaginatedResponse<ResourceSearchResult>>(okResult.Value);

            Assert.Single(response.Data);
            Assert.Equal("microsoft.app/containerapps", response.Data[0].Type);
        }

        [Fact]
        public async Task SearchResources_WithSubscriptionFilter_ReturnsFilteredResults()
        {
            var validSubscriptionId = "12345678-1234-1234-1234-123456789abc";
            var resources = new List<ResourceSearchResult>
            {
                new() {
                    ResourceId = $"/subscriptions/{validSubscriptionId}/resourceGroups/rg1/providers/Microsoft.Web/sites/app1",
                    Name = "app1",
                    Type = "microsoft.web/sites",
                    SubscriptionId = validSubscriptionId
                }
            };

            _mockGraphService
                .Setup(x => x.SearchResourcesAsync(null, null, validSubscriptionId, 0, 20))
                .ReturnsAsync((resources, 1));

            var result = await _controller.SearchResources(null, null, validSubscriptionId, 0, 20);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<PaginatedResponse<ResourceSearchResult>>(okResult.Value);

            Assert.Single(response.Data);
            Assert.Equal(validSubscriptionId, response.Data[0].SubscriptionId);
        }

        [Fact]
        public async Task SearchResources_WithPagination_ReturnsCorrectPage()
        {
            var resources = new List<ResourceSearchResult>
            {
                new() {
                    ResourceId = "/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Web/sites/app3",
                    Name = "app3",
                    Type = "microsoft.web/sites"
                },
                new() {
                    ResourceId = "/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Web/sites/app4",
                    Name = "app4",
                    Type = "microsoft.web/sites"
                }
            };

            _mockGraphService
                .Setup(x => x.SearchResourcesAsync(null, null, null, 1, 2))
                .ReturnsAsync((resources, 10)); // Total count is 10

            var result = await _controller.SearchResources(null, null, null, 1, 2);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<PaginatedResponse<ResourceSearchResult>>(okResult.Value);

            Assert.Equal(2, response.Data.Count);
            Assert.Equal(1, response.PageIndex);
            Assert.Equal(2, response.PageSize);
            Assert.Equal(10, response.TotalCount);
            Assert.Equal(5, response.TotalPages);
            Assert.True(response.HasPreviousPage);
            Assert.True(response.HasNextPage);
        }

        [Fact]
        public async Task SearchResources_WithNegativePageIndex_ReturnsBadRequest()
        {
            var result = await _controller.SearchResources(null, null, null, -1, 20);

            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("pageIndex must be greater than or equal to 0", badRequestResult.Value);
        }

        [Fact]
        public async Task SearchResources_WithZeroPageSize_ReturnsBadRequest()
        {
            var result = await _controller.SearchResources(null, null, null, 0, 0);

            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("pageSize must be between 1 and 100", badRequestResult.Value);
        }

        [Fact]
        public async Task SearchResources_WithPageSizeOver100_ReturnsBadRequest()
        {
            var result = await _controller.SearchResources(null, null, null, 0, 101);

            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("pageSize must be between 1 and 100", badRequestResult.Value);
        }

        [Fact]
        public async Task SearchResources_WithEmptyResults_ReturnsEmptyPaginatedResponse()
        {
            var emptyResources = new List<ResourceSearchResult>();

            _mockGraphService
                .Setup(x => x.SearchResourcesAsync("nonexistent", null, null, 0, 20))
                .ReturnsAsync((emptyResources, 0));

            var result = await _controller.SearchResources("nonexistent", null, null, 0, 20);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<PaginatedResponse<ResourceSearchResult>>(okResult.Value);

            Assert.Empty(response.Data);
            Assert.Equal(0, response.TotalCount);
            Assert.Equal(0, response.TotalPages);
            Assert.False(response.HasPreviousPage);
            Assert.False(response.HasNextPage);
        }

        [Fact]
        public async Task SearchResources_WithMultipleFilters_ReturnsFilteredResults()
        {
            var validSubscriptionId = "abcdef12-3456-7890-abcd-ef1234567890";
            var resources = new List<ResourceSearchResult>
            {
                new() {
                    ResourceId = $"/subscriptions/{validSubscriptionId}/resourceGroups/rg1/providers/Microsoft.Web/sites/webapp1",
                    Name = "webapp1",
                    Type = "microsoft.web/sites",
                    SubscriptionId = validSubscriptionId
                }
            };

            _mockGraphService
                .Setup(x => x.SearchResourcesAsync("web", "microsoft.web/sites", validSubscriptionId, 0, 20))
                .ReturnsAsync((resources, 1));

            var result = await _controller.SearchResources("web", "microsoft.web/sites", validSubscriptionId, 0, 20);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<PaginatedResponse<ResourceSearchResult>>(okResult.Value);

            Assert.Single(response.Data);
            Assert.Contains("web", response.Data[0].Name);
            Assert.Equal("microsoft.web/sites", response.Data[0].Type);
            Assert.Equal(validSubscriptionId, response.Data[0].SubscriptionId);
        }

        [Fact]
        public async Task SearchResources_WithMaxPageSize_ReturnsUpTo100Results()
        {
            var resources = Enumerable.Range(1, 100)
                .Select(i => new ResourceSearchResult
                {
                    ResourceId = $"/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Web/sites/app{i}",
                    Name = $"app{i}",
                    Type = "microsoft.web/sites"
                })
                .ToList();

            _mockGraphService
                .Setup(x => x.SearchResourcesAsync(null, null, null, 0, 100))
                .ReturnsAsync((resources, 100));

            var result = await _controller.SearchResources(null, null, null, 0, 100);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<PaginatedResponse<ResourceSearchResult>>(okResult.Value);

            Assert.Equal(100, response.Data.Count);
            Assert.Equal(100, response.PageSize);
        }

        [Fact]
        public async Task SearchResources_LastPage_HasNoPreviousPageSet()
        {
            var resources = new List<ResourceSearchResult>
            {
                new() {
                    ResourceId = "/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Web/sites/app10",
                    Name = "app10",
                    Type = "microsoft.web/sites"
                }
            };

            _mockGraphService
                .Setup(x => x.SearchResourcesAsync(null, null, null, 4, 2))
                .ReturnsAsync((resources, 9)); // Total 9 items, page 4 (last page)

            var result = await _controller.SearchResources(null, null, null, 4, 2);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<PaginatedResponse<ResourceSearchResult>>(okResult.Value);

            Assert.Equal(4, response.PageIndex);
            Assert.Equal(5, response.TotalPages);
            Assert.True(response.HasPreviousPage);
            Assert.False(response.HasNextPage); // Last page
        }

        [Fact]
        public async Task SearchResources_WithInjectionAttemptInSubscriptionId_ReturnsBadRequest()
        {
            // Arrange - Malicious input attempting Gremlin injection
            string maliciousSubscriptionId = "sub1').drop().constant('x";

            var result = await _controller.SearchResources(null, null, maliciousSubscriptionId, 0, 20);

            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.NotNull(badRequestResult.Value);
            Assert.Contains("must be a valid GUID format", badRequestResult.Value?.ToString() ?? "");
        }

        [Fact]
        public async Task SearchResources_WithInjectionAttemptInType_ReturnsBadRequest()
        {
            // Arrange - Malicious input with single quotes attempting injection
            string maliciousType = "microsoft.web/sites') OR has('malicious', 'value";

            var result = await _controller.SearchResources(null, maliciousType, null, 0, 20);

            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.NotNull(badRequestResult.Value);
            Assert.Contains("invalid characters", badRequestResult.Value?.ToString() ?? "");
        }

        [Fact]
        public async Task SearchResources_WithExcessivelyLongName_ReturnsBadRequest()
        {
            string longName = new string('a', 257);

            var result = await _controller.SearchResources(longName, null, null, 0, 20);

            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("name parameter must be 256 characters or less", badRequestResult.Value);
        }

        [Fact]
        public async Task SearchResources_WithExcessivelyLongType_ReturnsBadRequest()
        {
            string longType = new string('a', 257);

            var result = await _controller.SearchResources(null, longType, null, 0, 20);

            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("type parameter must be 256 characters or less", badRequestResult.Value);
        }

        [Fact]
        public async Task SearchResources_WithExcessivelyLongSubscriptionId_ReturnsBadRequest()
        {
            string longSubId = new string('a', 257);

            var result = await _controller.SearchResources(null, null, longSubId, 0, 20);

            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("subscriptionId parameter must be 256 characters or less", badRequestResult.Value);
        }

        [Fact]
        public async Task SearchResources_WithValidGuidSubscriptionId_Succeeds()
        {
            string validGuid = "12345678-1234-1234-1234-123456789abc";
            var resources = new List<ResourceSearchResult>();

            _mockGraphService
                .Setup(x => x.SearchResourcesAsync(null, null, validGuid, 0, 20))
                .ReturnsAsync((resources, 0));

            var result = await _controller.SearchResources(null, null, validGuid, 0, 20);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.NotNull(okResult.Value);
        }

        [Fact]
        public async Task SearchResources_WithInvalidSubscriptionIdFormat_ReturnsBadRequest()
        {
            string invalidGuid = "not-a-guid";

            var result = await _controller.SearchResources(null, null, invalidGuid, 0, 20);

            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.NotNull(badRequestResult.Value);
            Assert.Contains("must be a valid GUID format", badRequestResult.Value?.ToString() ?? "");
        }

        [Fact]
        public async Task SearchResources_WithValidTypeFormat_Succeeds()
        {
            string validType = "microsoft.web/sites";
            var resources = new List<ResourceSearchResult>();

            _mockGraphService
                .Setup(x => x.SearchResourcesAsync(null, validType, null, 0, 20))
                .ReturnsAsync((resources, 0));

            var result = await _controller.SearchResources(null, validType, null, 0, 20);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.NotNull(okResult.Value);
        }

        #endregion
    }
}
