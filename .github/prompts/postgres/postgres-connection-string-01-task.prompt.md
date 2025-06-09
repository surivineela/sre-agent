---
mode: 'agent'
---

Below is a writeup of how PostgreSQL connection data can appear in application settings. Use this guide to inform
your development of advanced capabilities for detecting and parsing PostgreSQL information as part of the Agent SRE's
crawler system. The purpose of this is to identify when an application, e.g. in Kubernetes, App Services, Container Apps, etc.,
has a PostgreSQL database connection configured, and to extract the relevant connection parameters.

Modify PostgreSqlConnectionStringHelper to handle these cases. You may need to change how the pattern of the helper currently is
implemented - right now it checks specific app setting values, but there's other ways that connections can be specified
that span multiple settings. Modify the call sites to the helper to handle these cases as well.

There's additional information that is gathered from the connection string that is useful to encode in the knowledge graph,
either on the edge connecting the app service to the database, or on the database node itself. Ensure all useful information
is captured.

Modify the PostgreSqlConnectionStringHelperTests to cover all cases. The current test suite is broken so feel free to completely
rewrite it. The goal is to ensure that the helper can handle all possible cases described below, and that these cases are well tested.

Note that we currently can only gather app settings/environment variables, so some of the ways connection strings are
determined below will not be possible to detect in the current architecture. Only cover those cases that are possible
and write a report about what cases we are still missing.



----

# PostgreSQL Connection String Patterns and Properties for Azure SRE Agent System

## Overview

This document provides comprehensive coverage of PostgreSQL connection string patterns and environment variable configurations that the Azure SRE agent system must detect and parse when crawling infrastructure resources. This information is used to build the knowledge graph that represents the topology and relationships between applications and databases.

## Detection Heuristics

Before parsing, use these quick detection patterns to identify PostgreSQL connection information:

```csharp
public enum PostgreSqlConnectionFormat
{
    URL,                // postgresql://, postgres://, jdbc:postgresql://
    SemicolonList,      // .NET/Npgsql format with semicolons
    KeyValueList,       // libpq conninfo format with spaces
    ServiceName,        // DSN/service file reference
    JSONPayload,        // JSON object with connection properties
    IndividualVars,     // Split PG* environment variables
    Unknown
}

public PostgreSqlConnectionFormat DetectFormat(string value, string keyName = null)
{
    if (string.IsNullOrEmpty(value)) return PostgreSqlConnectionFormat.Unknown;
    
    // URL formats
    if (value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("jdbc:postgresql://", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("postgresql+", StringComparison.OrdinalIgnoreCase))
        return PostgreSqlConnectionFormat.URL;
    
    // JSON format
    var trimmed = value.Trim();
    if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
        return PostgreSqlConnectionFormat.JSONPayload;
    
    // Service name (simple word)
    if (Regex.IsMatch(value, @"^\w+$") && 
        (keyName?.Equals("PGSERVICE", StringComparison.OrdinalIgnoreCase) == true ||
         keyName?.Equals("service", StringComparison.OrdinalIgnoreCase) == true))
        return PostgreSqlConnectionFormat.ServiceName;
    
    // Semicolon list (typical for .NET/Npgsql)
    if (value.Contains(';') && value.Contains('=') && 
        value.Count(c => c == ';') >= value.Count(c => c == ' '))
        return PostgreSqlConnectionFormat.SemicolonList;
    
    // Key-value list (libpq conninfo)
    if (value.Contains("host=", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("dbname=", StringComparison.OrdinalIgnoreCase))
        return PostgreSqlConnectionFormat.KeyValueList;
    
    return PostgreSqlConnectionFormat.Unknown;
}
```

## Connection String Formats

### 1. PostgreSQL URI Format (RFC 3986)

**Patterns**: 
- `postgresql://[user[:password]@][host1[:port1]][,host2[:port2],...][/database][?param1=value1&...]`
- `postgres://[user[:password]@][host1[:port1]][,host2[:port2],...][/database][?param1=value1&...]`

**Python Driver Variants**:
```
postgresql+psycopg2://user:password@host:port/database
postgresql+asyncpg://user:password@host:port/database
postgresql+psycopg://user:password@host:port/database
```

**Multi-Host High Availability Examples**:
```
postgresql://user:password@host1:5432,host2:5432/database?sslmode=require&target_session_attrs=read-write
postgres://user:pass@primary.db.com:5432,secondary.db.com:5432/mydb?target_session_attrs=primary
postgresql://user:pass@node1:5432,node2:5432,node3:5432/db?loadBalanceHosts=true
```

**Pooling and Performance Parameters in URLs**:
```
postgresql://user:pass@host:5432/db?pool=true&pool_max_conns=20&pool_min_conns=4&pool_max_conn_lifetime=3600
postgres://user:pass@host:5432/db?sslmode=require&connect_timeout=10&application_name=myapp&keepalives=1&keepalives_idle=600
```

