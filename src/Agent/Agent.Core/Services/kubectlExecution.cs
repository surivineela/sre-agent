using System;
using Microsoft.Extensions.Logging;

namespace Agent.Core.Services;

public class KubectlExecution
{
    private ILogger _logger;
    private string _k8sConfiguration;
    // The command is the full kubectl command without the 'kubectl ' prefix
    private string _command;
    private string? _stdin;
    private string _kubeConfigPath;
    private string _cacheDir;

    public KubectlExecution(
        ILogger logger,
        string k8sConfiguration,
        string command,
        string? stdin = null)
    {
        _logger = logger;
        _k8sConfiguration = k8sConfiguration;
        _command = command.Trim();
        if (_command.StartsWith("kubectl ", StringComparison.OrdinalIgnoreCase))
        {
            _command = _command.Substring("kubectl ".Length).Trim();
        }
        _stdin = stdin;
        _kubeConfigPath = Path.GetTempFileName();
        _cacheDir = Path.Combine(Path.GetTempPath(), ".kube");
    }

    public async Task<string> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        // write to temp file
        await File.WriteAllTextAsync(_kubeConfigPath, _k8sConfiguration, cancellationToken);

        var pCmd = new ExternalProcessCommand(_logger,
        "kubectl",
        [
            _command,
            $"--kubeconfig=\"{_kubeConfigPath}\"",
            $"--cache-dir=\"{_cacheDir}\"",
        ],
        stdin: _stdin);
        return await pCmd.ExecuteAsync(cancellationToken);
    }
}
