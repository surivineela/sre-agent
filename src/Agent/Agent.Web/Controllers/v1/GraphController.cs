// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;
using Agent.Core.Models.Api.v1;
using Agent.Plugins.Services.Interfaces;
using Agent.Web.Authorization;
using Agent.Web.Models.ExtendedAgents.Response;
using Gremlin.Net.Driver;
using Microsoft.AspNetCore.Mvc;
using ArmOperations = Agent.Core.Constants.ArmOperations;

namespace Agent.Web.Controllers.v1
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class GraphController : ControllerBase
    {
        private readonly IGraphService _graphService;

        public GraphController(IGraphService graphService)
        {
            _graphService = graphService;
        }

        /// <summary>
        /// This is just a temporary solution to query graph nodes and edges
        /// </summary>
        /// <param name="request">requests that has query string and optional max message size</param>
        /// <returns>graph data array</returns>
        [HttpPost]
        [AuthorizeArmOperation(ArmOperations.AgentGraphReadActionId)]
        public async Task<ActionResult<ResultSet<dynamic>>> Query([FromBody] GraphQueryRequest request)
        {
            return await _graphService.QueryAsync(request.Query);
        }

        /// <summary>
        /// Returns a list of subscriptions
        /// </summary>
        /// <returns>list of subscriptions</returns>
        [HttpGet("subscriptions")]
        [AuthorizeArmOperation(ArmOperations.AgentGraphReadActionId)]
        public async Task<ActionResult<ResultSet<dynamic>>> QuerySubscriptions()
        {
            var result = await _graphService.QuerySubscriptionsAsync();
            return Ok(result);
        }

        /// <summary>
        /// Returns a list of all available resource types in the graph
        /// </summary>
        /// <returns>List of resource types</returns>
        [HttpGet("resourceTypes")]
        [AuthorizeArmOperation(ArmOperations.AgentGraphReadActionId)]
        public async Task<ActionResult<IEnumerable<string>>> GetResourceTypes()
        {
            var result = await _graphService.GetResourceTypesAsync();
            return Ok(result);
        }

        /// <summary>
        /// Gets all app groups for a specific subscription
        /// </summary>
        /// <param name="subId">The subscription ID</param>
        /// <param name="resourceType">Optional resource type to filter app groups</param>
        /// <returns>List of app groups</returns>
        [HttpGet("{subId}/appGroups")]
        [AuthorizeArmOperation(ArmOperations.AgentGraphReadActionId)]
        public async Task<ActionResult<ResultSet<dynamic>>> GetAppGroupsBySubscription(string subId, [FromQuery] string? resourceType = null)
        {
            var result = await _graphService.GetAppGroupsBySubscriptionAsync(subId, resourceType);
            return Ok(result);
        }

        /// <summary>
        /// Gets detailed information about a specific app group
        /// </summary>
        /// <param name="subscriptionId">The subscription ID</param>
        /// <param name="resourceId">The app group ID is the app group root resource id</param>
        /// <returns>App group details</returns>
        [HttpGet("{subscriptionId}/appGroups/{appGroupId}")]
        [AuthorizeArmOperation(ArmOperations.AgentGraphReadActionId)]
        public async Task<ActionResult<ResultSet<AppGroupItem>>> GetAppGroupResources(string subscriptionId, string appGroupId)
        {
            var result = await _graphService.GetAppGroupResourcesAsync(appGroupId);
            return Ok(result);
        }

        /// <summary>
        /// Gets detailed information about a specific resource
        /// <param name="subscriptionId">The subscription ID</param>
        /// <param name="resourceId">The app group ID is the app group root resource id</param>
        /// <returns>App group details</returns>
        [HttpGet("resource/{resourceId}")]
        [AuthorizeArmOperation(ArmOperations.AgentGraphReadActionId)]
        public async Task<ActionResult<ResultSet<AppGroupItem>>> GetResource(string resourceId)
        {
            var result = await _graphService.GetGraphResourceAsync(resourceId);
            return Ok(result);
        }

        /// <summary>
        /// Returns current status of the resource graph generation
        /// Will return either a completed status or an estimated percentage of completion.
        /// </summary>
        /// <returns></returns>
        [HttpGet("progress")]
        [AuthorizeArmOperation(ArmOperations.AgentGraphReadActionId)]
        public async Task<ActionResult<ResultSet<dynamic>>> GetGraphProgressAsync()
        {
            var result = await _graphService.GetGraphProgressAsync();
            return Ok(result);
        }

        /// <summary>
        /// Search for resources by name and/or type with pagination
        /// </summary>
        /// <param name="name">Optional resource name filter (case-insensitive partial match)</param>
        /// <param name="type">Optional resource type filter (e.g., Microsoft.Web/sites, Microsoft.App/containerApps)</param>
        /// <param name="subscriptionId">Optional subscription ID filter</param>
        /// <param name="pageIndex">Zero-based page index (default: 0)</param>
        /// <param name="pageSize">Number of items per page (default: 20, max: 100)</param>
        /// <returns>Paginated list of matching resources</returns>
        [HttpGet("resources/search")]
        [AuthorizeArmOperation(ArmOperations.AgentGraphReadActionId)]
        public async Task<ActionResult<PaginatedResponse<ResourceSearchResult>>> SearchResources(
            [FromQuery] string? name = null,
            [FromQuery] string? type = null,
            [FromQuery] string? subscriptionId = null,
            [FromQuery] int pageIndex = 0,
            [FromQuery] int pageSize = 20)
        {
            // Validate pagination parameters
            if (pageIndex < 0)
            {
                return BadRequest("pageIndex must be greater than or equal to 0");
            }

            if (pageSize <= 0 || pageSize > 100)
            {
                return BadRequest("pageSize must be between 1 and 100");
            }

            // Validate input length to prevent DoS attacks
            if (name != null && name.Length > 256)
            {
                return BadRequest("name parameter must be 256 characters or less");
            }

            if (type != null && type.Length > 256)
            {
                return BadRequest("type parameter must be 256 characters or less");
            }

            if (subscriptionId != null && subscriptionId.Length > 256)
            {
                return BadRequest("subscriptionId parameter must be 256 characters or less");
            }

            // Validate subscriptionId format if provided (basic GUID validation)
            if (!string.IsNullOrWhiteSpace(subscriptionId) &&
                !System.Text.RegularExpressions.Regex.IsMatch(subscriptionId, @"^[a-fA-F0-9\-]{36}$"))
            {
                return BadRequest("subscriptionId must be a valid GUID format");
            }

            // Validate type format if provided (alphanumeric, dots, slashes, hyphens)
            if (!string.IsNullOrWhiteSpace(type) &&
                !System.Text.RegularExpressions.Regex.IsMatch(type, @"^[a-zA-Z0-9\.\/\-]+$"))
            {
                return BadRequest("type parameter contains invalid characters. Only alphanumeric, dots, slashes, and hyphens are allowed");
            }

            var (resources, totalCount) = await _graphService.SearchResourcesAsync(
                name,
                type,
                subscriptionId,
                pageIndex,
                pageSize);

            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var response = new PaginatedResponse<ResourceSearchResult>
            {
                Data = resources,
                PageIndex = pageIndex,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
                HasPreviousPage = pageIndex > 0,
                HasNextPage = pageIndex < totalPages - 1
            };

            return Ok(response);
        }

        #region Resource remarks CRUD APIs

        /// <summary>
        /// Update remarks for a resource in the knowledge graph.
        /// </summary>
        /// <param name="resourceId">Azure resource id.</param>
        /// <param name="request">The request containing the remark to update.</param>
        /// <returns></returns>
        [HttpPatch("resource/{resourceId}/remarks")]
        [AuthorizeArmOperation(ArmOperations.AgentGraphWriteActionId)]
        public async Task<ActionResult<AppGroupItem>> AddOrUpdateResourceRemark(string resourceId, [FromBody] ResourceRemarkRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request body cannot be null");
            }

            var properties = new Dictionary<string, string> { { "remarks", request.Remarks } };

            var result = await _graphService.UpdateGraphResourceProperties(resourceId, properties);
            return Ok(result);
        }

        /// <summary>
        /// Delete remarks for a resource in the knowledge graph.
        /// </summary>
        /// <param name="resourceId">Azure resource id.</param>
        /// <returns></returns>
        [HttpDelete("resource/{resourceId}/remarks")]
        [AuthorizeArmOperation(ArmOperations.AgentGraphDeleteActionId)]
        public async Task<ActionResult> DeleteResourceRemark(string resourceId)
        {
            var properties = new Dictionary<string, string> { { "remarks", "" } }; // Just mark the remark as empty for deletion.

            var result = await _graphService.UpdateGraphResourceProperties(resourceId, properties);
            return Ok(result);
        }

        #endregion

        public class GraphQueryRequest
        {
            public string Query { get; set; } = string.Empty;
            public int MaxMessageSize { get; set; } = 200000;
        }

        public class ResourceRemarkRequest
        {
            [Required]
            public string Remarks { get; set; } = string.Empty;
        }
    }
}