**Unix Socket Examples**:
```
postgresql:///database_name?host=/var/run/postgresql
postgresql://user@/database?host=/tmp&port=5433
```

### 2. Azure PostgreSQL Specific Formats

**Azure Flexible Server**:
```
postgresql://username@servername:password@servername.postgres.database.azure.com:5432/database_name?sslmode=require
postgres://username%40servername:password@servername.postgres.database.azure.com:5432/database_name
```

**Azure Single Server (Legacy)**:
```
postgresql://username@servername:password@servername.postgres.database.azure.com:5432/database_name?sslmode=require
```

**Azure Cosmos DB for PostgreSQL (Hyperscale)**:
```
postgresql://citus:password@coordinatorname.postgres.database.azure.com:5432/citus?sslmode=require
```

**Azure Managed Identity**:
```
postgresql://managed_identity_user@servername.postgres.database.azure.com:5432/database?sslmode=require&authtype=managedidentity
```

### 3. Key-Value Parameter Format (libpq conninfo)

**Pattern**: `host=hostname port=5432 dbname=database user=username password=password`

**Single Host Examples**:
```
host=localhost port=5432 dbname=mydb user=myuser password=mypass sslmode=require
host=server.postgres.database.azure.com port=5432 dbname=mydb user=myuser@myserver password=mypass sslmode=require
```

**Multi-Host Examples**:
```
host=primary.db.com,secondary.db.com port=5432,5432 dbname=mydb user=myuser password=mypass target_session_attrs=read-write
host=node1,node2,node3 port=5432 dbname=mydb user=myuser password=mypass sslmode=require
```

### 4. JDBC URL Format

**Pattern**: `jdbc:postgresql://[host]:[port]/[database][?param1=value1&...]`

```
jdbc:postgresql://hostname:5432/database_name
jdbc:postgresql://hostname:5432/database_name?user=username&password=password&ssl=true
jdbc:postgresql://server.postgres.database.azure.com:5432/database?user=username@servername&password=password&sslmode=require
jdbc:postgresql://host1:5432,host2:5432/database?targetServerType=primary
```

### 5. ODBC Connection String Format

```
DRIVER={PostgreSQL ANSI};SERVER=hostname;PORT=5432;DATABASE=database_name;UID=username;PWD=password;
DRIVER={PostgreSQL Unicode};SERVER=server.postgres.database.azure.com;PORT=5432;DATABASE=database_name;UID=username@servername;PWD=password;SSLMode=require;
```

### 6. .NET/Entity Framework Formats (Semicolon-Separated)

**Npgsql Connection String**:
```
Host=hostname;Port=5432;Database=database_name;Username=username;Password=password;
Host=server.postgres.database.azure.com;Port=5432;Database=database_name;Username=username@servername;Password=password;SSL Mode=Require;
Host=host1,host2;Port=5432;Database=database_name;Username=username;Password=password;Target Server Type=Primary;
```

**Npgsql with Pooling and Performance Settings**:
```
Host=hostname;Port=5432;Database=database_name;Username=username;Password=password;Pooling=true;Min Pool Size=1;Max Pool Size=20;Connection Lifetime=15;Connection Idle Lifetime=300;
Host=server.postgres.database.azure.com;Database=database;Username=user@server;Password=password;SSL Mode=Require;Timeout=30;Command Timeout=300;Keep Alive=30;
```

**Entity Framework Connection String**:
```
metadata=res://*/Model.csdl|res://*/Model.ssdl|res://*/Model.msl;provider=Npgsql;provider connection string="Host=hostname;Port=5432;Database=database_name;Username=username;Password=password;"
```

### 7. JSON Format

Common in Node.js applications and configuration management:

```json
{
  "host": "server.postgres.database.azure.com",
  "port": 5432,
  "database": "mydatabase", 
  "user": "username@servername",
  "password": "password",
  "sslmode": "require",
  "application_name": "myapp"
**Node.js Configuration with Pooling**:
```json
{
  "host": "server.postgres.database.azure.com",
  "port": 5432,
  "database": "mydatabase", 
  "user": "username@servername",
  "password": "password",
  "sslmode": "require",
  "application_name": "myapp",
  "max": 20,
  "min": 4,
  "idleTimeoutMillis": 1000,
  "connectionTimeoutMillis": 2000,
  "keepAlive": true
}
```

**Multi-Host JSON**:
```json
{
  "hosts": ["primary.db.com", "secondary.db.com"],
  "port": 5432,
  "database": "mydatabase",
  "user": "username",
  "password": "password",
  "target_session_attrs": "read-write"
}
```

### 8. Service File Indirection

**Service Name Reference**:
```
service=analytics_ro
service=production_db
```

**Service File Content** (`~/.pg_service.conf` or path in `PGSERVICEFILE`):
```ini
[analytics_ro]
host=analytics.postgres.database.azure.com
port=5432
dbname=analytics
user=readonly_user@analytics
sslmode=require

