using Agent.Core.Models.Session;

namespace Session.Proxy.Services;

public interface IShellService
{
    Task<ShellExecuteResponse> ExecuteAzCli(AzCliExecutionRequest request, string identifier, CancellationToken cancellationToken);
    Task<ShellExecuteResponse> ExecuteKubectl(KubectlExecutionRequest request, string identifier, CancellationToken cancellationToken);

}
