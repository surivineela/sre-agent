using Agent.Core.Configuration;
using Agent.Core.DataConnectors;

namespace Agent.Plugins.DataConnectors.Documentation;

[DataConnector("Documentation")]
public class UserDocumentDataConnector : IDataConnector
{
    public TimeSpan Interval => TimeSpan.FromDays(1);

    public Task InitAsync(DataConnectorInstanceSettings instanceSettings, CancellationToken stoppingToken)
    {
        // not implemented yet
        return Task.CompletedTask;
    }

    public Task RunAsync(CancellationToken stoppingToken)
    {
        // not implemented yet
        return Task.CompletedTask;
    }
}
