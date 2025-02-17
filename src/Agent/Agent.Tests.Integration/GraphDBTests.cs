using Agent.Core;
using Agent.Core.Models;
using Agent.Data.DatabaseManagers.GraphDatabase;
using Agent.Plugins;
using Agent.Runtime;
using Agent.Tests.Integration.Fixtures;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.SemanticKernel;
using Xunit.Abstractions;

namespace Agent.Tests.Integration
{
    //public class GraphPlugin
    //{
    //    public IGraphDatabaseManager GraphDatabaseManager { get; }
    //    public GraphPlugin(IGraphDatabaseManager graphDatabaseManager)
    //    {
    //        GraphDatabaseManager = graphDatabaseManager;
    //    }

    //    [KernelFunction("list_vertex_types")]
    //    public async Task<ResultSet<dynamic>> ListVertexTypes()
    //    {
    //        return await GraphDatabaseManager.Query("g.V().groupCount().by(label)");
    //    }

    //    [KernelFunction("list_edge_types")]
    //    public async Task<ResultSet<dynamic>> ListEdgeTypes()
    //    {
    //        return await GraphDatabaseManager.Query("g.E().groupCount().by(label)");
    //    }

    //    [KernelFunction("list_vertex_properties")]
    //    public async Task<ResultSet<dynamic>> ListVertexProperties()
    //    {
    //        return await GraphDatabaseManager.Query("g.V().properties().groupCount().by(label())");
    //    }

    //    [KernelFunction("list_edge_properties")]
    //    public async Task<ResultSet<dynamic>> ListEdgeProperties()
    //    {
    //        return await GraphDatabaseManager.Query("g.V().properties().groupCount().by(label())");
    //    }

    //    [KernelFunction("get_resource_types")]
    //    public async Task<ResultSet<dynamic>> GetResourceTypes()
    //    {
    //        return await GraphDatabaseManager.Query("g.V().values(\"resourceType\").dedup()");
    //    }

    //    [KernelFunction("list_web_apps")]
    //    public async Task<ResultSet<dynamic>> ListWebApps()
    //    {
    //        return await GraphDatabaseManager.Query("g.V().has(\"resourceType\", \"WebApp\")");
    //    }
    //}

    class QueryAgentPlugin
    {
        public IGraphDatabaseManager GraphDatabaseManager { get; }
        public IConfiguration Config { get; }
        public ITestOutputHelper _output { get; }
        public QueryAgentPlugin(IGraphDatabaseManager graphDatabaseManager, IConfiguration config, ITestOutputHelper testOutputHelper)
        {
            GraphDatabaseManager = graphDatabaseManager;
            Config = config;
            _output = testOutputHelper;
        }

        [KernelFunction("invoke_query_agent")]
        public async Task<string> InvokeQueryAgent(string goal)
        {
            var agent = new QueryAgent(Config, goal, _output);
            return await agent.CompleteAsync();
        }
    }

    class QueryAgent
    {
        internal IList<Microsoft.Extensions.AI.ChatMessage> ChatHistory { get; }
        internal ChatOptions ChatOptions { get; }
        internal IChatClient Client { get; }
        internal ITestOutputHelper _output { get; }
        string Answer = "";

        const string SystemPrompt = @"You are designed to traverse a CosmosDB resource graph to discover bad practices.
If found, return information about the resources that are using bad practices.
One such example of a bad practice is if a web app talks to a sql server using a connection string.
You can infer this if the web app does no have a managed identity assigned to it which has permission to access a sql server.
A managed identity would be connected to the web app via a graph edge, not a property.
The 'not' keyword will be helpful in your queries.

Before you are done, you must call update_answer";

        internal QueryAgent(IConfiguration _config, string goal, ITestOutputHelper testOutputHelper)
        {
            ChatHistory = [
                new Microsoft.Extensions.AI.ChatMessage(ChatRole.System, SystemPrompt),
                new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, goal)
                ];

            var services = new ServiceCollection();

            // Register dependencies
            services.AddLogging(
                builder => builder.AddXUnit(testOutputHelper)
            );
            services.AddSingleton(_config);
            services.AddScoped<GitHubClient>();
            services.AddScoped<ISubscriptionPlugin, SubscriptionPlugin>();
            services.AddScoped<SubscriptionPluginDefinition>();
            services.AddScoped<IGraphDatabaseManager, GremlinGraphDatabaseManager>();
            //services.AddScoped<GraphPlugin>();
            services.ConfigureAzureOpenAIClient();
            services.ConfigureIChatClient();

            ServiceProvider s = services.BuildServiceProvider();

