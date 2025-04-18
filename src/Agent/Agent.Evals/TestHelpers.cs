using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Core.Configuration;
using Agent.Runtime;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Agent.Evals;

public static class TestHelpers
{
    public static HostApplicationBuilder BuildTestApp()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Development });
        builder.LoadLocalAppSettings();
        builder.RegisterAppSettingsNoValidation<AppSettings>();

        string? llmDeploymentName = builder.Configuration["AppSettings:Core:Azure:OpenAI:LLMDeploymentName"];

        if (string.IsNullOrEmpty(llmDeploymentName))
        {
            //eval pipeline doesnt use appsettings

            string? apiKey = builder.Configuration["OpenAIKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException("OpenAI API key is missing. Pass it as a TestRunParameter.");
            }

            llmDeploymentName = builder.Configuration["OpenAIModel"];
            if (string.IsNullOrEmpty(llmDeploymentName))
            {
                throw new InvalidOperationException("OpenAI API model is missing. Pass it as a TestRunParameter.");
            }

            string? aiEndpoint = builder.Configuration["OpenAIEndpoint"];
            if (string.IsNullOrEmpty(aiEndpoint))
            {
                throw new InvalidOperationException("OpenAI API endpoint is missing. Pass it as a TestRunParameter.");
            }

            builder.Services.AddSingleton(new AzureOpenAIClient(new Uri(aiEndpoint), new System.ClientModel.ApiKeyCredential(apiKey)));
        }
        else
        {
            builder.Services.ConfigureAzureOpenAIClient();
        }
        
        builder.Services.AddChatClient(sp => sp.GetRequiredService<AzureOpenAIClient>().AsChatClient(llmDeploymentName));

        return builder;
    }
}
