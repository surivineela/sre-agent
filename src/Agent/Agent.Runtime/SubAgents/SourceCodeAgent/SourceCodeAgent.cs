// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Generic;
using System.Text;
using Agent.Core.Extensions;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Plugins;
using Agent.Plugins.Interface;
using Agent.Runtime.Communication;
using Google.Protobuf;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using AIChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace Agent.Runtime.SubAgents.SourceCodeAgent
{
    public class SourceCodeAgent : ScannerSubAgent
    {
        private readonly IGraphDBPlugin _graphDBPlugin;
        private readonly List<SourceCodeStatus>? _appsWithoutSourceCodeNodes;

        public SourceCodeAgent(
            IChatClient chatClient,
            IGraphDBPlugin graphDBPlugin,
            SinkService sinkService,
            IThreadRepository repository,
            List<SourceCodeStatus>? appsWithoutSourceCodeNodes = null) 
            : base("Source Code Agent", chatClient, sinkService, repository, isConcludingThreadAfterOpeningMessages: false)
        {
            _graphDBPlugin = graphDBPlugin;
            _appsWithoutSourceCodeNodes = appsWithoutSourceCodeNodes;
        }

        public override string SystemPrompt { get; protected set; } = $@"
You are **Source Code Agent**. Always address yourself as ""Source Code Agent"" and begin by asking which resources the user wants to monitor. For greeting messages, introduce yourself briefly and explain your capabilities.

**Core Communication Guidelines:**
- Use professional indicators (📝, ✅) to summarize findings
- **Always communicate with the user before and after each diagnostic step**
- Stay strictly within your defined toolset

**Approvals**
This workflow does not need to wait for explicit user approval to proceed. Just collect the needed info from the user and proceed with the steps.

**Workflow:**
1. **Find Resource**: Use the GetContainerAppsWithNodesWithoutSourceCodeNodesAsync to find a list of container apps that need source code information. If the list is empty, your job is done and you can conclude early. If the list has entries in it, please list them out in a nice Markdown table and proceed to step 2.
2: **Wait**: In this step, you are waiting for the user to respond with repo urls for EACH container app that you listed in the table in step 1. This repo url will look like https://github.com/..... Once you get one per app listed in step 1, move on to step 3. Always respond to the user with a message saying that you are going to add a link between the repo url and their container app, and please format this response with correct markdown syntax. If the user does not respond with a repo url, just kindly remind them that you need a repo url in order to proceed.
3. **Update Graph**: Use AddSourceCodeNodeToContainerAppNodeAsync to add repo url info as a source code node and add an edge from the container app node to the source code node.
4. **Completion**: After step 3 completes, rescan to see if any container apps need source code nodes using this tool GetContainerAppsWithNodesWithoutSourceCodeNodesAsync. When the list returned by the tool has 0 entries, this flow has concluded. Else, restart at step 1.

**Response Formatting:**
- Use well-formatted Markdown with clear line breaks
- Use only H2 headings (##) with professional emojis
- Put Azure IDs in code blocks
- Use chart plugins for visualizations
- Stay within Container App operations scope

**Available Tools:**
- **AddSourceCodeNodeToContainerAppNodeAsync**: Add a link between the source code repository and the container app in the knowledge graph
- **GetContainerAppsWithNodesWithoutSourceCodeNodesAsync**: Get a list of all container apps that DO NOT HAVE source code nodes with GitHub repo urls associated with them.
- **Wait**
";

        public override IList<AITool> Tools()
        {
            var graphDbPluginDefinition = new GraphDBPluginDefinition(_graphDBPlugin);
            
            return [
                AIFunctionFactory.Create(graphDbPluginDefinition.AddSourceCodeNodeToContainerAppNode),
                AIFunctionFactory.Create(graphDbPluginDefinition.GetContainerAppsWithNodesWithoutSourceCodeNodes)
            ];
        }

        public override async Task<IList<AIChatMessage>> GetStartingMessagesAsync()
        {
            var messages = new List<AIChatMessage>
            {
                new AIChatMessage(ChatRole.System, SystemPrompt)
            };

            if (_appsWithoutSourceCodeNodes != null &&  _appsWithoutSourceCodeNodes.Any())
            {
                messages.AddRange(GetMessagesToInformAgentAboutAppsWithoutSourceCode(_appsWithoutSourceCodeNodes));
            }

            var response = await _chatClient.GetResponseAsync(messages);
            messages.Add(response.GetMessage());
            messages.Add(new AIChatMessage(ChatRole.User, "Great, lets start executing - trigger an approval flow so that I can approve it."));

            return messages;
        }

        public static List<AIChatMessage> GetMessagesToInformAgentAboutAppsWithoutSourceCode(List<SourceCodeStatus> appsWithoutSourceCodeNodes)
        {
            var messages = new List<AIChatMessage>();
            var existingAppsDetails = string.Join(Environment.NewLine,
                appsWithoutSourceCodeNodes.Select(x => $"{x.ResourceId} currently does not have a source code node."));
            messages.Add(new AIChatMessage(ChatRole.User, $"Here are the apps that need updating: {existingAppsDetails}"));

            StringBuilder introMessage = new StringBuilder("""
                I work to link source repository urls with applications in order to perform richer analysis on the apps. Here are the apps that I need source repo URLs for:  
                """);

            introMessage.AppendLine();
            foreach (var app in appsWithoutSourceCodeNodes)
            {
                introMessage.AppendLine($"- {app.ResourceId}");
            }

            messages.Add(new AIChatMessage(ChatRole.Assistant, introMessage.ToString()));

            messages.Add(new AIChatMessage(ChatRole.System, """
                Now that the plan is complete, I would share a comprehensive summary of the steps I'll take
                """
            ));

            return messages;
        }
    }
}

