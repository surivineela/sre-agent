// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Azure.AI.OpenAI;
using Azure.Identity;
using Xunit.Abstractions;

using OpenAI.Chat;
using E2ETests;
using Agent.Tests.Common;

namespace Agent.Tests.End2End.Fixtures
{
    /// <summary>
    /// Starts the Agent app
    /// </summary>
    public class AzureFunctionsFixture : IDisposable
    {
        public AzureFunctionProcess FunctionApp1Process;
        public HttpClient Client;

        private const int _port = 7777;
        private bool _disposed;
        private readonly IMessageSink _sink;

        public ConfigFixture ConfigFixture { get; } = new ConfigFixture();


        public AzureFunctionsFixture(IMessageSink sink)
        {
            _sink = sink;

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
            // Extract configuration values
            string aoaiEndpoint = ConfigFixture.AzureSettings.OpenAI.Endpoint;
            string? key = ConfigFixture.AzureSettings.OpenAI.ApiKey;
            string? deployment = ConfigFixture.AzureSettings.OpenAI.LLMDeploymentName;

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
                client = new AzureOpenAIClient(new Uri(aoaiEndpoint), new DefaultAzureCredential());  // CodeQL [SM05137] This is non-production testing code which is not deployed.
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
