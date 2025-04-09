// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.GraphDbClient;
using Gremlin.Net.Driver;
using Microsoft.AspNetCore.Mvc;

namespace Agent.Web.Controllers.v1
{
    [ApiController]
    [Route("api/v1/[controller]")]

    public class GraphController : ControllerBase
    {
        private readonly IGraphDatabaseClient _graphDatabaseClient;

        public GraphController(
            IGraphDatabaseClient graphDatabaseClient)
        {
            _graphDatabaseClient = graphDatabaseClient;
        }

        /// <summary>
        /// This is just a temporary solution to query graph nodes and edges
        /// </summary>
        /// <param name="request">requests that has query string and optional max message size</param>
        /// <returns>graph data array</returns>
        [HttpPost]
        public async Task<ActionResult<ResultSet<dynamic>>> Query([FromBody] GraphQueryRequest request)
        {

            var result = await this._graphDatabaseClient.Query(request.Query);

            return Ok(result);
        }
    }

    public class GraphQueryRequest
    {
        public string Query { get; set; } = string.Empty;
        public int MaxMessageSize { get; set; } = 200000;
    }
}

