using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OperationalAgentRuntime;

FunctionsApplicationBuilder builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();



// Application Insights isn't enabled by default. See https://aka.ms/AAt8mw4.
// builder.Services
//     .AddApplicationInsightsTelemetryWorkerService()
//     .ConfigureFunctionsApplicationInsights();

builder.Services.AddSingleton<AzureOpenAIClient>(GetAzureOpenAIClient());
builder.Services.AddSingleton<IChatClient>(serviceProvider => GetChatClient(serviceProvider.GetRequiredService<AzureOpenAIClient>()));
builder.Build().Run();



AzureOpenAIClient GetAzureOpenAIClient()
{
    string? aoaiEndpoint = Environment.GetEnvironmentVariable("AzureOpenAIEndpoint");    
    string key = Environment.GetEnvironmentVariable("OpenAIAPI_KEY");

    if (string.IsNullOrEmpty(aoaiEndpoint))
        throw new Exception("Please set `AzureOpenAIEndpoint`, check the readme for more information.");

    Console.WriteLine($" * Using Azure OpenAI endpoint (AzureOpenAIEndpoint): {aoaiEndpoint}");

    if (string.IsNullOrEmpty(key))
    {
        Console.WriteLine("No OpenAIAPI_KEY found, using DefaultAzureCredential");
        return new AzureOpenAIClient(new Uri(aoaiEndpoint), new DefaultAzureCredential());
    }
    else
    {
        return new AzureOpenAIClient(new Uri(aoaiEndpoint), new System.ClientModel.ApiKeyCredential(key));
    }
}

IChatClient GetChatClient(AzureOpenAIClient client)
{
    string? deployment = Environment.GetEnvironmentVariable("AzureOpenAIDeployment");

    if (string.IsNullOrEmpty(deployment))
        throw new Exception("Please set `AzureOpenAIDeployment`, check the readme for more information.");

    return new ChatClientBuilder(client.AsChatClient(deployment))
        .UseFunctionInvocation()
        .Build();
}