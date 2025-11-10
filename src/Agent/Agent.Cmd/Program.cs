// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cmd.Commands;
using Agent.Core.Clients.Chat;
using Agent.Core.Configuration;
using Agent.Framework;
using Agent.Runtime;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenAI;

namespace Agent.Cmd
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var builder = Web.Program.CreateWebApplicationBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
            builder.LoadAppSettings(isDevelopment: true);
            builder.ValidateAndRegisterAppSettings<AppSettings>();

            // Register DI dependencies using builder.Services
            builder.Services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddConsole();
            });

            builder.Services.AddSingleton<GenerateEvalCommand>();
            builder.Services.AddSingleton<ConvertToSkillCommand>();

            string? llmDeploymentName = builder.Configuration["AppSettings:Core:Azure:OpenAI:LLMDeploymentName"];
            if (string.IsNullOrEmpty(llmDeploymentName))
            {
                throw new InvalidOperationException("LLM Deployment Name is not configured. Please set 'AppSettings:Core:Azure:OpenAI:LLMDeploymentName' in your configuration.");
            }

            builder.Services.ConfigureAzureOpenAIClient();

            builder.Services.AddKeyedChatClient("function-invocation-enabled",
                serviceProvider => serviceProvider.GetRequiredService<OpenAIClient>().GetChatClient(llmDeploymentName).AsIChatClient(),
                ServiceLifetime.Singleton)
                .Use(next => new ReasoningChatClient(next))
                .UseFunctionInvocation();

            var webapp = builder.Build();

            var asyncInitializerService = webapp.Services.GetRequiredService<AsyncInitializerService>() as IHostedService;
            await asyncInitializerService.StartAsync(CancellationToken.None);

            CommandLineApplication commandLineApplication = new(throwOnUnexpectedArg: true);
            commandLineApplication.HelpOption("-?|-h|--help");

            commandLineApplication.Command("GenerateEval",
                (command) =>
                {
                    var cmd = webapp.Services.GetRequiredService<GenerateEvalCommand>();
                    cmd.GenerateEval(command);
                });

            commandLineApplication.Command("convert-to-skill",
                (command) =>
                {
                    var cmd = webapp.Services.GetRequiredService<ConvertToSkillCommand>();

                    command.HelpOption("-?|-h|--help");

                    var agentName = command.Option(
                        "--agent-name",
                        "The name of the agent to convert to a skill.",
                        CommandOptionType.SingleValue);

                    var outputDirectory = command.Option(
                        "--output-directory",
                        "The directory to output the converted skill files.",
                        CommandOptionType.SingleValue);

                    command.OnExecute(async () =>
                    {
                        await cmd.ExecuteAsync(
                            command,
                            agentName: agentName.Value(),
                            outputDirectory: outputDirectory.Value());

                        return 0;
                    });
                });

            commandLineApplication.OnExecute(() =>
            {
                commandLineApplication.ShowHelp();
                return 0;
            });

            commandLineApplication.Execute(args);
        }
    }
}

