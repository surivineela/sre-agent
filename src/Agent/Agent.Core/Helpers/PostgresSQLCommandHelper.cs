using System.Collections.Immutable;
using System.IdentityModel.Tokens.Jwt;
using System.Text.RegularExpressions;
using Agent.Core.Interfaces;
using Azure.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Agent.Core.Helpers;

public sealed class PostgresConnectionOptions
{
    public string? ResourceId { get; init; }                 // If set, we derive host
    public string? Host { get; init; }                        // FQDN (overrides ResourceId if provided)
    public string Port { get; init; } = "5432";
    public string Database { get; init; } = "postgres";
    public string? User { get; init; }                        // e.g. UPN for AAD, or admin user 
    public string? ManagedIdentityClientId { get; init; }     // for user-assigned MI (optional)
}

public class PostgresSQLCommandHelper
{
    private readonly ILogger<PostgresSQLCommandHelper> _logger;
    private readonly IHostEnvironment _hostEnvironment;

    // Keeping auth service around in case you still want it elsewhere,
    // but we won't rely on it for token creation in this drop-in.
    private readonly IAuthenticationService _authService;

    public static readonly ImmutableArray<string> AllowedSqlStartingTokens = [
        "SELECT", "WITH", "SHOW", "TABLE", "VALUES", "EXPLAIN"
    ];

