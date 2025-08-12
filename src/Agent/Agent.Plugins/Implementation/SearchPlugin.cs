// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;
using Agent.Core.Services;
using Agent.Plugins.Interface;
using Microsoft.Extensions.Logging;

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

        public async Task<string> SearchDocumentsAsync(string searchText)
        {
            _logger.LogInternalInformation($"SearchPlugin received search request for: '{searchText}'");
            var result = await _searchHelper.SearchAsync(searchText, SearchRequest.TypeDocument, false,
                parentSpan: Core.ToolStatic.AsyncLocalToolTraceSpan.Value,
                threadId: Core.ToolStatic.AsyncLocalThreadId.Value.ToString());
            return _searchHelper.FormatSearchResult(result);
        }

        public async Task<string> SearchRunbooksAsync(string searchText)
        {
            _logger.LogInternalInformation($"SearchPlugin received runbook search request for: '{searchText}'");
            var result = await _searchHelper.SearchAsync(searchText, SearchRequest.TypeRunbook, true,
                parentSpan: Core.ToolStatic.AsyncLocalToolTraceSpan.Value,
                threadId: Core.ToolStatic.AsyncLocalThreadId.Value.ToString());
            return _searchHelper.FormatSearchResult(result);
        }
    }
}
