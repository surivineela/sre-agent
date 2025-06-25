using Agent.Tests.Common.TestApplication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit.Abstractions;

namespace Agent.Tests.Integration;

public class AgentStartupTest
{
    private readonly ITestOutputHelper _outputHelper;

    public AgentStartupTest(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StartupTestAsync(bool firstParty)
    {
        await using AgentTestApp target = new AgentTestApp(_outputHelper,
            webHostBuilder =>
            {
                webHostBuilder.UseDefaultServiceProvider(options => options.ValidateOnBuild = true);
            },
            deferredHostBuilder =>
            {
                deferredHostBuilder.ConfigureHostConfiguration(config =>
                {
                    if (firstParty)
                    {
                        config.AddCommandLine(["--is-first-party=true"]);
                    }
                });
            });

        // get the server to start the app host.
        Microsoft.AspNetCore.TestHost.TestServer server = target.Server;
    }
}
