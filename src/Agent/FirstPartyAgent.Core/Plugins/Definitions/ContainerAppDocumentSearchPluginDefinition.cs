// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using FirstPartyAgent.Constants;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Core.Plugins.Definitions
{
    public class ContainerAppDocumentSearchPluginDefinition
    {
        private readonly IAzureDocSearchPlugin _plugin;

        public ContainerAppDocumentSearchPluginDefinition(IAzureDocSearchPlugin Plugin)
        {
            _plugin = Plugin;
        }

        [KernelFunction(KernelFunctionNames.ACA.SearchAzureContainerAppsDocumentation)]
        [Description(@"Vector search for internal design documents.")]
        public Task<string> SearchAzureContainerAppsDocumentation([Description("search text")] string searchtext)
        {
            return _plugin.SearchDesignDocsAsync(searchtext);
        }
    }
}
