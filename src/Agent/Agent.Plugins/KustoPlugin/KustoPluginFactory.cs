// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Plugins.Interface;
using Agent.Plugins.Tools;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Kusto
{
    public partial class KustoPluginFactory
    {
        private readonly IKustoPlugin _kustoPlugin;
        private readonly IAgentOutboundCommunicationService _agentOutboundCommunicationService;
        private readonly IAuthenticationService _authService;

        private readonly ILoggerFactory _loggerFactory;

        public KustoPluginFactory(IKustoPlugin kustoPlugin, IAgentOutboundCommunicationService agentOutboundCommunicationService, ILoggerFactory loggerFactory, IAuthenticationService authService)
        {
            _kustoPlugin = kustoPlugin;
            _agentOutboundCommunicationService = agentOutboundCommunicationService;
            _loggerFactory = loggerFactory;
            _authService = authService;
        }

        public KustoPlugin Create(
            KustoConnector kustoSettings)
        {
            var kustoPlugin = new KustoPlugin(_loggerFactory.CreateLogger<KustoPlugin>(),
                new KustoClient(
                    _loggerFactory.CreateLogger<KustoClient>(),
                    kustoSettings,
                    _authService
                ), _agentOutboundCommunicationService
            );

            return kustoPlugin;
        }
    }
}