    // We allow a broad set of safe meta-commands. (All start with '\')
    // Anything that can write, shell out, or redirect is excluded.
    // This regex matches: \d, \dt, \dS, \dv, \di, \df, \dx, \du, \dn, \db, \dp, \l, \c, etc.
    private static readonly Regex SafeMetaCommandRegex =
        new(@"^\\(d\w*|l|c|du|dn|db|dp|dx)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private const string AadScope = "https://ossrdbms-aad.database.windows.net/.default";

    public PostgresSQLCommandHelper(
        ILogger<PostgresSQLCommandHelper> logger,
        IAuthenticationService authService,
        IHostEnvironment hostEnvironment)
    {
        _logger = logger;
        _authService = authService;
        _hostEnvironment = hostEnvironment;
    }

    public Task<CliExecutionResult> ExecutePsqlCommandAsync(string command)
    {
        throw new InvalidOperationException("This method requires connection parameters. Use the overload with PostgresConnectionOptions or resourceId/database parameters.");
    }

    public Task<CliExecutionResult> ExecutePsqlCommandAsync(string command, string? database)
    {
        if (string.IsNullOrEmpty(database))
        {
            return Task.FromResult(new CliExecutionResult
            {
                ErrorType = CliErrorType.ValidationError,
                Output = "[Validation Failed]: Database parameter is required."
            });
        }

        var opts = new PostgresConnectionOptions
        {
            Database = database,
            Port = "5432"
        };
        return ExecutePsqlCommandAsync(command, opts);
    }

    public Task<CliExecutionResult> ExecutePsqlCommandAsync(
        string command, string resourceId, string database, string? port = "5432")
    {
        var opts = new PostgresConnectionOptions
        {
            ResourceId = resourceId,
            Database = database,
            Port = port ?? "5432"
        };
        return ExecutePsqlCommandAsync(command, opts);
    }

    // New: explicit options
    public async Task<CliExecutionResult> ExecutePsqlCommandAsync(
        string command,
        PostgresConnectionOptions options)
    {
        _logger.LogInternalInformation($"[ExecutePsqlCommandAsync] command: {command}");

        command = command?.Trim() ?? string.Empty;

        // Validate command
        var validationSummary = ValidateReadOnlyCommand(command);
        if (validationSummary != null)
        {
            _logger.LogInternalError($"[ExecutePsqlCommandAsync] Validation failed: {validationSummary}");
            return new CliExecutionResult
            {
                ErrorType = CliErrorType.ValidationError,
                Output = validationSummary
            };
        }

        _logger.LogInternalInformation($"[ExecutePsqlCommandAsync] ===== INITIAL PARAMETER ANALYSIS =====");
        _logger.LogInternalInformation($"[ExecutePsqlCommandAsync] Original options.Host: '{options.Host}' (null/empty: {string.IsNullOrWhiteSpace(options.Host)})");
        _logger.LogInternalInformation($"[ExecutePsqlCommandAsync] Original options.Database: '{options.Database}' (null/empty: {string.IsNullOrWhiteSpace(options.Database)})");
        _logger.LogInternalInformation($"[ExecutePsqlCommandAsync] Original options.User: '{options.User}' (null/empty: {string.IsNullOrEmpty(options.User)})");
        _logger.LogInternalInformation($"[ExecutePsqlCommandAsync] Original options.ResourceId: '{options.ResourceId}' (null/empty: {string.IsNullOrWhiteSpace(options.ResourceId)})");
        _logger.LogInternalInformation($"[ExecutePsqlCommandAsync] Original options.Port: '{options.Port}'");

        // Resolve connection info
        var host = !string.IsNullOrWhiteSpace(options.Host)
            ? options.Host!.Trim()
            : GetFlexibleServerHost(options.ResourceId);

        _logger.LogInternalInformation($"[ExecutePsqlCommandAsync] Host extraction result: '{host}' (null/empty: {string.IsNullOrWhiteSpace(host)})");

        if (string.IsNullOrWhiteSpace(host))
        {
            _logger.LogInternalError($"[ExecutePsqlCommandAsync] HOST EXTRACTION FAILED! options.Host was '{options.Host}', resourceId was '{options.ResourceId}'");
            return Fail("Host could not be determined. Provide Host or a valid Flexible Server resourceId.");
        }
        if (string.IsNullOrWhiteSpace(options.Database))
        {
            _logger.LogInternalError($"[ExecutePsqlCommandAsync] DATABASE IS NULL/EMPTY! options.Database was '{options.Database}'");
            return Fail("Database is required.");
        }

        try
        {
            _logger.LogInternalInformation($"[ExecutePsqlCommandAsync] ===== STARTING POSTGRESQL COMMAND EXECUTION =====");
            _logger.LogInternalInformation($"[ExecutePsqlCommandAsync] Command: {command}");
            _logger.LogInternalInformation($"[ExecutePsqlCommandAsync] ResourceId: {options.ResourceId}");
            _logger.LogInternalInformation($"[ExecutePsqlCommandAsync] Database: {options.Database}");
            _logger.LogInternalInformation($"[ExecutePsqlCommandAsync] Host: {host}");
            _logger.LogInternalInformation($"[ExecutePsqlCommandAsync] Port: {options.Port}");
            _logger.LogInternalInformation($"[ExecutePsqlCommandAsync] User: {options.User ?? "NULL - will be extracted from token"}");

            string? accessToken = null;

            // Get credential from authentication service for PostgreSQL
            _logger.LogInternalInformation($"[ExecutePsqlCommandAsync] About to get PostgreSQL credential from auth service...");
            var credential = _authService.GetPostgresSqlCredential();
            _logger.LogInternalInformation($"[ExecutePsqlCommandAsync] Got credential object, requesting token...");
            var token = await credential.GetTokenAsync(new TokenRequestContext(new[] { AadScope }), CancellationToken.None);
            accessToken = token.Token;
            _logger.LogInternalInformation($"[ExecutePsqlCommandAsync] Got PostgreSQL token from authentication service, token length: {accessToken?.Length ?? 0}");
            _logger.LogInternalInformation($"[ExecutePsqlCommandAsync] Token expires at: {token.ExpiresOn}");

            // Auto-populate user from JWT token if not provided
            if (string.IsNullOrEmpty(options.User))
            {
                // Extract user from JWT token based on environment
                var extractedUser = !string.IsNullOrEmpty(accessToken) ? ExtractUserFromToken(accessToken) : null;
                _logger.LogInternalInformation($"[ExecutePsqlCommandAsync] JWT token extraction result: '{extractedUser}'");

                if (string.IsNullOrEmpty(extractedUser))
                {
                    // Final fallback to client ID if JWT extraction fails
                    if (!string.IsNullOrEmpty(options.ManagedIdentityClientId))
                    {
                        extractedUser = options.ManagedIdentityClientId;
                        _logger.LogInternalInformation($"[ExecutePsqlCommandAsync] Using managed identity client ID as final fallback: {extractedUser}");
                    }
                    else
                    {
                        _logger.LogInternalError($"[ExecutePsqlCommandAsync] No managed identity information available for authentication");
                    }
                }

                if (!string.IsNullOrEmpty(extractedUser))
                {
                    options = new PostgresConnectionOptions
                    {
                        ResourceId = options.ResourceId,
                        Host = host, // Use the already-extracted host, not options.Host which might be null
                        Port = options.Port,
                        Database = options.Database,
                        User = extractedUser,
                        ManagedIdentityClientId = options.ManagedIdentityClientId
                    };
                    _logger.LogInternalInformation($"[ExecutePsqlCommandAsync] Auto-populated user from token: {extractedUser}");
                    _logger.LogInternalInformation($"[ExecutePsqlCommandAsync] Updated options with host: {host}, database: {options.Database}, user: {extractedUser}");
                }
                else
                {
                    _logger.LogInternalWarning($"[ExecutePsqlCommandAsync] Could not extract user from token, will attempt connection anyway");
                }
            }

            _logger.LogInternalInformation($"[ExecutePsqlCommandAsync] ===== FINAL PARAMETER STATE BEFORE PSQLEXECUTION =====");
            _logger.LogInternalInformation($"[ExecutePsqlCommandAsync] Final host: '{host}' (from variable)");
            _logger.LogInternalInformation($"[ExecutePsqlCommandAsync] Final options.Database: '{options.Database}'");
            _logger.LogInternalInformation($"[ExecutePsqlCommandAsync] Final options.User: '{options.User}'");
            _logger.LogInternalInformation($"[ExecutePsqlCommandAsync] Final options.Host: '{options.Host}' (should use host variable instead)");
            _logger.LogInternalInformation($"[ExecutePsqlCommandAsync] Final options.Port: '{options.Port}'");

            _logger.LogInternalInformation($"[ExecutePsqlCommandAsync] Connecting host={host}, db={options.Database}, user={options.User}");

            // Execute command
            _logger.LogInternalInformation($"[ExecutePsqlCommandAsync] Creating PsqlExecution object...");
            _logger.LogInternalInformation($"[ExecutePsqlCommandAsync] PsqlExecution parameters - host: '{host}', database: '{options.Database}', user: '{options.User}', port: {options.Port}");
            _logger.LogInternalInformation($"[ExecutePsqlCommandAsync] Parameter validation - host null/empty: {string.IsNullOrEmpty(host)}, database null/empty: {string.IsNullOrEmpty(options.Database)}, user null/empty: {string.IsNullOrEmpty(options.User)}");

            var exec = new Services.PsqlExecution(
                _logger,
                command,
                accessToken: accessToken,
                isDevelopment: _hostEnvironment.IsDevelopment(),
                host: host,
                port: options.Port,
                database: options.Database,
                user: options.User
            );

            _logger.LogInternalInformation($"[ExecutePsqlCommandAsync] About to execute psql command via PsqlExecution.ExecuteAsync()...");
            var output = await exec.ExecuteAsync();
            _logger.LogInternalInformation($"[ExecutePsqlCommandAsync] PsqlExecution completed successfully, output length: {output?.Length ?? 0}");

            return new CliExecutionResult
            {
                ErrorType = CliErrorType.None,
                Output = output ?? string.Empty
            };
        }
        catch (Exception ex)
        {
            _logger.LogInternalError($"[ExecutePsqlCommandAsync] ===== CRITICAL EXCEPTION IN POSTGRESQL EXECUTION =====");
            _logger.LogInternalError($"[ExecutePsqlCommandAsync] Exception Type: {ex.GetType().FullName}");
            _logger.LogInternalError($"[ExecutePsqlCommandAsync] Exception Message: {ex.Message}");
            _logger.LogInternalError($"[ExecutePsqlCommandAsync] Stack Trace: {ex.StackTrace}");
            _logger.LogInternalError($"[ExecutePsqlCommandAsync] Command that failed: {command}");
            _logger.LogInternalError($"[ExecutePsqlCommandAsync] ResourceId: {options.ResourceId}");
            _logger.LogInternalError($"[ExecutePsqlCommandAsync] Database: {options.Database}");
            _logger.LogInternalError($"[ExecutePsqlCommandAsync] Host: {host}");
            _logger.LogInternalError($"[ExecutePsqlCommandAsync] User: {options.User}");

            if (ex.InnerException != null)
            {
                _logger.LogInternalError($"[ExecutePsqlCommandAsync] Inner Exception: {ex.InnerException.GetType().FullName} - {ex.InnerException.Message}");
            }

            return new CliExecutionResult
            {
                ErrorType = CliErrorType.Other,
                Output = ex.Message
            };
        }
    }

    public static string? ValidateReadOnlyCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return "[Validation Failed]: Command cannot be empty.";

        var trimmed = command.Trim();

        // Allow safe psql meta-commands
        if (trimmed.StartsWith("\\"))
        {
            if (!SafeMetaCommandRegex.IsMatch(trimmed))
                return "[Validation Failed]: Meta-command not allowed.";
            if (ContainsDangerousShellChars(trimmed))
                return "[Validation Failed]: Command contains dangerous shell characters.";
            return null;
        }

        // Strip comments and quoted strings for analysis
        var stripped = StripStringsAndComments(trimmed);

        // Disallow multi-statement chains (but allow a single trailing semicolon)
        var body = stripped.Trim();
        if (body.EndsWith(";")) body = body[..^1];
        if (body.Contains(";"))
            return "[Validation Failed]: Multiple statements are not allowed.";

        // First token must be a safe read starter
        var first = GetFirstToken(body);
        if (first is null || !AllowedSqlStartingTokens.Contains(first, StringComparer.OrdinalIgnoreCase))
            return $"[Validation Failed]: Only read operations are allowed. Start with one of: {string.Join(", ", AllowedSqlStartingTokens)}";

        // EXPLAIN ANALYZE runs the query; keep it safe
        if (Regex.IsMatch(body, @"\bEXPLAIN\s+ANALYZE\b", RegexOptions.IgnoreCase))
            return "[Validation Failed]: EXPLAIN ANALYZE is not allowed.";

        // Block classic DDL/DML keywords (word-boundary)
        var forbidden = new[]
        {
            "INSERT","UPDATE","DELETE","DROP","CREATE","ALTER","TRUNCATE",
            "GRANT","REVOKE","COPY","VACUUM","ANALYZE","REINDEX","SET","RESET","COMMIT","ROLLBACK"
        };
        foreach (var kw in forbidden)
        {
            if (Regex.IsMatch(body, $@"\b{kw}\b", RegexOptions.IgnoreCase))
                return $"[Validation Failed]: Command contains forbidden keyword: {kw}.";
        }

        // Still block shell-ish stuff
        if (ContainsDangerousShellChars(stripped))
            return "[Validation Failed]: Command contains dangerous shell characters.";

        return null; // OK
    }

