using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Core;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Data.Repositories;
using Agent.Framework;
using Agent.Plugins;
using Agent.Plugins.Interface;
using Agent.Plugins.Mocks;
using Agent.Runtime.MetaAgent;
using Agent.Runtime.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OpenAI.Chat;
using static Agent.Tests.Common.Mocks.MetaAgentMock;

namespace Agent.Tests.Common.Mocks;

public class MetaAgentMockSetup
{
    public IAuthenticationService AuthenticationService = Mock.Of<IAuthenticationService>();
    public InMemoryThreadRepository? ThreadRepository { get; set; }
    public MetaAgent? Agent { get; set; }
    public string GraphName { get; set; }

    public MetaAgentMockSetup(string graphName)
    {
        GraphName = graphName;
    }

    public void FinishSetup(IServiceProvider services)
    {
        this.ThreadRepository = (InMemoryThreadRepository)services.GetRequiredService<IThreadRepository>();

        var chatClientProvider = services.GetRequiredService<IChatClientProvider>();
        var graphDBPlugin = ActivatorUtilities.CreateInstance<GraphDBPlugin>(services, chatClientProvider, new DashboardSettings(), this.AuthenticationService);

        // TODO: add container apps plugin
        // this is harder because the container apps plugin requires more mocked dependencies.

        var factory = GetMockedThirdPartAgentsFactory(
            graphDBPlugin: graphDBPlugin,
            functionsAppPlugin: services.GetRequiredService<IFunctionAppsPlugin>()
            );

        this.Agent = GetMockedMetaAgent(
            chatClientProvider,
            factory,
            threadService: services.GetRequiredService<ThreadService>(),
            threadRepository: this.ThreadRepository);
    }

    public AgentContext GetDefaultMetaAgentContext(string threadId) => new AgentContext(Guid.NewGuid(), Guid.Parse(threadId), AgentTypeEnum.Meta, ContextStateEnum.Idle, null, null);


}