[production_db]
host=prod-primary.db.com,prod-secondary.db.com
port=5432
dbname=production
user=app_user
target_session_attrs=read-write
```

## Environment Variable Patterns

### 1. Standard PostgreSQL Environment Variables

Applications can use individual environment variables that libpq automatically combines:

| Variable | Description | Example |
|----------|-------------|---------|
| `PGHOST` | Database host/server (comma-separated for multiple) | `server.postgres.database.azure.com` or `host1,host2` |
| `PGPORT` | Port number(s) (comma-separated for multiple hosts) | `5432` or `5432,5433` |
| `PGDATABASE` | Database name | `mydatabase` |
| `PGUSER` | Username | `myuser@myserver` |
| `PGPASSWORD` | Password | `mypassword` |
| `PGPASSFILE` | Password file path | `/home/user/.pgpass` |
| `PGSERVICE` | Service name for service file lookup | `analytics_ro` |
| `PGSERVICEFILE` | Path to service file | `/etc/postgresql/pg_service.conf` |
| `PGSSLMODE` | SSL mode | `require`, `prefer`, `disable`, `verify-ca`, `verify-full` |
| `PGSSLCERT` | Client certificate file | `/path/to/client-cert.pem` |
| `PGSSLKEY` | Client private key file | `/path/to/client-key.pem` |
| `PGSSLROOTCERT` | Root certificate file | `/path/to/ca-cert.pem` |
| `PGGSSENCMODE` | GSS encryption mode | `disable`, `prefer`, `require` |
| `PGCHANNELBINDING` | Channel binding | `disable`, `prefer`, `require` |
| `PGTARGETSESSIONATTRS` | Target session attributes | `primary`, `standby`, `read-write`, `any` |
| `PGAPPNAME` | Application name | `myapp` |
| `PGCONNECT_TIMEOUT` | Connection timeout | `10` |

### 2. Common Connection String Environment Variable Names

**Direct Connection String Variables**:
```
DATABASE_URL=postgresql://user:password@host:port/database
POSTGRES_URL=postgresql://user:password@host:port/database
POSTGRES_CONNECTION_STRING=postgresql://user:password@host:port/database
DB_CONNECTION_STRING=postgresql://user:password@host:port/database
POSTGRESQL_URL=postgresql://user:password@host:port/database
DB_URL=postgresql://user:password@host:port/database
```

**Third-Party Service Patterns**:
```
HEROKU_POSTGRESQL_CRIMSON_URL=postgres://user:pass@host:port/db
HEROKU_POSTGRESQL_OLIVE_URL=postgres://user:pass@host:port/db
ELEPHANTSQL_URL=postgres://user:pass@host:port/db
SUPABASE_DB_URL=postgresql://user:pass@host:port/db
NEON_DATABASE_URL=postgresql://user:pass@host:port/db
```

### 3. Application/Framework-Specific Environment Variables

**Django**:
```
DATABASE_URL=postgres://user:password@host:port/database
DB_NAME=database_name
DB_USER=username
DB_PASSWORD=password
DB_HOST=hostname
DB_PORT=5432
```

**Rails**:
```
DATABASE_URL=postgresql://user:password@host:port/database
RAILS_ENV_DATABASE_URL=postgresql://user:password@host:port/database
```

**Node.js/JavaScript**:
```
DATABASE_URL=postgres://user:password@host:port/database
PG_CONNECTION_STRING=postgres://user:password@host:port/database
POSTGRES_URL=postgres://user:password@host:port/database
DB_CONNECTION_STRING=postgres://user:password@host:port/database
```

**Spring Boot/Java**:
```
SPRING_DATASOURCE_URL=jdbc:postgresql://host:port/database
SPRING_DATASOURCE_USERNAME=username
SPRING_DATASOURCE_PASSWORD=password
DATASOURCE_URL=jdbc:postgresql://host:port/database
JDBC_DATABASE_URL=jdbc:postgresql://host:port/database
```

**Python**:
```
DATABASE_URL=postgresql://user:password@host:port/database
POSTGRES_URL=postgresql://user:password@host:port/database
DB_URL=postgresql://user:password@host:port/database
```

**Go**:
```
DATABASE_URL=postgres://user:password@host:port/database
POSTGRES_URL=postgres://user:password@host:port/database
```

**PHP**:
```
DATABASE_URL=pgsql://user:password@host:port/database
PDO_URL=pgsql:host=hostname;port=5432;dbname=database_name
```

### 4. Container and Kubernetes Patterns

**Docker Environment Variables**:
```
POSTGRES_HOST=postgres-service
POSTGRES_PORT=5432
POSTGRES_DB=database_name
POSTGRES_USER=username
POSTGRES_PASSWORD=password
```

**Kubernetes ConfigMap/Secret References**:
```yaml
# In deployment.yaml
env:
- name: DATABASE_URL
  valueFrom:
    secretKeyRef:
      name: postgres-secret
      key: database-url
