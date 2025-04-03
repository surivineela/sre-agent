// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Extensions;
using Agent.Core.Models;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Agent.Runtime.SubAgents
{
    public class GraphDBQueryAgent : SubAgent
    {
        internal ILogger<GraphDBQueryAgent> _logger { get; }

        internal GraphDBPluginDefinition _def { get; }

        internal IPostToTeamsPlugin _teamsPlugin { get; }

        bool _fetchedPrelimData = false;

        public override string SystemPrompt { get; protected set; } = $@"You are an agent that will explore an unexplored graph database.
You will have to procedurally generate queries to help you answer the question, with each one building on
 the next. Never directly query more than 10 nodes, however if you are calling groupCount, you can ignore the limit.

Note that just because your query doesn't return any results doesn't mean that the higher level
thing you are searching for doesn't exist. Therefore, only filter on properties which you have already proven to exist
and once you come to a conclusion, verify it with a different query before deciding it is true.

If you are asked a question, respond but do not ask a follow-up question, as you are part of a larger system
and nobody is listening.

TIPS:
- To fiter on properties, use .has(<property>, <value>)
- If your query contains a 'limit', you cannot assume that query can answer any questions about the whole dataset.
- Sites are Web Apps
- An association doesn't have to mean there is an edge between two vertices. E.g. an app could be associated with a subscription if
    its resource id contains that sub id.
- If your query contains a limit, know that there could be more actual results
- If a web app uses a managed identity, there will be an edge between a web app and that managed identity
- If a web app uses a specific tls version, it would be specified in webapp's properties. You could use this to notify apps not using TLS 1.3
- Don't presume a specific label or direction when querying edges (e.g. outE, inE). Prefer querying biderectionally and with no specific label (bothE(), both())
- Assume that if a user is asking if something exists, it is more likely to exist than not. Therefore, prove through multiple queries that it does not
- Try to keep your queries as simple as possible
- Try to not use specific ids in your queries

IMPORTANT: If you find apps not following best practices, call the 'postToTeams' tool to notify the end user with a descriptive message.
";

        public GraphDBQueryAgent(GraphDBPluginDefinition def, IChatClient chatClient, ILogger<GraphDBQueryAgent> logger, IPostToTeamsPlugin teamsPlugin) : base("GraphDBQueryAgent", chatClient)
        {
            _logger = logger;
            _def = def;
            _teamsPlugin = teamsPlugin;
        }

        public override IList<AITool> Tools()
        {
            return [
                AIFunctionFactory.Create(_def.Query),
                AIFunctionFactory.Create(_teamsPlugin.PostAsync)
            ];
        }

        public override async Task DoWork(string question)
        {
            if (!_fetchedPrelimData)
            {
                _fetchedPrelimData = true;
                await PrelimData();
            }

            ChatHistory.Add(new(ChatRole.User, question));
            ChatResponse completion = await _chatClient.GetResponseAsync(ChatHistory, ChatOptionsWithTools);
            ChatHistory.Add(new(ChatRole.Assistant, completion.GetMessage().Text));
        }

        public async Task PrelimData()
        {
            ChatHistory.Add(new(ChatRole.System, "Vertex Labels (g.V().groupCount().by(label())):"));
            ChatHistory.Add(new(ChatRole.System, JsonSerializer.Serialize(await _def.Query("g.V().groupCount().by(label())"))));
            ChatHistory.Add(new(ChatRole.System, "Edge labels (g.E().groupCount().by(label())):"));
            ChatHistory.Add(new(ChatRole.System, JsonSerializer.Serialize(await _def.Query("g.E().groupCount().by(label())"))));
            ChatHistory.Add(new(ChatRole.System, "Vertex properties (g.V().properties().key().dedup()):"));
            ChatHistory.Add(new(ChatRole.System, JsonSerializer.Serialize(await _def.Query("g.V().properties().key().dedup()"))));
            ChatHistory.Add(new(ChatRole.System, "Edge properties (g.E().properties().key().dedup()):"));
            ChatHistory.Add(new(ChatRole.System, JsonSerializer.Serialize(await _def.Query("g.E().properties().key().dedup()"))));
        }
    }
}

