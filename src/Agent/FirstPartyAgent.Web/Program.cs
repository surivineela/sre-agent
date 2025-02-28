// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;
using FirstPartyAgent.Agents;
using FirstPartyAgent.Core.Services;
using FirstPartyAgent.Plugins;
using FirstPartyAgent.Runtime;
using FirstPartyAgent.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Agent.Runtime;
using Agent.Core.Configuration;
using FirstPartyAgent.Core.Configuration;
using Microsoft.Extensions.Options;
using FirstPartyAgent.Plugins.Definitions;
using Agent.Plugins;

var builder = WebApplication.CreateBuilder(args);

builder.LoadAppSettings();
builder.ValidateAndRegisterAppSettings<FirstPartyAgentAppSettings>();
builder.Services.AddSingleton(sp => sp.GetRequiredService<FirstPartyAgentExternalSettings>().Kusto);
builder.Services.AddSingleton(sp => sp.GetRequiredService<FirstPartyAgentExternalSettings>().ICM);
builder.Services.AddSingleton(sp => sp.GetRequiredService<FirstPartyAgentExternalSettings>().ICMAPI);
builder.Services.AddSingleton(sp => sp.GetRequiredService<FirstPartyAgentExternalSettings>().ICMWorkflow);

bool useSessionChatService = false;
if (args.Length > 0)
{
    if (args[0] == "--session")
    {
        useSessionChatService = true;
    }
}

builder.Services.AddSingleton<IICMAPIClient, ICMAPIClient>();
builder.Services.AddSingleton<ICMWorkflowClient, ICMWorkflowClient>();
builder.Services.AddSingleton<ICMPlugin>();
builder.Services.AddSingleton<KustoClientService>();
builder.Services.AddSingleton<IKustoPlugin, KustoPlugin>();

builder.Services.AddSingleton<FirstPartyAgent.Core.Services.IAzureSearchClient, FirstPartyAgent.Core.Services.AzureSearchClient>();
builder.Services.AddSingleton<IAzureSearchPlugin, AzureSearchPlugin>();
builder.Services.AddSingleton<AzureSearchPluginDefinition>();

builder.Services.AddSingleton<Agent.Plugins.Models.GitHubClient>();
builder.Services.AddSingleton<IGithubIssuePlugin, GitHubIssuePlugin>();
builder.Services.AddSingleton<GitHubIssuePluginDefinition>();

if (useSessionChatService)
{
    builder.Services.ConfigureAzureOpenAIClient();
    builder.Services.ConfigureIChatClient();
    builder.Services.ConfigureAgents();
    // Add background service that processes the chat conversation
    builder.Services.AddHostedService<SessionService>();
    builder.Services.AddScoped<IChatService, SessionChatService>();
}
else
{
    builder.Services.ConfigureSemanticKernel();
    builder.Services.AddScoped<IChatService, LegacyChatService>();
}

// Add services to the container.
// Register our chat service
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        // Allow HTML in JSON responses
        options.JsonSerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
    });

// Add Blazor services
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Serve static files from wwwroot
app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
