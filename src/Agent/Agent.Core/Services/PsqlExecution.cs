using Agent.Core.Helpers;
using Agent.Logging;
using Microsoft.Extensions.Logging;

namespace Agent.Core.Services;

public class PsqlExecution
{
    private readonly ILogger _logger;
    private readonly string _command;
    private readonly string? _accessToken;
    private readonly bool _isDevelopment;
    private readonly string? _host;
    private readonly string? _port;
    private readonly string? _database;
    private readonly string? _user;

    public PsqlExecution(ILogger logger,
        string command,
        string? accessToken = null,
        bool isDevelopment = false,
        string? host = null,
        string? port = "5432",
        string? database = null,
        string? user = null)
    {
        _logger = logger;
        _logger.LogInternalInformation($"[PsqlExecution] Constructor called with parameters:");
        _logger.LogInternalInformation($"[PsqlExecution] - host: '{host}' (null/empty: {string.IsNullOrWhiteSpace(host)})");
        _logger.LogInternalInformation($"[PsqlExecution] - database: '{database}' (null/empty: {string.IsNullOrWhiteSpace(database)})");
        _logger.LogInternalInformation($"[PsqlExecution] - user: '{user}' (null/empty: {string.IsNullOrWhiteSpace(user)})");
        _logger.LogInternalInformation($"[PsqlExecution] - port: '{port}'");
        _logger.LogInternalInformation($"[PsqlExecution] - accessToken length: {accessToken?.Length ?? 0}");
        _logger.LogInternalInformation($"[PsqlExecution] - isDevelopment: {isDevelopment}");
        
        _command = command.Trim();
        _accessToken = accessToken;
        _isDevelopment = isDevelopment;
        _host = host;
        _port = port;
        _database = database;
        _user = user;
    }

    public async Task<string> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_host) || string.IsNullOrWhiteSpace(_database) || string.IsNullOrWhiteSpace(_user))
                throw new InvalidOperationException("Host, database, and user parameters are required for PostgreSQL connection");

            // Environment for auth + SSL (Azure requires SSL)
            var envs = new Dictionary<string, string>
            {
                ["PGPASSWORD"] = _accessToken ?? throw new InvalidOperationException("Access token is required for authentication"),
                ["PGSSLMODE"] = "require"
            };

            var psqlArgs = new List<string>
            {
                "-X", "-q",
                "--pset", "pager=off",
                "--pset", "format=aligned",
                "--pset", "border=2",         // full box
                "--pset", "linestyle=ascii",  // or 'unicode' for box-drawing chars
                "--pset", "footer=off",       // hide "(n rows)"
                "--pset", "null=(null)",      // optional: explicit NULLs
                "-h", _host!,
                "-p", _port ?? "5432",
                "-d", _database!,
                "-U", _user!,
                "-v", "ON_ERROR_STOP=1",
                "-c", _command
            };

            _logger.LogInternalInformation($"[PsqlExecution] Executing command: '{_command}'");
            _logger.LogInternalInformation($"[PsqlExecution] psql args: -h {_host} -p {_port ?? "5432"} -d {_database} -U {_user} -c <sql>");

            // Use appropriate psql path based on environment
            var psqlPath = _isDevelopment 
                ? @"C:\Program Files\PostgreSQL\17\bin\psql.exe"  // Windows development
                : "psql";  // Linux container (installed via postgresql-client package)
            
            _logger.LogInternalInformation($"[PsqlExecution] Using psql path: {psqlPath}");

            var pCmd = new ExternalProcessCommand(
                _logger,
                psqlPath,
                psqlArgs.ToArray(),
                envs: envs);

            var result = await pCmd.ExecuteAsync(cancellationToken);
            _logger.LogInternalInformation($"[PsqlExecution] Command completed successfully. Result length: {result?.Length ?? 0}");
            return result ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError($"PsqlExecution failed for command '{_command}': {ex}");
            throw;
        }
    }
}
