// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Agent.Runtime.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Agent.Tests.Unit.Agents;

public class AgentsUsageTests
{
    private readonly WebApplication _app;

    public AgentsUsageTests()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "test");

        var builder = Web.Program.CreateWebApplicationBuilder(new WebApplicationOptions { EnvironmentName = "test" });

        // Mock the IMcpConnectable service to prevent actual MCP connections during tests
        var mockMcpConnectable = new Mock<IMcpConnectable>();
        mockMcpConnectable.Setup(m => m.GetAllFunctions()).Returns(
        [
            AIFunctionFactory.Create((string query) => "mock docs search result", "microsoft-learn-mcp_microsoft_docs_search"),
            AIFunctionFactory.Create((string url) => "mock docs fetch result", "microsoft-learn-mcp_microsoft_docs_fetch"),
            AIFunctionFactory.Create((string query) => "mock code sample result", "microsoft-learn-mcp_microsoft_code_sample_search")
        ]);

        // Replace the IMcpConnectable registration with our mock
        var serviceDescriptor = builder.Services.FirstOrDefault(d => d.ServiceType == typeof(IMcpConnectable));
        if (serviceDescriptor != null)
        {
            builder.Services.Remove(serviceDescriptor);
        }
        builder.Services.AddSingleton(mockMcpConnectable.Object);

        _app = builder.Build();
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
            if (!agentDescriptor.EnableVanillaMode)
            {
                Assert.True(agentDescriptor.CommonPrompts.Contains("guard_rail"),
                    $"Agent Descriptor with name {agentDescriptor.Name} does not include 'guard_rail' in the 'common_prompts' list");
            }
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
            if (!agentDescriptor.EnableVanillaMode)
            {
                Assert.True(agentDescriptor.CommonPrompts.Contains("format_guidelines"),
                    $"Agent Descriptor with name {agentDescriptor.Name} does not include 'format_guidelines' in the 'common_prompts' list");
            }
        }
    }
}
