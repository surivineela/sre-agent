// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
using Agent.Runtime.Services;
using Gremlin.Net.Driver;
using Microsoft.AspNetCore.Mvc;

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
        /// Gets all app groups for a specific subscription
        /// </summary>
        /// <param name="subId">The subscription ID</param>
        /// <returns>List of app groups</returns>
        [HttpGet("{subId}/appGroups")]
        public async Task<ActionResult<ResultSet<dynamic>>> GetAppGroupsBySubscription(string subId)
        {
            var result = await _graphService.GetAppGroupsBySubscriptionAsync(subId);
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

        public class GraphQueryRequest
        {
        public string Query { get; set; } = string.Empty;
        public int MaxMessageSize { get; set; } = 200000;
        }
    }
}
