// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;
using Agent.Logging;
using Agent.Plugins.Interface;
using Microsoft.Extensions.Logging;
using SearchDocument = Agent.Core.Models.Api.v1.SearchDocument;

namespace Agent.Plugins.Implementation
{
    public class SearchPlugin : ISearchPlugin
    {
        private readonly ILogger<SearchPlugin> _logger;
        private readonly SearchHelper _searchHelper;

        public SearchPlugin(
            ILogger<SearchPlugin> logger,
            SearchHelper searchHelper)
        {
            _logger = logger;
            _searchHelper = searchHelper ?? throw new ArgumentNullException(nameof(searchHelper));
        }

        public async Task<List<SearchDocument>> SearchAsync(string searchText)
        {
            _logger.LogInternalInformation($"SearchPlugin received search request for: '{searchText}'");
            return await _searchHelper.SearchAsync(searchText);
        }
    }
}