- name: PGHOST
  valueFrom:
    configMapKeyRef:
      name: postgres-config
      key: host
- name: PGUSER
  valueFrom:
    secretKeyRef:
      name: postgres-secret
      key: username
- name: PGPASSWORD
  valueFrom:
    secretKeyRef:
      name: postgres-secret
      key: password
```

**Azure Container Apps Environment Variables**:
```
DATABASE_URL=postgresql://user:password@host/database
POSTGRES_CONNECTION_STRING=postgresql://user:password@host/database
```

### 5. Azure-Specific Environment Variables

**Azure App Service Connection String Prefixes**:
```
POSTGRESQLCONNSTR_MyDatabase=Host=server.postgres.database.azure.com;Database=database;Username=user@server;Password=password;Sslmode=Require
CUSTOMCONNSTR_PostgreSQL=Host=server.postgres.database.azure.com;Database=database;Username=user@server;Password=password;Sslmode=Require
SQLAZURECONNSTR_PostgreSQL=Host=server.postgres.database.azure.com;Database=database;Username=user@server;Password=password;Sslmode=Require
```

**Azure Functions**:
```
PostgreSQL_CONNECTION=Host=server.postgres.database.azure.com;Database=database;Username=user@server;Password=password;Sslmode=Require
```

**Azure Kubernetes Service (AKS)**:
```
AZURE_POSTGRESQL_HOST=server.postgres.database.azure.com
AZURE_POSTGRESQL_USER=user@server
AZURE_POSTGRESQL_PASSWORD=password
AZURE_POSTGRESQL_DATABASE=database
```

### 6. Cloud Provider Service Bindings

**Azure Service Connector**:
```json
{
  "AZURE_POSTGRESQL_CONNECTIONSTRING": "Host=server.postgres.database.azure.com;Database=database;Username=user;Password=password;Sslmode=Require",
  "AZURE_POSTGRESQL_HOST": "server.postgres.database.azure.com",
  "AZURE_POSTGRESQL_USER": "user",
  "AZURE_POSTGRESQL_PASSWORD": "password",
  "AZURE_POSTGRESQL_DATABASE": "database"
}
```

**Managed Identity Authentication**:
```
Host=server.postgres.database.azure.com;Database=database;Username=managed_identity_user;Authentication=Active Directory Default;Sslmode=Require
```

## Canonical Parameter Names & Common Aliases

When parsing connection strings across different formats, normalize these parameter names to their canonical forms:

| Canonical | Aliases / Variations | Notes |
|-----------|---------------------|-------|
| `host` | `hostaddr`, `server`, `hostname`, `Host`, `SERVER` | May contain comma-separated list for HA |
| `port` | `Port`, `PORT` | Default: 5432 |
| `dbname` | `database`, `db`, `Database`, `DATABASE` | Database name |
| `user` | `username`, `Username`, `UID` | Authentication username |
| `password` | `pwd`, `pass`, `Password`, `PWD` | Authentication password |
| `sslmode` | `SSL Mode`, `ssl`, `SSLMode` | Values: `disable`, `allow`, `prefer`, `require`, `verify-ca`, `verify-full` |
| `gssencmode` | `GSS Encryption Mode` | GSS encryption: `disable`, `prefer`, `require` |
| `channel_binding` | `Channel Binding` | Values: `disable`, `prefer`, `require` |
| `target_session_attrs` | `Target Server Type`, `targetServerType` | Values: `primary`, `standby`, `read-write`, `any` |
| `application_name` | `ApplicationName`, `Application Name` | Application identifier |
| `connect_timeout` | `Connection Timeout`, `Timeout` | Connection timeout in seconds |
| `command_timeout` | `Command Timeout` | Query timeout in seconds |
| `service` | — | DSN/service file reference |
| `pooling` | `Pooling` | Connection pooling: `true`, `false` |
| `min_pool_size` | `Min Pool Size`, `MinPoolSize` | Minimum connections in pool |
| `max_pool_size` | `Max Pool Size`, `MaxPoolSize` | Maximum connections in pool |
| `keepalives` | `TCP Keepalives Enabled`, `keepAlive` | TCP keepalives: `1`, `0`, `true`, `false` |
| `keepalives_idle` | `TCP Keepalives Idle` | Seconds before keepalive |
| `keepalives_interval` | `TCP Keepalives Interval` | Interval between keepalives |
| `keepalives_count` | `TCP Keepalives Count` | Number of keepalives before disconnect |
| `load_balance_hosts` | `Load Balance Hosts`, `loadBalanceHosts` | Host load balancing: `true`, `false` |
| `pool` | — | Connection pooling for drivers: `true`, `false` |

### Azure-Specific Parameters

| Canonical | Aliases | Notes |
|-----------|---------|-------|
| `authentication` | `Authentication` | Azure AD authentication method |
| `encrypt` | `Encrypt` | Data encryption setting |
| `trust_server_certificate` | `Trust Server Certificate` | Certificate validation bypass |

## Connection Properties for Knowledge Graph

When parsing PostgreSQL connection strings, the following properties should be extracted and encoded as edge or node properties in the knowledge graph:

### 1. Basic Connection Properties

| Property | Description | Knowledge Graph Usage |
|----------|-------------|---------------------|
| `db.hosts` | Database server hostname(s) - split multi-host configurations | Primary identifier for server lookup; HA detection |
| `db.port` | Connection port | Service endpoint information |
| `db.name` | Database name | Specific database being accessed |
| `auth.user` | Database user | Access identity information |
| `auth.application_name` | Application identifier | Connection source tracking |

### 2. Security and Transport Properties

| Property | Values | Knowledge Graph Encoding |
|----------|--------|-------------------------|
| `transport.encryption` | `sslmode`: `disable`, `allow`, `prefer`, `require`, `verify-ca`, `verify-full` | `sslRequired: true/false`, `sslMode: value` |
| `transport.gss_encryption` | `gssencmode`: `disable`, `prefer`, `require` | `gssEncryption: enabled/disabled` |
| `transport.channel_binding` | `disable`, `prefer`, `require` | `channelBinding: enabled/disabled` |
| `auth.client_cert` | Client certificate path | `clientCertAuth: true` |
| `auth.client_key` | Client private key path | `clientCertAuth: true` |
| `auth.ca_cert` | Root certificate path | `caCertValidation: true` |

### 3. Authentication Properties

| Property | Values | Knowledge Graph Encoding |
|----------|--------|-------------------------|
| `auth.method` | Password, Certificate, Azure AD, Managed Identity, GSS | `authType: password/certificate/azuread/managedidentity/gss` |
| `auth.azure_domain` | `@servername` suffix in username | `azureAdAuth: true` |
| `auth.managed_identity` | Special usernames or auth parameters | `managedIdentityAuth: true` |

### 4. High Availability and Routing Properties

| Property | Values | Knowledge Graph Usage |
|----------|--------|---------------------|
| `session_attrs` | `target_session_attrs`: `primary`, `standby`, `read-write`, `any` | Identify read-only replica usage |
| `load_balancing` | `loadBalanceHosts`, `load_balance_hosts` | HA configuration detection |
| `failover_config` | Multiple hosts defined | Failover capability tracking |

### 5. Connection Pool Properties

| Property | Description | Knowledge Graph Usage |
|----------|-------------|---------------------|
| `pooling.enabled` | Connection pooling enabled | Resource management info |
| `pooling.min_size` | Minimum pool size | Capacity planning |
| `pooling.max_size` | Maximum pool size | Resource utilization tracking |
| `connection.timeout` | Connection timeout | Performance characteristics |
| `command.timeout` | Query timeout | Performance characteristics |

### 6. Azure-Specific Properties

| Property | Description | Knowledge Graph Encoding |
|----------|-------------|-------------------------|
| `azure.server_type` | Flexible Server, Single Server, Hyperscale | `serverType: flexible/single/hyperscale` |
| `azure.managed` | `.postgres.database.azure.com` domain | `azureManaged: true` |
| `azure.resource_group` | Extracted from server name pattern | `resourceGroup: name` |
| `azure.subscription` | Contextual from resource discovery | `subscription: id` |

### 7. Driver and Framework Detection

| Property | Detection Heuristics | Knowledge Graph Usage |
|----------|---------------------|---------------------|
| `driver.family` | `jdbc:` → Java, `SSL Mode=` → .NET, camelCase JSON → Node.js | Tailored remediation recommendations |
| `driver.technology` | `postgresql+psycopg2://` → Python/psycopg2, `postgresql+asyncpg://` → Python/asyncpg | Framework-specific guidance |
| `config.format` | URI, Key-Value, Semicolon, JSON, Service | Connection source type |

