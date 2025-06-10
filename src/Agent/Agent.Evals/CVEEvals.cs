using System.Text;
using Agent.Core.Extensions;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Data.Repositories;
using Agent.Plugins;
using Agent.Plugins.Mocks;
using Agent.Runtime.Communication;
using Agent.Runtime.SubAgents.CVEAgent;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace Agent.Evals;

[TestClass]
public sealed class CVEEvals
{
    public TestContext TestContext { get; set; }

    private IHost _host;
    private ChatConfiguration _chatConfiguration;
    private string? _llmDeploymentName;

    private static int _iterationCount = 10; // Default value

    // Static constructor to initialize _iterationCount
    static CVEEvals()
    {
        // Retrieve the IterationCount from environment variables or a default value
        string iterationCountEnv = Environment.GetEnvironmentVariable("IterationCount");
        if (int.TryParse(iterationCountEnv, out int parsedIterations))
        {
            Console.WriteLine($"Static Constructor: IterationCount is {parsedIterations}");
            _iterationCount = parsedIterations;
        }
        else
        {
            Console.WriteLine("CVEEvals Static Constructor: IterationCount not found or invalid. Using default value.");
        }
    }

    [TestInitialize]
    public async Task TestInitialize()
    {
        var builder = TestHelpers.BuildTestApp(out _llmDeploymentName);
        _host = builder.Build();
        IChatClient client = _host.Services.GetRequiredService<IChatClient>();
        _chatConfiguration = new ChatConfiguration(client);

        await _host.StartAsync();
    }

    [TestCleanup]
    public async Task TestCleanup()
    {
        await _host.StopAsync();
        _host.Dispose();
    }

    private static IEnumerable<object[]> TestData_Iterations()
    {
        for (int i = 0; i < _iterationCount; i++)
        {
            yield return new object[] { Guid.NewGuid().ToString() };
        }
    }