    private static bool ContainsDangerousShellChars(string text)
    {
        // Check for dangerous shell constructs while allowing valid SQL operators

        // Pipes: Allow SQL OR operator ||, but block single pipe |
        if (text.Contains("|") && !Regex.IsMatch(text, @"\|\|"))
            return true;

        // Output redirection: Look for patterns like "> file" or ">> file"  
        if (Regex.IsMatch(text, @">\s*[a-zA-Z_./\\]") || Regex.IsMatch(text, @">>\s*[a-zA-Z_./\\]"))
            return true;

        // Input redirection: Look for "< file" patterns, but allow SQL comparisons
        // This regex looks for < followed by what looks like a file path (contains / or . or starts with /)
        // while excluding SQL function calls like "< now()" or "< INTERVAL"
        if (Regex.IsMatch(text, @"<\s*[./\\]") || // < ./file or < /path or < \path  
            Regex.IsMatch(text, @"<\s+[a-zA-Z_]+\.[a-zA-Z]") || // < file.ext
            (Regex.IsMatch(text, @"<\s+[a-zA-Z_]+") && // < word
             !Regex.IsMatch(text, @"<\s+(?:now|current_timestamp|current_date|current_time|INTERVAL)\b", RegexOptions.IgnoreCase))) // but not SQL functions
            return true;

        // Command substitution, backticks, and logical shell chaining are always dangerous
        var alwaysBad = new[] { "&&", "$(", "`" };
        return alwaysBad.Any(text.Contains);
    }