### 8. Source and Traceability Properties

| Property | Description | Knowledge Graph Usage |
|----------|-------------|---------------------|
| `source.location` | Environment variable name, config file path | Traceability for remediation |
| `source.type` | `appSetting`, `environment`, `configmap`, `secret` | Source classification |
| `source.name` | Variable name or setting key | Specific source identifier |

## Environment Variable Merge Precedence and Conflict Handling

When multiple PostgreSQL connection sources are present, apply this precedence order:

### 1. Merge Order (Highest to Lowest Priority)

1. **Explicit Connection String**: Direct connection string values take highest precedence
2. **Service File Resolution**: If `service=name` or `PGSERVICE` is used, resolve from service file
3. **Individual PG* Environment Variables**: libpq standard environment variables
4. **Default Values**: Driver and library defaults

### 2. Implementation Strategy

```csharp
public Dictionary<string, string> MergeConnectionParameters(
    string connectionString, 
    Dictionary<string, string> environmentVars,
    string serviceName = null)
{
    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    
    // 1. Start with libpq environment variable defaults
    AddPgEnvironmentVariables(result, environmentVars);
    
    // 2. Overlay service file parameters if present
    if (!string.IsNullOrEmpty(serviceName))
    {
        var serviceParams = ResolveServiceFile(serviceName, environmentVars);
        OverlayParameters(result, serviceParams);
    }
    
    // 3. Overlay explicit connection string parameters (highest priority)
    if (!string.IsNullOrEmpty(connectionString))
    {
        var connectionParams = ParseConnectionString(connectionString);
        OverlayParameters(result, connectionParams);
    }
    
    return result;
}

private void AddPgEnvironmentVariables(Dictionary<string, string> result, Dictionary<string, string> env)
{
    var pgVarMap = new Dictionary<string, string>
    {
        ["PGHOST"] = "host",
        ["PGPORT"] = "port", 
        ["PGDATABASE"] = "dbname",
        ["PGUSER"] = "user",
        ["PGPASSWORD"] = "password",
        ["PGSSLMODE"] = "sslmode",
        ["PGGSSENCMODE"] = "gssencmode",
        ["PGCHANNELBINDING"] = "channel_binding",
        ["PGTARGETSESSIONATTRS"] = "target_session_attrs",
        ["PGAPPNAME"] = "application_name",
        ["PGCONNECT_TIMEOUT"] = "connect_timeout",
        ["PGSERVICE"] = "service",
        ["PGSERVICEFILE"] = "servicefile"
    };
    
    foreach (var kvp in pgVarMap)
    {
        if (env.TryGetValue(kvp.Key, out var value) && !string.IsNullOrEmpty(value))
        {
            result[kvp.Value] = value;
        }
    }
}
```