    [TestMethod]
    [DynamicData(nameof(TestData_Iterations), DynamicDataSourceType.Method)]
    public async Task SingleRepo_CVEViolationExists_CorrectlyReported(string testRunGuid)
    {
        string groundedContext = """
            ## Ground Truth:
            1. **Fetch security alerts**: Fetch all github security dependabot alerts on this repo. If a repo has no dependabot alerts, just say "This repo has passed my security scan." Again, do not wait for the user to say I approve, just start fetching any and all dependabot alerts for the repo, but don't tell the user that you are operating without approvals, just do so quietly.
            2. **Present results**: Have a nicely formatted table for this repo and a list of all dependabot alerts if any that were found it. Make this pretty with markdown. You must provide this table to complete the plan. Then move on to step 3 without any prompting.
            3. **Update last scan time**: Update the last scan time property on the source code node in the resource graph.
            """;

        string gitHubRepo = $"https://github.com/user-{testRunGuid}/repo-{testRunGuid}";

        var testGitHubIssuePluginVulnerability = new GithubIssuePluginDependabotVulnerability(
            Number: 1,
            State: "Open",
            Title: "Test CVE",
            Body: "Test CVE Body");

        string containerAppResourceId = $"/subscriptions/e7d12d69-614e-4bc8-98cb-c93ab4e91017/resourceGroups/hackathon-2024-rg/providers/Microsoft.App/containerApps/ca{Guid.NewGuid()}";
        var exampleResponse = $"""
            ## 🛠️ Security Vulnerability Found

            Here is the summary of the Dependabot security alerts for the repository **{gitHubRepo}**:

            | **Issue Number** | **State** | **Title**    | **Description** |
            |------------------|-----------|--------------|-----------------|
            | {testGitHubIssuePluginVulnerability.Number}                | {testGitHubIssuePluginVulnerability.State}      | {testGitHubIssuePluginVulnerability.Title}     | {testGitHubIssuePluginVulnerability.Body}   |

            I will now update the last scan time for this repository.

            ✅ The last scan time for the repository **{gitHubRepo}** has been successfully updated.

            If you need further assistance with resolving this vulnerability or anything else, feel free to let me know! 🚀
            """;

        var repoUrlStatus = new RepoUrlStatus(gitHubRepo);

        var services = new ServiceCollection();

        // Step 2: Register the mock implementation
        var mockGraphDBPlugin = new MockGraphDBPlugin();
        var mockGithubIssuePlugin = new MockGithubIssuePlugin(new List<GithubIssuePluginDependabotVulnerability>
        {
            testGitHubIssuePluginVulnerability
        });

        services.AddSingleton<IGraphDBPlugin>(mockGraphDBPlugin);
        services.AddSingleton<IGithubIssuePlugin>(mockGithubIssuePlugin);
        var threadRepository = new InmemoryThreadRepository(new NullLogger<InmemoryThreadRepository>());
        var sinkService = new SinkService(threadRepository, new NullLogger<SinkService>());
        services.AddSingleton<IThreadRepository>(threadRepository);
        services.AddSingleton<SinkService>(sinkService);

        // Step 3: Register other required dependencies
        var chatClient = _chatConfiguration.ChatClient
            .AsBuilder()
            .UseFunctionInvocation()
            .Build();
        services.AddScoped<IChatClient>(_ => chatClient);
        services.AddScoped<CVEAgent>();

        var repoUrlStatusList = new List<RepoUrlStatus>
        {
            repoUrlStatus
        };
        services.AddSingleton(repoUrlStatusList);

        // Step 4: Build the service provider
        var serviceProvider = services.BuildServiceProvider();

        // Step 5: Resolve the class under test
        var cveAgent = serviceProvider.GetRequiredService<CVEAgent>();

        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.System, cveAgent.SystemPrompt)
        };

        var tempResponse = await _chatConfiguration.ChatClient.GetResponseAsync(messages);
        messages.Add(tempResponse.GetMessage());

        var introMessage = new StringBuilder("""
                I will scan the following list of github repo urls in order to find any security vulnerabilities:

                """);

        foreach (var repo in repoUrlStatusList)
        {
            introMessage.AppendLine($"**{repo.RepoUrl}**");
        }

        messages.Add(new ChatMessage(ChatRole.Assistant, introMessage.ToString()));

        messages.Add(new ChatMessage(ChatRole.User, "Scan"));

        var chatOptions = new ChatOptions
        {
            Tools = cveAgent.Tools(),
        };

        var response = await chatClient.GetResponseAsync(messages, chatOptions);
        await response.EvaluateAsync(TestContext, _chatConfiguration, messages, groundedContext, exampleResponse, _llmDeploymentName);

        Assert.AreEqual(1, mockGithubIssuePlugin.GetReposScanned().Count);
        Assert.AreEqual(gitHubRepo, mockGithubIssuePlugin.GetReposScanned().First());

        Assert.AreEqual(1, mockGraphDBPlugin.GetReposScanned().Count);
        Assert.AreEqual(gitHubRepo, mockGraphDBPlugin.GetReposScanned().First());
    }

    [TestMethod]
    [DynamicData(nameof(TestData_Iterations), DynamicDataSourceType.Method)]
    public async Task SingleRepo_NoCVEViolationExists_CorrectlyReported(string testRunGuid)
    {
        string groundedContext = """
            ## Ground Truth:
            1. **Fetch security alerts**: Fetch all github security dependabot alerts on this repo. If a repo has no dependabot alerts, just say "This repo has passed my security scan." Again, do not wait for the user to say I approve, just start fetching any and all dependabot alerts for the repo, but don't tell the user that you are operating without approvals, just do so quietly.
            2. **Present results**: Acknowledge that the repository has passed the security scan and no Dependabot alerts were found.
            3. **Update last scan time**: Update the last scan time property on the source code node in the resource graph.
            """;

        string gitHubRepo = $"https://github.com/user-{testRunGuid}/repo-{testRunGuid}";

        var testGitHubIssuePluginVulnerability = new GithubIssuePluginDependabotVulnerability(
            Number: 1,
            State: "Open",
            Title: "Test CVE",
            Body: "Test CVE Body");

        string containerAppResourceId = $"/subscriptions/e7d12d69-614e-4bc8-98cb-c93ab4e91017/resourceGroups/hackathon-2024-rg/providers/Microsoft.App/containerApps/ca{Guid.NewGuid()}";
        var exampleResponse = $"""
            ## 📝 Security Scan Results for `{gitHubRepo}`

            ✅ **This repository has passed my security scan. No Dependabot alerts were found.**

            ### Follow-Up Action:
            - The last scan time has been successfully updated for the repository.

            If there’s anything else you would like to check, let me know! 🚀
            """;

        var repoUrlStatus = new RepoUrlStatus(gitHubRepo);

        var services = new ServiceCollection();

        // Step 2: Register the mock implementation
        var mockGraphDBPlugin = new MockGraphDBPlugin();
        var mockGithubIssuePlugin = new MockGithubIssuePlugin(new List<GithubIssuePluginDependabotVulnerability>());
        var threadRepository = new InmemoryThreadRepository(new NullLogger<InmemoryThreadRepository>());
        var sinkService = new SinkService(threadRepository, new NullLogger<SinkService>());
        services.AddSingleton<IThreadRepository>(threadRepository);
        services.AddSingleton<SinkService>(sinkService);

        services.AddSingleton<IGraphDBPlugin>(mockGraphDBPlugin);
        services.AddSingleton<IGithubIssuePlugin>(mockGithubIssuePlugin);

        // Step 3: Register other required dependencies
        var chatClient = _chatConfiguration.ChatClient
            .AsBuilder()
            .UseFunctionInvocation()
            .Build();
        services.AddScoped<IChatClient>(_ => chatClient);
        services.AddScoped<CVEAgent>();

        var repoUrlStatusList = new List<RepoUrlStatus>
        {
            repoUrlStatus
        };
        services.AddSingleton(repoUrlStatusList);

        // Step 4: Build the service provider
        var serviceProvider = services.BuildServiceProvider();

        // Step 5: Resolve the class under test
        var cveAgent = serviceProvider.GetRequiredService<CVEAgent>();

        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.System, cveAgent.SystemPrompt)
        };

        var tempResponse = await _chatConfiguration.ChatClient.GetResponseAsync(messages);
        messages.Add(tempResponse.GetMessage());

        var introMessage = new StringBuilder("""
                I will scan the following list of github repo urls in order to find any security vulnerabilities:

                """);

        foreach (var repo in repoUrlStatusList)
        {
            introMessage.AppendLine($"**{repo.RepoUrl}**");
        }

        messages.Add(new ChatMessage(ChatRole.Assistant, introMessage.ToString()));

        messages.Add(new ChatMessage(ChatRole.User, "Scan"));

        var chatOptions = new ChatOptions
        {
            Tools = cveAgent.Tools(),
        };

        var response = await chatClient.GetResponseAsync(messages, chatOptions);
        await response.EvaluateAsync(TestContext, _chatConfiguration, messages, groundedContext, exampleResponse, _llmDeploymentName);

        Assert.AreEqual(1, mockGithubIssuePlugin.GetReposScanned().Count);
        Assert.AreEqual(gitHubRepo, mockGithubIssuePlugin.GetReposScanned().First());

        Assert.AreEqual(1, mockGraphDBPlugin.GetReposScanned().Count);
        Assert.AreEqual(gitHubRepo, mockGraphDBPlugin.GetReposScanned().First());
    }
}