    private static string StripStringsAndComments(string sql)
    {
        // Remove -- line comments
        sql = Regex.Replace(sql, @"--.*?$", "", RegexOptions.Multiline);

        // Remove /* block comments */
        sql = Regex.Replace(sql, @"/\*.*?\*/", "", RegexOptions.Singleline);

        // Remove single-quoted literals '...'
        sql = Regex.Replace(sql, @"'(?:''|[^'])*'", "''");

        // Remove double-quoted identifiers "..."
        sql = Regex.Replace(sql, @"""(?:\""|[^""])*""", "\"\"");

        // Best-effort: remove $$...$$ dollar-quoted blocks
        sql = Regex.Replace(sql, @"\$(?:[A-Za-z_]\w*)?\$.*?\$(?:[A-Za-z_]\w*)?\$", "''", RegexOptions.Singleline);

        return sql;
    }

    private static string? GetFirstToken(string sql)
    {
        var m = Regex.Match(sql, @"^\s*([A-Za-z]+)\b");
        return m.Success ? m.Groups[1].Value.ToUpperInvariant() : null;
    }

    private static string? GetFlexibleServerHost(string? resourceId)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            return null;
        }

        var m = Regex.Match(resourceId,
            @"/providers/Microsoft\.DBforPostgreSQL/flexibleServers/(?<name>[^/]+)",
            RegexOptions.IgnoreCase);

        if (!m.Success)
        {
            return null;
        }

        var name = m.Groups["name"].Value;
        var host = $"{name}.postgres.database.azure.com";
        return host;
    }

    private string? ExtractUserFromToken(string accessToken)
    {
        try
        {
            _logger.LogInternalInformation($"[ExtractUserFromToken] Starting JWT token parsing...");
            _logger.LogInternalInformation($"[ExtractUserFromToken] Environment: {(_hostEnvironment.IsDevelopment() ? "Development" : "Production")}");

            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadJwtToken(accessToken);

            // Log all claims for debugging
            var allClaims = jsonToken.Claims.Select(c => $"{c.Type}='{c.Value}'").ToArray();
            _logger.LogInternalInformation($"[ExtractUserFromToken] All JWT claims: {string.Join(", ", allClaims)}");

            // Environment-based extraction strategy
            if (_hostEnvironment.IsDevelopment())
            {
                _logger.LogInternalInformation($"[ExtractUserFromToken] Using development strategy - looking for unique_name claim");

                var uniqueName = jsonToken.Claims.FirstOrDefault(c => c.Type == "unique_name")?.Value;
                if (!string.IsNullOrEmpty(uniqueName))
                {
                    _logger.LogInternalInformation($"[ExtractUserFromToken] Found unique_name in development: {uniqueName}");
                    return uniqueName;
                }

                // Fallback to preferred_username for development
                var preferredUsername = jsonToken.Claims.FirstOrDefault(c => c.Type == "preferred_username")?.Value;
                if (!string.IsNullOrEmpty(preferredUsername))
                {
                    _logger.LogInternalInformation($"[ExtractUserFromToken] Found preferred_username in development: {preferredUsername}");
                    return preferredUsername;
                }
            }
            else
            {
                _logger.LogInternalInformation($"[ExtractUserFromToken] Using production strategy - looking for xms_mirid claim");

                // Production: Extract managed identity name from xms_mirid claim
                var xmsMirid = jsonToken.Claims.FirstOrDefault(c => c.Type == "xms_mirid")?.Value;
                if (!string.IsNullOrEmpty(xmsMirid))
                {
                    _logger.LogInternalInformation($"[ExtractUserFromToken] Found xms_mirid claim: {xmsMirid}");
                    var managedIdentityName = ExtractManagedIdentityNameFromResourceId(xmsMirid);
                    if (!string.IsNullOrEmpty(managedIdentityName))
                    {
                        _logger.LogInternalInformation($"[ExtractUserFromToken] Extracted managed identity name from xms_mirid: {managedIdentityName}");
                        return managedIdentityName;
                    }
                }
            }

            // Fallback strategy for both environments - try common claim types
            _logger.LogInternalInformation($"[ExtractUserFromToken] Primary strategy failed, trying fallback claims...");

            var name = jsonToken.Claims.FirstOrDefault(c => c.Type == "name")?.Value;
            if (!string.IsNullOrEmpty(name))
            {
                _logger.LogInternalInformation($"[ExtractUserFromToken] Found name claim: {name}");
                return name;
            }

            var clientId = jsonToken.Claims.FirstOrDefault(c => c.Type == "client_id")?.Value;
            if (!string.IsNullOrEmpty(clientId))
            {
                _logger.LogInternalInformation($"[ExtractUserFromToken] Found client_id claim: {clientId}");
                return clientId;
            }

            var oid = jsonToken.Claims.FirstOrDefault(c => c.Type == "oid")?.Value;
            if (!string.IsNullOrEmpty(oid))
            {
                _logger.LogInternalInformation($"[ExtractUserFromToken] Found oid claim: {oid}");
                return oid;
            }

            // If no suitable claims found, log available claims for debugging
            var availableClaims = string.Join(", ", jsonToken.Claims.Select(c => c.Type));
            _logger.LogInternalWarning($"[ExtractUserFromToken] No suitable username claim found. Available claims: {availableClaims}");

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError($"[ExtractUserFromToken] Failed to parse JWT token: {ex.Message}");
            return null;
        }
    }

    private string? ExtractManagedIdentityNameFromResourceId(string resourceId)
    {
        if (string.IsNullOrEmpty(resourceId))
        {
            _logger.LogInternalError($"[ExtractManagedIdentityNameFromResourceId] ResourceId is null or empty");
            return null;
        }

        _logger.LogInternalInformation($"[ExtractManagedIdentityNameFromResourceId] Parsing resourceId: '{resourceId}'");

        // Extract managed identity name from resource path like:
        // /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.ManagedIdentity/userAssignedIdentities/{identityName}
        var match = Regex.Match(resourceId,
            @"/providers/Microsoft\.ManagedIdentity/userAssignedIdentities/(?<name>[^/]+)",
            RegexOptions.IgnoreCase);

        if (!match.Success)
        {
            _logger.LogInternalError($"[ExtractManagedIdentityNameFromResourceId] Regex match failed for resourceId: '{resourceId}'");
            return null;
        }

        var identityName = match.Groups["name"].Value;
        _logger.LogInternalInformation($"[ExtractManagedIdentityNameFromResourceId] Extracted identity name: '{identityName}'");
        return identityName;
    }

    private static CliExecutionResult Fail(string message) => new()
    {
        ErrorType = CliErrorType.ValidationError,
        Output = "[Validation Failed]: " + message
    };
}
