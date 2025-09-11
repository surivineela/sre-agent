// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Agent.Tests.Unit.Agents;

public class AgentsUsageTests
{
    private readonly WebApplication _app;

    public AgentsUsageTests()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "test");

        _app = Web.Program.CreateWebApplicationBuilder([]).Build();

    }

    [Fact]
    public async Task ValidateNotifyUserUsage()
    {
        var agentFactory = _app.Services.GetRequiredService<IAgentFactory<AgentContext>>() as AgentFactory<AgentContext>;

        Assert.NotNull(agentFactory);

        await agentFactory.InitializeAsync();

        var agentDescriptors = agentFactory.GetAllAgentDescriptors();

        Assert.NotEmpty(agentDescriptors);

        foreach (var agentDescriptor in agentDescriptors)
        {
            if (agentDescriptor.Tools.Any(t => t == "NotifyUser"))
            {
                Assert.True(agentDescriptor.CommonPrompts.Contains("notify_user"),
                    $"Agent Descriptor with name {agentDescriptor.Name} uses tool NotifyUser but does not include 'notify_user' in the 'common_prompts' list");
            }
        }
    }

    [Fact]
    public async Task ValidateGuardRailUsage()
    {
        var agentFactory = _app.Services.GetRequiredService<IAgentFactory<AgentContext>>() as AgentFactory<AgentContext>;

        Assert.NotNull(agentFactory);

        await agentFactory.InitializeAsync();

        var agentDescriptors = agentFactory.GetAllAgentDescriptors();

        Assert.NotEmpty(agentDescriptors);

        foreach (var agentDescriptor in agentDescriptors)
        {
            Assert.True(agentDescriptor.CommonPrompts.Contains("guard_rail"),
                $"Agent Descriptor with name {agentDescriptor.Name} does not include 'guard_rail' in the 'common_prompts' list");
        }
    }

    [Fact]
    public async Task ValidateFormatGuidelinesUsage()
    {
        var agentFactory = _app.Services.GetRequiredService<IAgentFactory<AgentContext>>() as AgentFactory<AgentContext>;

        Assert.NotNull(agentFactory);

        await agentFactory.InitializeAsync();

        var agentDescriptors = agentFactory.GetAllAgentDescriptors();

        Assert.NotEmpty(agentDescriptors);

        foreach (var agentDescriptor in agentDescriptors)
        {
            Assert.True(agentDescriptor.CommonPrompts.Contains("format_guidelines"),
                $"Agent Descriptor with name {agentDescriptor.Name} does not include 'format_guidelines' in the 'common_prompts' list");
        }
    }
}
