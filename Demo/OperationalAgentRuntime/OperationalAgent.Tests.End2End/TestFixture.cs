using Azure.AI.OpenAI;
using Azure.Identity;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Xunit.Abstractions;

using E2ETests.Models;
using OpenAI.Chat;
using Microsoft.Extensions.Configuration;
using OperationalAgentRuntime.Configuration.Settings;

namespace E2ETests
{
    /// <summary>
    /// 
    /// </summary>
    public class TestFixture : IDisposable
    {
        public AzureFunctionProcess FunctionApp1Process;
        public HttpClient Client;
        public ChatClient ChatClient;

        private const int _port = 7777;
        private bool _disposed;
        private readonly IMessageSink _sink;
        private WebApp _webApp;


        public TestFixture(IMessageSink sink)
        {
            _sink = sink;
            _webApp = new WebApp(sink);
            _webApp.EnsureWebAppExists().GetAwaiter().GetResult();

            ChatClient = GetChatClient();
            StartFunctionApp();
        }

        public void StartFunctionApp()
        {
            var functionApp1Folder = Path.GetFullPath(@"../../../../OperationalAgentRuntimeSK");
            _sink.WriteLine(functionApp1Folder);
            var processFactory = new AzureFunctionProcessFactory();

            try
            {
                FunctionApp1Process = processFactory.Create(functionApp1Folder, _port, _sink);
                FunctionApp1Process.Start();
            }
            catch
            {
                FunctionApp1Process?.Dispose();
                throw;
            }

            this.Client = new HttpClient();
            this.Client.BaseAddress = new Uri($"http://localhost:{_port}");
        }

        public ChatClient GetChatClient()
        {
            string basePath = Path.Combine("..", "..", "..", "..", "OperationalAgentRuntimeSK");
            basePath = Path.GetFullPath(basePath);

            var configuration = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true) //load base settings
                .AddJsonFile("appsettings.development.json", optional: true, reloadOnChange: true) //load local settings
                .AddEnvironmentVariables()
                .Build();

            if (configuration == null)
            {
                throw new InvalidOperationException("Error: Could not find appsettings.json or appsettings.development.json");
            }

            var azureSettings = configuration.GetSection("Azure").Get<AzureSettings>();

            // Extract configuration values
            string aoaiEndpoint = azureSettings.OpenAI.Endpoint;
            string? key = azureSettings.OpenAI.ApiKey;
            string? deployment = azureSettings.OpenAI.DeploymentName;

            // Validate required settings
            if (string.IsNullOrEmpty(aoaiEndpoint))
            {
                throw new InvalidOperationException("The `AzureOpenAIEndpoint` setting is required. Check the README for more information.");
            }

            if (string.IsNullOrEmpty(deployment))
            {
                throw new InvalidOperationException("The `AzureOpenAIDeployment` setting is required. Check the README for more information.");
            }

            Console.WriteLine($" * Using Azure OpenAI endpoint: {aoaiEndpoint}");

            // Create the Azure OpenAI client
            AzureOpenAIClient client;
            if (string.IsNullOrEmpty(key))
            {
                Console.WriteLine("No `OpenAIAPI_KEY` found. Using DefaultAzureCredential.");
                client = new AzureOpenAIClient(new Uri(aoaiEndpoint), new DefaultAzureCredential());
            }
            else
            {
                client = new AzureOpenAIClient(new Uri(aoaiEndpoint), new System.ClientModel.ApiKeyCredential(key));
            }

            // Return the ChatClient instance
            return client.GetChatClient(deployment);
        }


        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    this.Client?.Dispose();

                    FunctionApp1Process?.Dispose();
                    _webApp.EnsureWebAppDeleted().GetAwaiter().GetResult();
                }

                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}