### 3. Conflict Resolution Rules

- **Host Lists**: Merge multiple hosts from different sources (comma-separated)
- **Timeouts**: Use the most restrictive (lowest) timeout value
- **Security Settings**: Use the most secure setting when conflicts occur
- **Authentication**: Explicit credentials override environment defaults

## Enhanced Driver Family Detection

Detect driver families using multiple heuristics for better knowledge graph classification:

### 1. URL Scheme Detection

```csharp
public string DetectDriverFamily(string connectionString, string keyName = null)
{
    if (string.IsNullOrEmpty(connectionString)) return "unknown";
    
    // URL scheme patterns
    if (connectionString.StartsWith("jdbc:postgresql://")) return "java";
    if (connectionString.StartsWith("postgresql+psycopg2://")) return "python-psycopg2";
    if (connectionString.StartsWith("postgresql+asyncpg://")) return "python-asyncpg";
    if (connectionString.StartsWith("postgresql+psycopg://")) return "python-psycopg3";
    if (connectionString.StartsWith("pgsql://")) return "php";
    
    // Format-based detection
    if (connectionString.Contains("SSL Mode=") || connectionString.Contains("Pooling=")) return "dotnet";
    if (connectionString.Contains("DRIVER={PostgreSQL")) return "odbc";
    
    // Key name patterns
    if (keyName != null)
    {
        if (keyName.StartsWith("SPRING_DATASOURCE_")) return "java-spring";
        if (keyName.Contains("RAILS_")) return "ruby-rails";
        if (keyName.StartsWith("DB_") && !keyName.Contains("URL")) return "generic-split";
    }
    
    // JSON structure patterns
    if (connectionString.TrimStart().StartsWith("{"))
    {
        if (connectionString.Contains("\"ssl\"") && connectionString.Contains("\"database\"")) return "nodejs";
        if (connectionString.Contains("\"sslmode\"")) return "python";
    }
    
    return "standard";
}
```

### 2. Case-Insensitive Parameter Handling

All semicolon-separated formats (Npgsql, .NET) should be parsed case-insensitively:

```csharp
public Dictionary<string, string> ParseSemicolonFormat(string connectionString)
{
    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
    
    foreach (var part in parts)
    {
        var equalIndex = part.IndexOf('=');
        if (equalIndex > 0)
        {
            var key = part.Substring(0, equalIndex).Trim();
            var value = part.Substring(equalIndex + 1).Trim();
            
            // Handle space-containing keys like "SSL Mode"
            result[key] = value;
        }
    }
    
    return result;
}
```

## Minimal Parsing Workflow

The other agent identified this essential workflow for comprehensive parsing:

1. **Service Resolution**: If string is only a DSN/service name → resolve via service file
2. **Format Detection**: Use detection heuristics to identify syntax type
3. **Primary Parsing**: Parse into canonical parameter dictionary
4. **Environment Overlay**: Apply any `PG*` environment variables set at runtime
5. **Azure Expansion**: Process Azure-specific prefixes (`POSTGRESQLCONNSTR_*`, `CUSTOMCONNSTR_*`)
6. **Knowledge Graph Mapping**: Populate KG attributes via mapping tables above

