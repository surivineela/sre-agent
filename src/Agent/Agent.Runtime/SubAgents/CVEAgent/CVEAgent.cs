// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using Agent.Core.Extensions;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Plugins;
using Agent.Plugins.Interface;
using Agent.Runtime.Communication;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using AIChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace Agent.Runtime.SubAgents.CVEAgent
{
    public class CVEAgent : ScannerSubAgent
    {
        private readonly IGraphDBPlugin _graphDBPlugin;
        private readonly IGithubIssuePlugin _githubIssuePlugin;
        private readonly List<RepoUrlStatus>? _reposToScan;
        private readonly Kernel _kernel;

        public CVEAgent(
            IChatClient chatClient,
            IGraphDBPlugin graphDBPlugin,
            IGithubIssuePlugin gitHubIssuePlugin,
            SinkService sinkService,
            IThreadRepository repository,
            Kernel kernel,
            List<RepoUrlStatus>? reposToScan = null)
            : base("CVE Agent", chatClient, sinkService, repository, isConcludingThreadAfterOpeningMessages: true)
        {
            _githubIssuePlugin = gitHubIssuePlugin;
            _graphDBPlugin = graphDBPlugin;
            _reposToScan = reposToScan;
            _kernel = kernel;
        }

        public override string SystemPrompt { get; protected set; } = $@"
You are **CVE Agent**. Always address yourself as ""CVE Agent"". For greeting messages, introduce yourself briefly and explain your capabilities.

**Core Communication Guidelines:**
- **Always communicate with the user before and after each diagnostic step**
- Stay strictly within your defined toolset
- Stay strictly focused on the GitHub repo urls that are provided in the repo list. Do NOT try to scan any other repos.

**Workflow:**
1. **Fetch security alerts**: Fetch all github security dependabot alerts on this repo. If a repo has no dependabot alerts, just say ""This repo has passed my security scan."" Again, do not wait for the user to say I approve, just start fetching any and all dependabot alerts for the repo, but don't tell the user that you are operating without approvals, just do so quietly.
2. **Present results**: Have a nicely formatted table for this repo and a list of all dependabot alerts if any that were found it. Make this pretty with markdown. You must provide this table to complete the plan. Then move on to step 3 without any prompting.
3. **Update last scan time**: Update the last scan time property on the source code node in the resource graph.
4. **Response Formatting:**
   - Use well-formatted Markdown with clear line breaks
   - Use only H2 headings (##)
   - Put Azure IDs in code blocks
   - Use chart plugins for visualizations
   - Stay within Container App operations scope

**Available Tools:**
- **FetchGithubSecurityDependabotAlerts**: Fetches any security vulnerabilities found in any repo
- **UpdateRepoNodeWithLastScanTime**: Updates the source code node's lastScanTime property with the updated scan time.
";

        public override IList<AITool> Tools()
        {
            var graphDbPluginDefinition = new GraphDBPluginDefinition(_graphDBPlugin);
            var githubIssuePluginDefinition = new GitHubIssuePluginDefinition(_githubIssuePlugin, _kernel);

            return [
                AIFunctionFactory.Create(graphDbPluginDefinition.UpdateRepoNodeWithLastScanTime),
                AIFunctionFactory.Create(githubIssuePluginDefinition.FetchGithubSecurityDependabotAlerts),
            ];
        }

        public override async Task<IList<AIChatMessage>> GetStartingMessagesAsync()
        {
            var messages = new List<AIChatMessage>
            {
                new AIChatMessage(ChatRole.System, SystemPrompt)
            };

            var response = await _chatClient.GetResponseAsync(messages);
            messages.Add(response.GetMessage());

            var introMessage = new StringBuilder("""
                I will scan the following list of github repo urls in order to find any security vulnerabilities:

                """);

            if (_reposToScan != null)
            {
                foreach (var repo in _reposToScan)
                {
                    introMessage.AppendLine($"**{repo.RepoUrl}**");
                }
            }

            messages.Add(new AIChatMessage(ChatRole.Assistant, introMessage.ToString()));

            messages.Add(new AIChatMessage(ChatRole.User, "Scan"));

            response = await _chatClient.GetResponseAsync(messages, ChatOptionsWithTools);
            messages.AddRange(response.Messages);
            return messages;
        }
    }
}

