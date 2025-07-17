// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
using Agent.Plugins.Services.Interfaces;
using Agent.Runtime.Services;
using Gremlin.Net.Driver;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

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
        public async Task<ActionResult<ResultSet<dynamic>>> Query([FromBody] GraphQueryRequest request)
        {
            return await _graphService.QueryAsync(request.Query);
        }

        /// <summary>
        /// Returns a list of subscriptions
        /// </summary>
        /// <returns>list of subscriptions</returns>
        [HttpGet("subscriptions")]
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
        public async Task<ActionResult<ResultSet<dynamic>>> GetGraphProgressAsync()
        {
            var result = await _graphService.GetGraphProgressAsync();
            return Ok(result);
        }

        #region Resource remarks CRUD APIs

        /// <summary>
        /// Update remarks for a resource in the knowledge graph.
        /// </summary>
        /// <param name="resourceId">Azure resource id.</param>
        /// <param name="request">The request containing the remark to update.</param>
        /// <returns></returns>
        [HttpPatch("resource/{resourceId}/remarks")]
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