## Detection and Parsing Implementation Guidelines

### 1. Environment Variable Scanning Priorities

1. **Direct Connection Strings**: Look for variables ending with `_URL`, `_CONNECTION_STRING`, `_DATABASE_URL`
2. **PostgreSQL-specific variables**: Scan for `PG*` prefixed variables
3. **Framework-specific patterns**: Check for framework naming conventions
4. **Composite variables**: Look for separate host/user/password/database variables

### 2. Enhanced Connection String Format Detection

```csharp
public bool IsPostgreSqlConnectionString(string value, string keyName = null)
{
    if (string.IsNullOrEmpty(value)) return false;
    
    // Strip potential Azure App Service quotes
    value = value.Trim().Trim('"');
    
    // URI formats (including Python driver variants)
    if (value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("psql://", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("postgresql+", StringComparison.OrdinalIgnoreCase))
        return true;
    
    // JDBC format
    if (value.StartsWith("jdbc:postgresql://", StringComparison.OrdinalIgnoreCase))
        return true;
    
    // Service name detection (simple word with appropriate key)
    if (Regex.IsMatch(value, @"^\w+$") && 
        (keyName?.Equals("PGSERVICE", StringComparison.OrdinalIgnoreCase) == true ||
         keyName?.Equals("service", StringComparison.OrdinalIgnoreCase) == true ||
         value.Contains("service=", StringComparison.OrdinalIgnoreCase)))
        return true;
    
    // JSON format detection
    var trimmed = value.Trim();
    if ((trimmed.StartsWith("{") && trimmed.EndsWith("}")) ||
        (trimmed.StartsWith("[") && trimmed.EndsWith("]")))
    {
        // Additional validation for PostgreSQL-specific JSON keys
        return trimmed.Contains("\"host\"") || trimmed.Contains("\"dbname\"") || 
               trimmed.Contains("\"database\"") || trimmed.Contains("postgres");
    }
    
    // Key-value format with PostgreSQL indicators
    if ((value.Contains("host=", StringComparison.OrdinalIgnoreCase) ||
         value.Contains("dbname=", StringComparison.OrdinalIgnoreCase)) &&
        (value.Contains(".postgres.database.azure.com", StringComparison.OrdinalIgnoreCase) ||
         value.Contains("postgresql", StringComparison.OrdinalIgnoreCase) ||
         value.Contains("port=5432", StringComparison.OrdinalIgnoreCase)))
        return true;
    
    // Semicolon-separated format (Npgsql/.NET style)
    if (value.Contains(';') && value.Contains('=') && 
        value.Count(c => c == ';') >= value.Count(c => c == ' ') &&
        (value.Contains("Host=", StringComparison.OrdinalIgnoreCase) ||
         value.Contains("Database=", StringComparison.OrdinalIgnoreCase) ||
         value.Contains("Server=", StringComparison.OrdinalIgnoreCase)))
        return true;
    
    // ODBC format
    if (value.Contains("DRIVER={PostgreSQL", StringComparison.OrdinalIgnoreCase))
        return true;
    
    // Azure-specific patterns (even without other indicators)
    if (value.Contains(".postgres.database.azure.com", StringComparison.OrdinalIgnoreCase))
        return true;
    
    // Check for Azure connection string prefixes in key name
    if (keyName != null && 
        (keyName.StartsWith("POSTGRESQLCONNSTR_", StringComparison.OrdinalIgnoreCase) ||
         keyName.StartsWith("CUSTOMCONNSTR_", StringComparison.OrdinalIgnoreCase) ||
         keyName.Contains("POSTGRESQL", StringComparison.OrdinalIgnoreCase) ||
         keyName.Contains("POSTGRES", StringComparison.OrdinalIgnoreCase)))
        return true;
    
    return false;
}
```

### 3. Property Extraction Guidelines

1. **Always extract**: host(s), port, database, authentication method
2. **Security focus**: SSL settings, certificate authentication, encryption modes
3. **Azure integration**: Detect Azure-managed services, managed identity usage
4. **Performance indicators**: Connection pooling, timeouts, load balancing
5. **Compliance tracking**: Encryption requirements, authentication strength
6. **High Availability**: Multi-host configurations, session routing preferences
7. **Driver Detection**: Framework-specific patterns for tailored recommendations

### 4. Advanced Implementation Notes

**Use Official Parsers Where Possible**:
- Python: `psycopg.conninfo.conninfo_to_dict()` for libpq-style parsing
- Go: `pq.ParseURL()` for PostgreSQL URL parsing
- .NET: `NpgsqlConnectionStringBuilder` for Npgsql format parsing

