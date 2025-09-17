// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Search.Documents.Models;
using FirstPartyAgent.Core.Configuration;
using FirstPartyAgent.Core.Models;

namespace FirstPartyAgent.Core.Plugins
{
    public interface IAzureSearchPlugin
    {
        public Task<IEnumerable<SearchResult<IndexedGitHubIssueModel>>> LookupRelatedGitHubIssues(string issueUrl, List<string> issueDescriptions, CancellationToken cancellationToken = default);
    }
}

