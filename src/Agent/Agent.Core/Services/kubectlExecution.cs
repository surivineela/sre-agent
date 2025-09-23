using System;
using System.Text;
using Microsoft.Extensions.Logging;
using Agent.Logging;

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

        // Split the command into individual arguments
        var commandArgs = SplitCommandIntoArgs(_command);

        // Clean up arguments and apply proper escaping for the final command line
        // Since ExternalProcessCommand now uses simple string.Join, we need to handle all escaping here
        for (int i = 0; i < commandArgs.Length; i++)
        {
            var arg = commandArgs[i];

            // Handle flag=value format where value might be quoted
            if (arg.Contains('='))
            {
                var parts = arg.Split('=', 2); // Split into at most 2 parts
                if (parts.Length == 2)
                {
                    var flag = parts[0];
                    var value = parts[1];

                    // Remove shell-level quotes from the value
                    if (value.Length >= 2 && value.StartsWith("'") && value.EndsWith("'"))
                    {
                        value = value.Substring(1, value.Length - 2);
                    }
                    else if (value.Length >= 2 && value.StartsWith("\"") && value.EndsWith("\""))
                    {
                        value = value.Substring(1, value.Length - 2);
                    }

                    // Apply proper escaping for the final command line
                    value = EscapeArgumentForCommandLine(value);
                    commandArgs[i] = flag + "=" + value;
                }
            }
            // Handle standalone quoted arguments
            else
            {
                // Remove shell-level quotes
                if (arg.Length >= 2 && arg.StartsWith("'") && arg.EndsWith("'"))
                {
                    arg = arg.Substring(1, arg.Length - 2);
                }
                else if (arg.Length >= 2 && arg.StartsWith("\"") && arg.EndsWith("\""))
                {
                    arg = arg.Substring(1, arg.Length - 2);
                }

                // Apply proper escaping for the final command line
                commandArgs[i] = EscapeArgumentForCommandLine(arg);
            }
        }

        var allArgs = new List<string>(commandArgs)
        {
            $"--kubeconfig={_kubeConfigPath}",
            $"--cache-dir={_cacheDir}"
        };

        var pCmd = new ExternalProcessCommand(_logger,
            "kubectl",
            allArgs.ToArray(),
            stdin: _stdin);

        try
        {
            var (exitCode, stdout, stderr) = await pCmd.ExecuteAsync(cancellationToken);

            if (exitCode != 0)
            {
                throw new InvalidOperationException(stderr);
            }

            return stdout;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError($"KubectlExecution failed for command '{_command}': {ex}");
            throw;
        }
    }

    private static string[] SplitCommandIntoArgs(string command)
    {
        var args = new List<string>();
        var currentArg = new StringBuilder();
        bool inDoubleQuotes = false;
        bool inSingleQuotes = false;
        bool escapeNext = false;

        for (int i = 0; i < command.Length; i++)
        {
            char c = command[i];

            if (escapeNext)
            {
                currentArg.Append(c);
                escapeNext = false;
                continue;
            }

            if (c == '\\')
            {
                // Look ahead to see if this is escaping a quote
                if (i + 1 < command.Length && (command[i + 1] == '"' || command[i + 1] == '\''))
                {
                    escapeNext = true;
                    currentArg.Append(c);
                }
                else
                {
                    currentArg.Append(c);
                }
                continue;
            }

            if (c == '"' && !inSingleQuotes)
            {
                inDoubleQuotes = !inDoubleQuotes;
                currentArg.Append(c);
                continue;
            }

            if (c == '\'' && !inDoubleQuotes)
            {
                inSingleQuotes = !inSingleQuotes;
                currentArg.Append(c);
                continue;
            }

            if (char.IsWhiteSpace(c) && !inDoubleQuotes && !inSingleQuotes)
            {
                if (currentArg.Length > 0)
                {
                    args.Add(currentArg.ToString());
                    currentArg.Clear();
                }
                // Skip multiple spaces
                while (i + 1 < command.Length && char.IsWhiteSpace(command[i + 1]))
                {
                    i++;
                }
                continue;
            }

            currentArg.Append(c);
        }

        if (currentArg.Length > 0)
        {
            args.Add(currentArg.ToString());
        }

        return args.ToArray();
    }

    private static string EscapeArgumentForCommandLine(string argument)
    {
        if (string.IsNullOrEmpty(argument))
        {
            return "\"\"";
        }

        // Only quote if the argument contains spaces, quotes, or other special characters
        bool needsQuoting = argument.Any(c => char.IsWhiteSpace(c) || c == '"' || c == '&' || c == '|' || c == '<' || c == '>' || c == '^');

        if (!needsQuoting)
        {
            return argument;
        }

        // Escape the argument according to Windows command line rules
        var escaped = new StringBuilder();
        escaped.Append('"');

        int backslashCount = 0;
        foreach (char c in argument)
        {
            if (c == '\\')
            {
                backslashCount++;
            }
            else if (c == '"')
            {
                // Escape all preceding backslashes and the quote
                escaped.Append('\\', backslashCount * 2 + 1);
                backslashCount = 0;
            }
            else
            {
                // Non-special character, just append preceding backslashes
                escaped.Append('\\', backslashCount);
                backslashCount = 0;
            }
            escaped.Append(c);
        }

        // Escape trailing backslashes
        escaped.Append('\\', backslashCount * 2);
        escaped.Append('"');

        return escaped.ToString();
    }
}