**Azure App Service Specific Handling**:
- Connection string values are often wrapped in double quotes - strip them during parsing
- Look for Azure-specific prefixes: `POSTGRESQLCONNSTR_`, `CUSTOMCONNSTR_`, `SQLAZURECONNSTR_`

**Parsing Strategy**:
- Be liberal in splitting (`;`, space, `&`, `?`) but conservative in merging
- Remember that query-string parameters can use `;` as separators in some contexts
- Always normalize parameter names to canonical forms before Knowledge Graph storage

**Security Considerations**:
- Store connection strings with passwords redacted: `postgresql://user:***@host:5432/db`
- Retain raw connection string structure for future re-parsing needs
- Track password presence vs. certificate-based authentication

### Managed Identity Detection

Identify managed identity usage patterns:
- Username contains "managed_identity" or special Azure patterns
- Authentication parameter set to "Active Directory Default" or similar
- Absence of password with Azure-hosted servers

## Implementation Best Practices and Parser Reuse

### 1. Leverage Official Parsers

**Python**: Use `psycopg.conninfo.conninfo_to_dict()` for robust libpq-style parsing:
```python
import psycopg
from psycopg.conninfo import conninfo_to_dict

# Parse libpq connection string
params = conninfo_to_dict("host=localhost port=5432 dbname=mydb user=user")
```

**Go**: Use `github.com/lib/pq` for PostgreSQL URL parsing:
```go
import "github.com/lib/pq"

// Parse PostgreSQL URL
config, err := pq.ParseURL("postgres://user:pass@localhost/db?sslmode=require")
```

**C#/.NET**: Use `NpgsqlConnectionStringBuilder` for Npgsql format:
```csharp
var builder = new NpgsqlConnectionStringBuilder(connectionString);
var host = builder.Host;
var database = builder.Database;
```

### 2. Azure App Service Specific Parsing Considerations

Azure App Service often wraps connection string values in double quotes. Always strip them:

```csharp
private string PreprocessAzureConnectionString(string connectionString)
{
    if (string.IsNullOrEmpty(connectionString)) return connectionString;
    
    // Strip outer quotes that Azure App Service may add
    var trimmed = connectionString.Trim();
    if (trimmed.StartsWith("\"") && trimmed.EndsWith("\"") && trimmed.Length > 1)
    {
        return trimmed.Substring(1, trimmed.Length - 2);
    }
    
    return trimmed;
}
```

### 3. Robust Parsing Strategy

**Liberal in Splitting, Conservative in Merging**:
- Accept multiple separators: `;`, space, `&`, `?`
- Be aware that query-string parameters can use `;` as separators in some contexts
- Always validate parsed parameters before using them

**Security-First Approach**:
- Never log actual passwords - always redact before storing
- Track presence of credentials vs. certificate-based authentication
- Flag unencrypted connections for security review

### 4. Connection String Storage and Redaction

Store connection strings with passwords redacted for future analysis:

```csharp
private string RedactConnectionString(string connectionString)
{
    if (string.IsNullOrEmpty(connectionString)) return connectionString;
    
    // Handle different formats
    if (connectionString.StartsWith("postgresql://") || connectionString.StartsWith("postgres://"))
    {
        return Regex.Replace(connectionString, @"://([^:]+):([^@]+)@", @"://$1:***@");
    }
    
    if (connectionString.Contains("password=", StringComparison.OrdinalIgnoreCase))
    {
        return Regex.Replace(connectionString, @"password=[^;\s]+", "password=***", RegexOptions.IgnoreCase);
    }
    
    if (connectionString.Contains("Password="))
    {
        return Regex.Replace(connectionString, @"Password=[^;]+", "Password=***");
    }
    
    return connectionString;
}
```

### 5. Complete Coverage Checklist

Ensure your implementation covers all these scenarios:

- [ ] **URL formats**: `postgresql://`, `postgres://`, `jdbc:postgresql://`, Python variants
- [ ] **Key-value formats**: libpq conninfo, case-sensitive and case-insensitive
- [ ] **Semicolon formats**: Npgsql, .NET Entity Framework, Azure prefixes
- [ ] **JSON formats**: Node.js configuration objects
- [ ] **Service files**: DSN indirection via `pg_service.conf`
- [ ] **Multi-host configurations**: HA and load balancing setups
- [ ] **Azure-specific patterns**: All three server types, managed identity
- [ ] **Security parameters**: SSL, GSS, channel binding beyond basic SSL
- [ ] **Pooling parameters**: Min/max sizes, timeouts, keep-alives
- [ ] **Environment variable merging**: Proper precedence handling
- [ ] **Driver family detection**: Framework-specific tailoring
- [ ] **Source traceability**: Full provenance tracking

This comprehensive documentation ensures that Azure SRE agents can detect and properly categorize all PostgreSQL connection patterns found in modern cloud application environments, enabling complete topology mapping and security analysis.
