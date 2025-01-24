using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.Planning;
using OperationalAgentRuntime.Configuration;
using OperationalAgentRuntime.Configuration.Settings;
using OperationalAgentRuntime.Planner;
using OperationalAgentRuntime.Skills;

FunctionsApplicationBuilder builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Add configuration
builder.Services.AddApplicationConfiguration(builder.Configuration);

// Add logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
});
builder.Logging.SetMinimumLevel(LogLevel.Trace);

// Configure Semantic Kernel with typed settings
builder.Services.AddScoped<Kernel>(sp =>
{
    var azureSettings = sp.GetRequiredService<IOptions<AzureSettings>>().Value;
    
    var kernelBuilder = Kernel.CreateBuilder()
        .AddAzureOpenAIChatCompletion(
            deploymentName: azureSettings.OpenAI.DeploymentName,
            endpoint: azureSettings.OpenAI.Endpoint,
            apiKey: azureSettings.OpenAI.ApiKey);

    // Register skills
    kernelBuilder.Plugins.AddFromType<MetricsSkill>();
    
    return kernelBuilder.Build();
});

// Register the planner
builder.Services.AddScoped<SkillsPlanner>();

var app = builder.Build();
app.Run();