            SubscriptionPluginDefinition pluginDef = s.GetRequiredService<SubscriptionPluginDefinition>();
            //GraphPlugin plugin2Def = s.GetRequiredService<GraphPlugin>();
            
            IChatClient chatClient = s.GetRequiredService<IChatClient>();

            var chatOptions = new ChatOptions
            {
                Tools = [
                    AIFunctionFactory.Create(pluginDef.QueryResourceGraph),
                    AIFunctionFactory.Create(this.UpdateAnswer)
                ]
            };

            ChatOptions = chatOptions;
            Client = chatClient
                    .AsBuilder()
                    .UseFunctionInvocation()
                    .Build();
        }

        public async Task GetMinContext()
        {
            await Client.CompleteAsync(ChatHistory, ChatOptions);
        }

        public async Task<string> CompleteAsync()
        {
            int attempts = 0;

            while (attempts < 10 && string.IsNullOrEmpty(Answer))
            {
                ChatCompletion completion = await Client.CompleteAsync(ChatHistory, ChatOptions);
                ChatHistory.Add(new(ChatRole.Assistant, completion.Message.Text));
                attempts += 1;
            }

            if (string.IsNullOrEmpty(Answer))
            {
                throw new Exception("The agent did not complete the task in 10 attempts");
            }

            return Answer;
        }

        [KernelFunction("update_answer")]
        public void UpdateAnswer(string answer)
        {
            Answer = answer;
        }
    }


   [Collection(nameof(CombinedTestCollection))]
    public class GraphDBTests : IDisposable
    {
        private readonly CombinedFixture _fixture;
        private readonly ITestOutputHelper _output;
        private readonly IConfiguration _config;
        private readonly Session Session;
        private readonly TestChatClient ToolCallingChatClient;

        public GraphDBTests(CombinedFixture fixture, ITestOutputHelper testOutputHelper)
        {
            _fixture = fixture;
            _output = testOutputHelper;
            _config = fixture.ConfigFixture.Configuration;

            var services = new ServiceCollection();

            // Register dependencies
            services.AddLogging();
            services.AddSingleton(_config);
            services.AddScoped<ITestOutputHelper>(_ => _output);
            services.AddScoped<GitHubClient>();
            services.AddScoped<ISubscriptionPlugin, SubscriptionPlugin>();
            services.AddScoped<SubscriptionPluginDefinition>();
            services.AddScoped<IGraphDatabaseManager, GremlinGraphDatabaseManager>();
            //services.AddScoped<GraphPlugin>();
            services.AddScoped<QueryAgentPlugin>();
            services.ConfigureAzureOpenAIClient();
            services.ConfigureIChatClient();

            ServiceProvider s = services.BuildServiceProvider();

            SubscriptionPluginDefinition pluginDef = s.GetRequiredService<SubscriptionPluginDefinition>();
            //GraphPlugin plugin2Def = s.GetRequiredService<GraphPlugin>();
            QueryAgentPlugin plugin3Def = s.GetRequiredService<QueryAgentPlugin>();
            IChatClient chatClient = s.GetRequiredService<IChatClient>();

            var chatOptions = new ChatOptions
            {
                Tools = [
                    AIFunctionFactory.Create(pluginDef.DeleteResourceGraph),
                    AIFunctionFactory.Create(pluginDef.BuildResourceGraphForAllSubscriptionsAsync),
                    AIFunctionFactory.Create(plugin3Def.InvokeQueryAgent)
                ]
            };

            ToolCallingChatClient = new TestChatClient(
                chatClient
                    .AsBuilder()
                    .UseFunctionInvocation()
                    .Build(),
                chatOptions,
                _output
            );
        }

        [Fact]
        public async Task CreateGraph()
        {
            await ToolCallingChatClient.CompleteAsync($"call build_mock_resource_graph_for_all_subscriptions");
        }

        [Fact]
        public async Task ClearGraph()
        {
            await ToolCallingChatClient.CompleteAsync($"call delete_resource_graph");
        }

        [Fact]
        public async Task QueryGraph()
        {
            await ToolCallingChatClient.CompleteAsync($"find a web app that uses bad practices");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("bad web app found"));
        }

        private async Task _Dispose()
        {
            _output.WriteLine("\nAll chat messages:");
            foreach (var message in ToolCallingChatClient.ChatHistory)
            {
                if (message.Text != null)
                {
                    _output.WriteLine(message.Text);
                }
            }

            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("no exceptions or errors occurred. bad practices are okay."));
        }

        public void Dispose()
        {
            _Dispose().GetAwaiter().GetResult();
        }
    }
}