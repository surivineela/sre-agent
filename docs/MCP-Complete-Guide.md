# Model Context Protocol (MCP) in the SRE Agent - Complete Integration Guide

## Table of Contents

1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Quick Start](#quick-start)
4. [API Endpoints](#api-endpoints)
5. [Authentication](#authentication)
6. [Connection Types](#connection-types)
7. [Heartbeat & Health Monitoring](#heartbeat--health-monitoring)
8. [Tool Management](#tool-management)
9. [Dynamic Tool Addition](#dynamic-tool-addition)
10. [Configuration](#configuration)
11. [Security](#security)
12. [Error Handling](#error-handling)
13. [Workflow & Implementation](#workflow--implementation)
14. [Testing](#testing)
15. [Troubleshooting](#troubleshooting)
16. [Best Practices](#best-practices)

---

## Overview

The Model Context Protocol (MCP) integration enables dynamic connection to external MCP servers, allowing the SRE Agent to use tools from remote services as first-class capabilities. Tools are automatically added to the `meta_agent` for immediate use.

### Key Features

✅ **Configuration-Based Management** - Connections managed via connectors api
✅ **Enable/Disable Control** - Global MCP feature toggle via `MCPSettings.Enabled`  
✅ **Dynamic Tool Addition** - Tools automatically added to meta_agent.FactoryTools  
✅ **Multiple Authentication Types** - ApiKey, BearerToken (current support), Basic, CustomHeaders  
✅ **Connection Types** - HTTP (StreamableHttp)
✅ **Automatic Heartbeat Monitoring** - Health checks and connection status tracking  
✅ **Tool Limiting** - Configurable max tools per connection (default: 20)  
✅ **Health Monitoring** - Test connection endpoint for diagnostics  
✅ **Error Handling** - Comprehensive error responses with stack traces  

### Simplified Architecture

The current architecture **directly adds MCP tools to meta_agent** without generating separate agents:

```
MCP Connection Created
    ↓
Tools Loaded from MCP Server (max 20)
    ↓
Tools Registered in ToolFactory
    ↓
Tools Added to meta_agent.FactoryTools ⭐
    ↓
meta_agent can immediately use the tools
```

## Architecture

### Component Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                      MCP Integration Layer                      │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌────────────────────────┐     ┌──────────────────────────┐    │
│  │ Data Connectors or     │     │                          │    │
│  │ McpConnectionController│────▶│ McpConnectionEventManager│    │
│  │  (REST API)            │     │  (Lifecycle Management)  │   │
│  └────────────────────────┘     └────────┬─────────────────┘   │
│                                           │                      │
│                                           ▼                      │
│                              ┌─────────────────────────┐        │
│                              │ McpAgentManagement     │        │
│                              │ Service (Orchestration)│        │
│                              └────────┬────────────────┘        │
│                                       │                          │
│                    ┌──────────────────┴──────────────┐          │
│                    ▼                                 ▼           │
│         ┌──────────────────┐              ┌─────────────────┐  │
│         │ ToolFactory      │              │ McpToolsRepo    │  │
│         │ (Tool Registry)  │◀─────────────│ (MCP Tools)     │  │
│         └────────┬─────────┘              └─────────────────┘  │
│                  │                                               │
│                  ▼                                               │
│         ┌──────────────────┐                                    │
│         │ meta_agent       │                                    │
│         │ .FactoryTools ⭐ │                                    │
│         └──────────────────┘                                    │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### Key Components

#### 1. McpConnectionController
- REST API endpoints for connection management
- Input validation and error handling
- Response formatting with detailed error information

#### 2. McpConnectionEventManager
- Connection lifecycle management
- Event-driven architecture (ConnectionAdded, ConnectionRemoved)
- Connection state tracking
- Metadata storage for refresh operations
- Heartbeat verification for dynamic connections

#### 3. McpAgentManagementService
- Orchestrates tool registration
- Updates meta_agent.FactoryTools dynamically
- Handles connection/disconnection events
- Manages heartbeat timer for dynamic connections

#### 4. MCPMetaAgentManagementService
- Manages static connections from configuration
- Automatic reconnection for failed static connections
- Heartbeat monitoring for config-based servers

#### 5. McpToolsRepository
- Stores MCP tools from all connections
- Limits to first 20 tools per connection
- Provides tools to ToolFactory

#### 6. ToolFactory
- Central tool registry
- Resolves tools for agent execution
- Refreshes MCP tools on connection changes

#### 7. McpAuthenticationService
- Applies authentication to HTTP requests
- Supports multiple authentication types
- Resolves credentials from configuration

---

## Quick Start

### Prerequisites
- MCP server configured in `appsettings.json`
- Authentication credentials configured (if required)
- Application restart access

### 1. Configure MCP Connection

Edit `appsettings.json`:

```json
{
  "MCP": {
    "Enabled": true,
    "MaxToolsPerConnection": 20,
    "PingIntervalInSeconds": 60,
    "PingTimeoutInSeconds": 10,
    "StdioConnections": [
      {
        "Name": "github_mcp",
        "Command": "node",
        "Arguments": ["path/to/github-mcp-server.js"],
        "WorkingDirectory": "C:\\mcp-servers",
        "Enabled": true
      }
    ]
  }
}
```

### 2. Restart Application

Restart the application to load the new MCP connection.

### 3. Verify Connection

```http
GET /api/v1/mcp/connections/list
```

**Response:**
```json
[
  {
    "connectionId": "github_mcp",
    "name": "github_mcp",
    "type": "stdio",
    "status": "Connected",
    "toolCount": 15,
    "authenticationType": "None",
    "lastHeartbeat": "2025-10-21T10:30:00Z",
    "maxToolsPerConnection": 20,
    "tools": [...]
  }
]
```

### 4. Test Connection Health

```http
POST /api/v1/mcp/connections/test/github_mcp
```

**Response:**
```json
{
  "connectionId": "github_mcp",
  "success": true,
  "message": "Ping successful",
  "responseTimeMs": 45
}
```

### 5. Select Tools in Agent Builder

Use the Agent Builder UI to select which MCP tools to enable for your agents. The UI enforces the `MaxToolsPerConnection` limit and displays tools grouped by connection.

### 6. Use the Tools

The meta_agent now has access to selected tools from the MCP server. Ask the agent to use them:

```
"List my GitHub repositories using the available MCP tools"
```

---

## API Endpoints

### Available Endpoints

The MCP Connection API provides read-only access to connection information and health testing:

| Method | Endpoint | Description | Auth Required |
|--------|----------|-------------|---------------|
| `GET` | `/api/v1/mcp/connections/list` | List all MCP connections | ✅ Yes |
| `GET` | `/api/v1/mcp/connections/{id}` | Get connection details | ✅ Yes |
| `POST` | `/api/v1/mcp/connections/test/{id}` | Test connection health | ✅ Yes |

**Important Notes:**
- All endpoints require ARM operation authorization
- All endpoints return `503 Service Unavailable` if `MCPSettings.Enabled = false`
- Connections are managed via `appsettings.json` configuration
- To add/modify/remove connections: Update configuration and restart application
- Failed connections persist in the system with "Failed" status and tools remain registered

**Removed Endpoints (October 2025):**
- ~~`POST /api/v1/mcp/connections/add`~~ - Use configuration file instead
- ~~`DELETE /api/v1/mcp/connections/remove/{id}`~~ - Use configuration file instead
- ~~`POST /api/v1/mcp/connections/refresh/{id}`~~ - Restart application to reload connections

---

### 1. List Connections

**Endpoint:** `GET /api/v1/mcp/connections/list`

**Response:**
```json
[
  {
    "connectionId": "my_mcp_server",
    "name": "my_mcp_server",
    "type": "stdio",
    "status": "Connected",
    "toolCount": 15,
    "authenticationType": "None",
    "description": "Production monitoring MCP server for metrics and alerts",
    "serviceType": "Custom",
    "lastHeartbeat": "2025-10-21T10:30:00Z",
    "maxToolsPerConnection": 20
  }
]
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `connectionId` | string | Unique connection identifier |
| `name` | string | Connection display name |
| `type` | string | Connection type: `"http"`, `"sse"`, or `"stdio"` |
| `status` | string | Connection status: `"Connected"`, `"Failed"`, `"Disconnected"` |
| `endpoint` | string? | Endpoint URL (for HTTP/SSE connections only) |
| `toolCount` | number | Number of tools loaded from this connection |
| `authenticationType` | string | Auth type: `"ApiKey"`, `"Bearer"`, `"Basic"`, `"CustomHeaders"`, or `"None"` |
| `description` | string? | Optional connection description |
| `serviceType` | string? | Optional free-form service categorization (e.g., `"GitHub"`, `"Datadog"`, `"Custom"`) |
| `errorMessage` | string? | Error message if connection is in Failed status |
| `lastHeartbeat` | string | ISO 8601 timestamp of last successful health check |
| `maxToolsPerConnection` | number | Maximum tools allowed per connection (from settings) |
| `tools` | array? | Array of tool information (present in GET /{id} endpoint only) |

**Response (MCP Disabled - 503):**
```json
{
  "error": "MCP operations are currently disabled. Please enable MCP in settings to use this feature.",
  "exceptionType": "ServiceUnavailable"
}
```

---

### 2. Get Connection Details

**Endpoint:** `GET /api/v1/mcp/connections/{id}`

**Response:**
```json
{
  "connectionId": "my_mcp_server",
  "name": "my_mcp_server",
  "type": "stdio",
  "status": "Connected",
  "toolCount": 15,
  "authenticationType": "None",
  "description": "Production monitoring MCP server for metrics and alerts",
  "serviceType": "Custom",
  "lastHeartbeat": "2025-10-21T10:30:00Z",
  "maxToolsPerConnection": 20,
  "tools": [
    {
      "name": "query_data",
      "description": "Query data from the server",
      "parameters": {...}
    }
  ]
}
```

**Response (Error - 404 Not Found):**
```json
{
  "error": "Connection 'my_mcp_server' not found",
  "exceptionType": "NotFound"
}
```

**Response (MCP Disabled - 503):**
```json
{
  "error": "MCP operations are currently disabled. Please enable MCP in settings to use this feature.",
  "exceptionType": "ServiceUnavailable"
}
```

**Response (MCP Disabled - 503):**
```json
{
  "error": "MCP is disabled in configuration",
  "exceptionType": "ServiceUnavailable"
}
```

---

### 3. Test Connection

**Endpoint:** `POST /api/v1/mcp/connections/test/{id}`

**Response (Success):**
```json
{
  "connectionId": "my_mcp_server",
  "success": true,
  "message": "Ping successful",
  "responseTimeMs": 45,
  "error": null
}
```

**Response (Failure):**
```json
{
  "connectionId": "my_mcp_server",
  "success": false,
  "message": "Ping failed",
  "responseTimeMs": 5000,
  "error": "Request timeout"
}
```

**Response (MCP Disabled - 503):**
```json
{
  "error": "MCP is disabled in configuration",
  "exceptionType": "ServiceUnavailable"
}
```

**Use Cases:**
- Verify connection health
- Measure response time
- Diagnose connectivity issues
- Check authentication is working

**Response (MCP Disabled - 503):**
```json
{
  "error": "MCP operations are currently disabled. Please enable MCP in settings to use this feature.",
  "exceptionType": "ServiceUnavailable"
}
```

---

## Authentication

MCP connections support multiple authentication types. Authentication is configured in `appsettings.json` as part of the connection configuration.

### Supported Types

#### 1. API Key Authentication

Configure in `appsettings.json`:
```json
{
  "authentication": {
    "type": "ApiKey",
    "apiKey": "secret-key-123",
    "apiKeyHeader": "X-API-Key",    // Optional, defaults to "X-API-Key"
  "apiKeyPrefix": null            // Optional, adds prefix (e.g., "Bearer")
}
```

**Result:** `X-API-Key: secret-key-123`

**With Prefix:**
```json
{
  "type": "ApiKey",
  "apiKey": "secret-key-123",
  "apiKeyHeader": "Authorization",
  "apiKeyPrefix": "Bearer"
}
```

**Result:** `Authorization: Bearer secret-key-123`

#### 2. Bearer Token

```json
{
  "type": "Bearer",
  "bearerToken": "eyJhbGciOiJIUzI1NiIs..."
}
```

**Result:** `Authorization: Bearer eyJhbGciOiJIUzI1NiIs...`

#### 3. Basic Authentication

```json
{
  "type": "Basic",
  "username": "admin",
  "password": "secret"
}
```

**Result:** `Authorization: Basic YWRtaW46c2VjcmV0` (base64 encoded)

#### 4. Custom Headers

```json
{
  "type": "CustomHeaders",
  "customHeaders": {
    "X-Tenant-ID": "tenant-123",
    "X-Custom-Auth": "custom-value",
    "X-Request-ID": "unique-id"
  }
}
```

**Result:** All headers added to HTTP requests

#### 5. Azure ARM Authentication (for Logic Apps)

```json
{
  "type": "AzureARM",
  "armScope": "https://management.azure.com/.default"
}
```

**Result:** `Authorization: Bearer <Azure AD token>`

**How it works:**
- Server-side uses `AuthService.GetArmOperationCredential()` to obtain Azure AD credentials
- Token is requested with the specified ARM scope
- Token is automatically refreshed on expiration
- Ideal for Azure Logic Apps MCP servers

**Server-side implementation:**
```csharp
var cred = await _authService.GetArmOperationCredential();
var token = await cred.GetTokenAsync(
    new TokenRequestContext(new[] { "https://management.azure.com/.default" }), 
    CancellationToken.None
);
request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
```

#### 6. No Authentication

```json
{
  "type": "None"
}
```

Or simply omit the `authentication` property.

---

### How Authentication Works

```
┌─────────────────────────────────────────────────────────────────┐
│ 1. Create MCP Connection with Authentication Config             │
└──────────────────────┬──────────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────────┐
│ 2. AuthenticatedHttpClientTransportFactory                       │
│    - Creates HttpClientTransport with StreamableHttp mode        │
│    - Uses AdditionalHeaders property for authentication          │
│    - Dictionary<string, string> with auth headers                │
└──────────────────────┬──────────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────────┐
│ 3. SDK HttpClientTransportOptions                                │
│    - Name, Endpoint, TransportMode (StreamableHttp)              │
│    - AdditionalHeaders: Dictionary with auth headers             │
│    - OAuth: Optional ClientOAuthOptions for OAuth 2.0            │
└──────────────────────┬──────────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────────┐
│ 4. All HTTP Requests Include Authentication Headers             │
│    - SDK automatically adds AdditionalHeaders to requests        │
│    - No custom HttpClient or reflection needed                   │
│    - Clean, type-safe implementation                             │
└─────────────────────────────────────────────────────────────────┘
```

### AdditionalHeaders Implementation

The authentication system uses the SDK's **AdditionalHeaders** property:

#### Clean SDK Pattern (SDK 0.4.0+)
```csharp
// Create authentication headers dictionary
var headers = new Dictionary<string, string>();

// Add authentication based on type
switch (authConfig.Type)
{
    case "Bearer":
        headers["Authorization"] = $"Bearer {authConfig.BearerToken}";
        break;
    case "ApiKey":
        var headerName = authConfig.ApiKeyHeader ?? "X-API-Key";
        headers[headerName] = authConfig.ApiKey;
        break;
    case "Basic":
        var credentials = Convert.ToBase64String(
            Encoding.ASCII.GetBytes($"{authConfig.Username}:{authConfig.Password}"));
        headers["Authorization"] = $"Basic {credentials}";
        break;
    case "CustomHeaders":
        foreach (var (key, value) in authConfig.CustomHeaders)
            headers[key] = value;
        break;
    case "AzureARM":
        // Server-side Azure AD token acquisition
        var cred = await _authService.GetArmOperationCredential();
        var token = await cred.GetTokenAsync(
            new TokenRequestContext(new[] { authConfig.ArmScope }), 
            CancellationToken.None);
        headers["Authorization"] = $"Bearer {token.Token}";
        break;
}

// Create transport with authentication
var transport = new HttpClientTransport(new HttpClientTransportOptions
{
    Name = "MyServer",
    Endpoint = new Uri("https://server.com/mcp"),
    TransportMode = HttpTransportMode.StreamableHttp,
    AdditionalHeaders = headers // ✅ Official SDK API
});
```

#### Benefits Over Reflection Approach
- ✅ **Official SDK API** - No reflection or workarounds needed
- ✅ **Type-safe** - Dictionary<string, string> is strongly typed
- ✅ **Simpler** - Direct property assignment, no field injection
- ✅ **Maintainable** - Won't break with SDK updates
- ✅ **Cleaner** - No nested field discovery or fallback strategies
- ✅ **OAuth support** - Separate `OAuth` property for OAuth 2.0 flows

### Success Logging

When authentication is successfully configured, you'll see:

```log
INFO: Creating HTTP transport (StreamableHttp mode) for MyServer at https://server.com/mcp
INFO: ✅ Created HttpClientTransport with StreamableHttp mode
INFO: 🔐 Authentication configured: Bearer token authentication
```

---

## Connection Types

### 1. HTTP (StreamableHttp) - **Recommended for All Remote Servers** ⭐

```json
{
  "name": "remote_mcp",
  "type": "http",
  "endpoint": "https://api.example.com/mcp",
  "authentication": {
    "type": "Bearer",
    "bearerToken": "token-123"
  }
}
```

**Implementation:**
- Uses **`HttpClientTransport`** from `ModelContextProtocol.Client` namespace
- Transport mode: **`HttpTransportMode.StreamableHttp`**
- SDK Version: `ModelContextProtocol` 0.4.0-preview.2 or later

**Use Cases:**
- Remote MCP servers (preferred)
- Cloud-hosted services
- HTTP/HTTPS endpoints
- Servers requiring authentication
- Corporate proxy environments

**Features:**
- ✅ **Bidirectional HTTP streaming** - Full duplex communication
- ✅ **Better firewall/proxy compatibility** - Standard HTTP requests
- ✅ **SDK-powered** - Uses official ModelContextProtocol library
- ✅ **Supports ping protocol** - Health check capability
- ✅ **Heartbeat monitoring** - Automatic connection health checks
- ✅ **Standard HTTP authentication** - Bearer, ApiKey, Basic, Custom headers
- ✅ **More reliable than SSE** - Better reconnection handling

**Why HTTP (StreamableHttp) over SSE?**
| Feature | StreamableHttp | SSE |
|---------|---------------|-----|
| **Bidirectional** | ✅ True bidirectional | ❌ Server→Client only |
| **Firewall/Proxy** | ✅ Better compatibility | ⚠️ May be blocked |
| **Reliability** | ✅ Robust reconnection | ⚠️ Fragile |
| **Authentication** | ✅ Standard HTTP auth | ⚠️ Custom headers |
| **JSON-RPC** | ✅ Clean implementation | ⚠️ Complex streaming |

**Technical Details:**
```csharp
// Created using AuthenticatedHttpClientTransportFactory
var transport = new HttpClientTransport(new HttpClientTransportOptions
{
    Name = "remote_mcp",
    Endpoint = new Uri("https://api.example.com/mcp"),
    TransportMode = HttpTransportMode.StreamableHttp, // ⭐ StreamableHttp mode
    AdditionalHeaders = new Dictionary<string, string> // ✅ SDK authentication
    {
        ["Authorization"] = "Bearer token-123"
    }
});
```

---

### 2. SSE (Server-Sent Events) - Legacy Support

```json
{
  "name": "legacy_mcp",
  "type": "sse",
  "endpoint": "https://api.example.com/mcp/sse",
  "authentication": {
    "type": "Bearer",
    "bearerToken": "token-123"
  }
}
```

**Note:** SSE connections now automatically use `HttpClientTransport` with `HttpTransportMode.StreamableHttp` internally for better reliability. This type is maintained for backward compatibility.

**Use Cases:**
- Legacy MCP servers that only support SSE
- Maintaining compatibility with existing configurations

**Migration:** New connections should use `"type": "http"` instead.

---

### 3. STDIO (Standard Input/Output) - Best for Local Processes

```json
{
  "name": "local_mcp",
  "type": "stdio",
  "command": "node",
  "arguments": ["path/to/mcp-server.js"],
  "workingDirectory": "C:\\mcp-servers"
}
```

**Use Cases:**
- Local MCP server processes
- Development/testing
- Isolated tool sandboxes
- Custom local integrations

**Features:**
- ⚠️ Does not support ping protocol
- ✅ Heartbeat timestamp updated during verification
- ✅ Health monitored through successful tool executions

**Note:** STDIO connections use tool name prefixing to avoid conflicts:
- Tool name: `query_data`
- Registered as: `local_mcp_query_data`

---

## Heartbeat & Health Monitoring

### Overview

The MCP heartbeat mechanism provides automatic health monitoring for all MCP server connections. It periodically verifies connections are alive and marks unhealthy connections as failed while keeping them in the system for diagnostics and recovery.

### Configuration

Configure heartbeat behavior in `appsettings.json`:

```json
{
  "MCP": {
    "PingIntervalInSeconds": 60,
    "PingTimeoutInSeconds": 10,
    "IsolatedServers": [...],
    "SharedServers": [...],
    "StdioConnections": [...]
  }
}
```

**Settings:**
- **PingIntervalInSeconds** (default: 60): How often to verify connections
- **PingTimeoutInSeconds** (default: 10): How long to wait for a ping response before considering it failed

### How It Works

#### Initialization
1. On application startup, both services initialize their timers
2. Initial connection attempts are made for all configured static connections
3. Dynamic connections can be added via API at any time

#### Heartbeat Verification Cycle

Every `PingIntervalInSeconds` seconds:

**For HTTP/SSE Connections:**
1. **Ping Request**: Send a ping to the MCP server using `client.PingAsync()`
2. **Timeout Check**: Wait up to `PingTimeoutInSeconds` for response
3. **Success**: Update `LastHeartbeat` timestamp, reset failure counter, mark connection as healthy (`Status = Connected`)
4. **Failure**: Mark connection as failed with error message (`Status = Failed`, `ErrorMessage` set), **connection remains active**

**For STDIO Connections:**
- STDIO transport does not support the ping protocol
- Heartbeat timestamp is updated during verification to track when verification was attempted
- Connection health is monitored implicitly through successful tool executions
- Failed STDIO connections also remain active and can be refreshed

#### Connection Health Management (Updated Behavior - October 2025)

**🔥 IMPORTANT CHANGE**: Connections are **no longer automatically removed** when they fail heartbeat checks.

**Previous Behavior:**
- ❌ Connections automatically removed after consecutive ping failures
- ❌ Tools disappeared from meta_agent when connections failed
- ❌ Required complete reconfiguration to restore connections

**New Behavior:**
- ✅ **Connections persist through failures** - Failed connections remain in the system
- ✅ **Tools stay registered** - Tools remain visible in meta_agent and API listings
- ✅ **Graceful degradation** - Tools throw descriptive exceptions when invoked on unhealthy connections
- ✅ **Better diagnostics** - Connection status shows detailed error messages

When a connection fails verification:
1. **Keep connection active** in connections dictionary 
2. **Mark connection as failed** (`Status = Failed`, `ErrorMessage` set)
3. **Tools remain registered** in ToolFactory and meta_agent
4. **Tool invocation throws exception** with connection health details

**Configuration Changes:**
- ❌ **Removed**: `MaxPingFailuresBeforeRemoval` setting - No longer needed
- ✅ **Kept**: `PingIntervalInSeconds`, `PingTimeoutInSeconds` - Still used for health monitoring
- ✅ **Kept**: `MaxToolsPerConnection` - Limits tools loaded per connection

**Key Benefits:**
- Connections persist through temporary network issues
- Tools remain visible to agents but fail gracefully when invoked
- Better visibility into connection issues through status and error messages
- Restart application to reload updated configurations

#### Tool Execution Health Checks

**NEW**: Before executing any MCP tool, the system validates connection health and attempts automatic reconnection:

```csharp
public async Task ValidateConnectionHealthAsync(McpConnection connection, string toolName)
{
    // Check if connection is in failed state (initialization failure - cannot reconnect)
    if (connection.Status == DataConnectorStatus.Failed)
    {
        throw new InvalidOperationException(
            $"Cannot execute MCP tool '{toolName}': Connection '{connection.Id}' failed to initialize - {connection.ErrorMessage}");
    }

    // Check if connection is disconnected - attempt to reconnect
    if (connection.Status == DataConnectorStatus.Disconnected)
    {
        // Automatically attempt to refresh/reconnect the connection
        await _connectionManager.RefreshConnectionAsync(connection.Id);
    }

    // Check if client is null
    if (connection.Client == null)
    {
        throw new InvalidOperationException(
            $"Cannot execute MCP tool '{toolName}': Connection '{connection.Id}' has no active client");
    }
}
```

**Connection Status Behavior:**
- **Failed**: Initialization failed - connection cannot be automatically reconnected
- **Disconnected**: Verification (ping) failed - connection will attempt automatic reconnection before tool execution
- **Connected**: Connection is healthy and ready for tool execution

**Error Messages Examples:**
- `"Cannot execute MCP tool 'github_mcp_search_code': Connection 'github_mcp' failed to initialize - HTTP 401 Unauthorized"`
- `"Cannot execute MCP tool 'dynatrace_mcp_execute_dql': Connection 'dynatrace_mcp' is disconnected and reconnection failed - Network error"`
- `"Cannot execute MCP tool 'custom_mcp_process_data': Connection 'custom_mcp' has no active client"`

### Heartbeat Flow Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                    APPLICATION STARTUP                           │
└──────────────────────────────┬──────────────────────────────────┘
                               ↓
        ┌──────────────────────────────────────────┐
        │   Two Heartbeat Systems Start            │
        └──────────────────────────────────────────┘
                    ↓                     ↓
    ┌───────────────────┐    ┌───────────────────┐
    │ Static Connections│    │Dynamic Connections│
    │  (from config)    │    │   (from API)      │
    └───────────────────┘    └───────────────────┘
              ↓                        ↓
    MCPMetaAgentManagement    McpAgentManagement
         Service                    Service
              ↓                        ↓
    StartConnectionVerification  StartHeartbeatTimer()
         Timer()                       ↓
              ↓                        ↓
    Timer fires every         Timer fires every
    PingIntervalInSeconds    PingIntervalInSeconds
              ↓                        ↓
              └────────────┬───────────┘
                           ↓
              ┌────────────────────────┐
              │ HEARTBEAT VERIFICATION │
              │      (parallel)        │
              └────────────────────────┘
                           ↓
              For Each Active Connection
                           ↓
            ┌──────────────────────┐
            │ Is STDIO Connection? │
            └──────────────────────┘
                  Yes ↓    ↓ No
                      ↓    ↓
        ┌─────────────┐  ┌──────────────┐
        │Update       │  │Send PingAsync│
        │Heartbeat    │  │Wait timeout  │
        └─────────────┘  └──────────────┘
                               ↓
                    ┌──────────────────┐
                    │ Ping Successful? │
                    └──────────────────┘
                        Yes ↓  ↓ No
                            ↓  ↓
            ┌───────────────┐  ┌────────────────────┐
            │Update         │  │Mark as Failed      │
            │LastHeartbeat  │  │Set ErrorMessage    │
            │Status=Connected│ │Status=Failed       │
            │Log Success    │  │Fire Event          │
            └───────────────┘  │Log Warning         │
                               │⚠️ Tools Stay Active│
                               └────────────────────┘
```

**Key Changes in Flow:**
- ❌ No longer removes connections on failure
- ✅ Marks connection as Failed with error details
- ✅ Tools remain registered and visible
- ✅ Manual refresh or removal required

### Monitoring Connection Health

#### Via API

```bash
curl https://your-app/api/v1/mcp/connections/list
```

Response includes `lastHeartbeat` for each connection:

```json
[
  {
    "name": "github_copilot_mcp",
    "type": "sse",
    "endpoint": "https://api.github.com/mcp",
    "lastHeartbeat": "2025-10-12T10:30:00Z",
    "status": "Connected",
    "tools": [...]
  }
]
```

#### Via Logs

Enable trace logging to see heartbeat activity:

```json
{
  "Logging": {
    "LogLevel": {
      "Agent.Runtime.Services.MCPMetaAgentManagementService": "Trace",
      "Agent.Runtime.Services.McpConnectionEventManager": "Trace"
    }
  }
}
```

Sample logs:
```
[Trace] Verifying 3 active MCP connections
[Trace] Successfully pinged 'github_copilot_mcp'
[Warning] Ping failed for 'offline_server' - marking as Failed (connection remains active)
[Information] Starting connection verification timer with 60s interval and 10s timeout
```

### What Happens When a Connection Fails?

**NEW Behavior (October 2025):**

1. **Detection**: Connection fails ping or times out
2. **Mark as Failed**: Connection status changed to `Failed`, error message recorded
3. **Tools Remain**: All tools stay registered in ToolFactory and meta_agent
4. **Notification**: Event fired to update dependent systems
5. **Logging**: Warning logged with connection details
6. **Tool Errors**: Attempting to use tools throws descriptive exceptions:
   - `"Cannot execute MCP tool 'github_mcp_search_code': Connection 'github_mcp' is unhealthy - Ping timeout after 10 seconds"`
   - `"Cannot execute MCP tool 'dynatrace_mcp_execute_dql': Connection 'dynatrace_mcp' is disconnected"`

**Recovery Options:**

- **Restart Application**: Reload connections from updated `appsettings.json` configuration
- **Fix Configuration**: Update connection settings in `appsettings.json` and restart

**Note:** Failed connections remain visible with "Failed" status until the application is restarted with corrected configuration.

---

## Tool Management

### Tool Limiting (First 20)

Each MCP connection is limited to **20 tools** to prevent overload:

```csharp
public void TryAddServer(McpConnection connection)
{
    if (connection.Tools != null)
    {
        // Limit to first 20 tools
        var toolsToAdd = connection.Tools.Take(20).ToList();
        
        foreach (AIFunction tool in toolsToAdd)
        {
            // Register tool
        }

        _logger.LogInformation(
            "Added {Count} tools from MCP connection '{ConnectionId}' (limited to first 20)",
            toolsToAdd.Count,
            connection.Id);
    }
}
```

**Why limit to 20?**
- Prevents context overflow in agent prompts
- Keeps tool selection focused
- Improves agent decision-making performance
- Reduces latency

### Tool Naming

#### SSE Connections (Remote)
- Tools keep their original names
- Example: `query_data`

#### STDIO Connections (Local)
- Tools are prefixed with connection ID
- Example: `local_mcp_query_data`
- Prevents naming conflicts between local servers

### Tool Resolution

When an agent calls a tool:

```
1. Agent calls tool by name: "query_data"
    ↓
2. ToolFactory looks up tool
    ↓
3. Found in registered MCP tools
    ↓
4. [NEW] Health check: Validate connection status
    ↓
5. If healthy: Tool executed via MCP protocol
   If unhealthy: Exception thrown with error details
    ↓
6. Result returned to agent (or exception propagated)
```

---

## Dynamic Tool Addition

### How It Works

When a connection is added or removed, the system **dynamically updates the meta_agent**:

#### OnConnectionAdded Flow

```
MCP Connection Added
    ↓
ToolFactory.RefreshMcpToolsAsync()  ← Registers tools in ToolFactory
    ↓
AddMcpToolsToMetaAgent()  ← Adds tool names to meta_agent.FactoryTools
    ↓
meta_agent can now resolve and use the tools
```

**Code:**
```csharp
private async Task OnConnectionAdded(McpConnection connection)
{
    // Refresh MCP tools in ToolFactory
    await _toolFactory.RefreshMcpToolsAsync();
    
    // 🎯 Dynamically add MCP tools to meta_agent
    AddMcpToolsToMetaAgent();
}
```

#### OnConnectionRemoved Flow (Manual Removal Only)

**Note**: Connections are no longer automatically removed due to heartbeat failures. This flow only applies to explicit connection removal via API.

```
MCP Connection Manually Removed
    ↓
ToolFactory.RefreshMcpToolsAsync()  ← Removes tools from ToolFactory
    ↓
AddMcpToolsToMetaAgent()  ← Updates meta_agent.FactoryTools
    ↓
meta_agent no longer has access to removed tools
```

#### AddMcpToolsToMetaAgent() Implementation

```csharp
private void AddMcpToolsToMetaAgent()
{
    try
    {
        // Get the meta_agent instance
        var metaAgent = _agentFactory.GetAgent("meta_agent");
        
        // Get all current MCP tool names
        var mcpToolNames = _mcpToolsRepository.GetAllFunctions()
            .Select(f => f.Name)
            .ToList();

        // Remove old MCP tools
        metaAgent.FactoryTools.RemoveAll(tool => 
            _mcpToolsRepository.GetAllFunctions().Any(f => f.Name == tool));

        // Add current MCP tools
        metaAgent.FactoryTools.AddRange(mcpToolNames);

        _logger.LogInternalInformation(
            "Added {Count} MCP tools to meta_agent.FactoryTools",
            mcpToolNames.Count);
    }
    catch (Exception ex)
    {
        _logger.LogInternalError(ex, 
            "Failed to add MCP tools to meta_agent");
    }
}
```

### Benefits

✅ **Automatic** - No manual YAML configuration  
✅ **Real-time** - Tools available immediately after connection  
✅ **Clean** - Proper cleanup when connection removed  
✅ **Transparent** - meta_agent sees MCP tools like any other tool  

---

## Configuration

### appsettings.json

**File:** `appsettings.json`

```json
{
  "MCP": {
    "Enabled": true,                 // ⭐ Master enable/disable switch
    "PingIntervalInSeconds": 60,     // Heartbeat interval
    "PingTimeoutInSeconds": 10,      // Ping timeout
    "MaxToolsPerConnection": 20,     // Maximum tools to load per connection
    "StdioConnections": [            // Local process connections
      {
        "Name": "local-mcp-server",
        "Command": "node",
        "Arguments": ["server.js"],
        "WorkingDirectory": "C:\\mcp-servers",
        "Enabled": true
      }
    ]
  }
}
```

**Configuration Settings:**

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `Enabled` | bool | `true` | **Master switch** - Disables all MCP operations when `false`. API returns 503. |
| `PingIntervalInSeconds` | int | `60` | How often to verify connection health |
| `PingTimeoutInSeconds` | int | `10` | How long to wait for ping response |
| `MaxToolsPerConnection` | int | `20` | Maximum number of tools to load from each connection |
| `StdioConnections` | array | `[]` | Array of local process-based MCP server configurations |

**Removed Configuration (as of Oct 2025):**

- ~~**MaxPingFailuresBeforeRemoval**~~: This setting has been removed. Connections are no longer automatically removed when ping failures occur. Instead, failed connections are marked as unhealthy but remain in the system.
- ~~**IsolatedServers**~~: Legacy static HTTP/SSE connections removed in favor of STDIO connections
- ~~**SharedServers**~~: Legacy static HTTP/SSE connections removed in favor of STDIO connections

### StdioConnection Configuration

Each STDIO connection supports the following properties:

```json
{
  "Name": "my-mcp-server",           // Required: Unique connection identifier
  "Command": "node",                  // Required: Command to execute
  "Arguments": [                      // Optional: Command arguments
    "path/to/server.js",
    "--port", "8080"
  ],
  "WorkingDirectory": "C:\\servers",  // Optional: Working directory for process
  "Enabled": true                     // Optional: Enable/disable this connection (default: true)
}
```

### Disabling MCP Feature

To completely disable MCP functionality:

```json
{
  "MCP": {
    "Enabled": false  // All MCP API endpoints return 503 Service Unavailable
  }
}
```

**Effects when `Enabled = false`:**
- ✅ `McpToolsRepository` skips initialization
- ✅ `McpAgentManagementService` skips startup
- ✅ All API endpoints return `503 Service Unavailable`
- ✅ No connections are created or monitored
- ✅ No tools are registered from MCP servers

### Connection Metadata Storage

Connections store metadata for refresh operations and type preservation:

```csharp
public class McpConnectionMetadata
{
    public required string Type { get; init; }           // "http", "sse", or "stdio"
    public string? Endpoint { get; init; }               // For HTTP/SSE
    public string? Command { get; init; }                // For STDIO
    public string[]? Arguments { get; init; }            // For STDIO
    public string? WorkingDirectory { get; init; }       // For STDIO
    public McpAuthenticationConfig? Authentication { get; init; }  // Auth config
    public string? Description { get; init; }            // User description
    public string? ServiceType { get; init; }            // Service classification
}
```

**Important Notes:**

1. **Type Preservation**: The `Type` field preserves the original connection type requested by the user:
   - `"http"` → HTTP (StreamableHttp) connection
   - `"sse"` → SSE connection (internally uses StreamableHttp but preserves "sse" label)
   - `"stdio"` → Local process connection

2. **Transport vs Type**: Both HTTP and SSE connections use `HttpClientTransport` internally, but:
   - The metadata `Type` field preserves the user's intent ("http" vs "sse")
   - API responses return the metadata type, not the transport type
   - This maintains backward compatibility with existing SSE configurations

3. **Refresh Operations**: Metadata enables connection refresh without requiring the client to resend all connection details. The stored metadata includes:
   - Original connection type
   - Authentication configuration
   - Endpoint/command details
   - Service classification

**Example:**
```csharp
// Creating an SSE connection
await CreateAndAddConnectionAsync("my-server", "sse", "https://api.example.com/sse", ...);

// Connection uses HttpClientTransport internally
// But metadata.Type = "sse"
// API responses show Type = "sse" (not "http")
```

This enables refresh without requiring the client to resend all connection details.

---

## Security

### Authentication Security

1. **Never Hardcode Credentials**
   - Use environment variables
   - Use Azure Key Vault (placeholder support in code)
   - Use secure configuration providers

2. **Credential Storage**
   - Credentials are stored in memory only
   - Not persisted to disk
   - Not included in API responses (only auth type shown)

3. **HTTPS Enforcement**
   - Use HTTPS endpoints for production
   - SSL/TLS for all remote connections
   - Certificate validation enabled

4. **Least Privilege**
   - Use minimal scopes for tokens
   - Rotate credentials regularly
   - Separate credentials per environment

### API Security

1. **Input Validation**
   - All inputs validated before processing
   - Type checking and sanitization
   - Length limits enforced

2. **Error Messages**
   - Detailed errors only in development
   - Generic errors in production
   - No credential leakage in logs

3. **Rate Limiting**
   - Implement rate limiting for API endpoints
   - Prevent connection flooding
   - Monitor suspicious activity

### Network Security

1. **Firewall Rules**
   - Whitelist MCP server IPs
   - Block unauthorized access
   - Monitor network traffic

2. **Connection Isolation**
   - Separate networks for different environments
   - No cross-environment connections
   - VPN or private networks for sensitive servers

---

## Error Handling

### Error Response Format

All error responses include detailed diagnostic information:

```json
{
  "error": "Connection failed: HTTP 401 Unauthorized",
  "exceptionType": "System.Net.Http.HttpRequestException",
  "stackTrace": "at Agent.Runtime.Services.McpConnectionEventManager...",
  "innerException": {
    "error": "Invalid API key",
    "exceptionType": "System.InvalidOperationException",
    "stackTrace": "..."
  }
}
```

### HTTP Status Codes

| Status Code | Meaning | Common Causes |
|-------------|---------|---------------|
| **200 OK** | Success | Request completed successfully |
| **204 No Content** | Success (no body) | Connection deleted |
| **400 Bad Request** | Invalid request | Missing parameters, wrong type |
| **404 Not Found** | Resource not found | Connection ID doesn't exist |
| **500 Internal Server Error** | Server error | Unexpected exception |
| **502 Bad Gateway** | MCP server error | Server unreachable, auth failed |

### Common Error Scenarios

#### 1. Connection Unreachable (HTTP 502)

**Response:**
```json
{
  "error": "Connection failed: Unable to connect to remote server",
  "exceptionType": "System.Net.Http.HttpRequestException"
}
```

**Causes:**
- MCP server offline
- Network connectivity issues
- Firewall blocking connection
- Wrong endpoint URL

**Solutions:**
- Verify MCP server is running
- Test endpoint with curl/browser
- Check firewall rules
- Verify endpoint URL format

#### 2. Authentication Failed (HTTP 502)

**Response:**
```json
{
  "error": "Connection failed: HTTP 401 Unauthorized",
  "exceptionType": "System.Net.Http.HttpRequestException"
}
```

**Causes:**
- Invalid API key/token
- Expired credentials
- Wrong authentication type
- Missing authentication headers

**Solutions:**
- Verify credentials are correct
- Check authentication type matches server
- Regenerate expired tokens
- Review authentication logs

#### 3. Invalid Request (HTTP 400)

**Response:**
```json
{
  "error": "Missing required parameter: endpoint",
  "exceptionType": "System.ArgumentException"
}
```

**Causes:**
- Missing required parameters
- Invalid connection type
- Malformed request body

**Solutions:**
- Review API documentation
- Validate request JSON
- Check all required fields present

#### 4. Connection Not Found (HTTP 404)

**Response:**
```json
{
  "error": "Connection 'my_server' not found",
  "exceptionType": "NotFound"
}
```

**Causes:**
- Connection ID doesn't exist
- Connection was removed
- Typo in connection ID

**Solutions:**
- List all connections to verify ID
- Check for typos
- Recreate connection if needed

---

## Workflow & Implementation

### Complete Connection Lifecycle

```
┌─────────────────────────────────────────────────────────────────┐
│                1. CONFIGURE CONNECTION                           │
│  Edit appsettings.json -> MCP.StdioConnections                  │
└──────────────────────────┬──────────────────────────────────────┘
                           ↓
┌─────────────────────────────────────────────────────────────────┐
│                2. RESTART APPLICATION                            │
│  Application startup reads MCP configuration                    │
└──────────────────────────┬──────────────────────────────────────┘
                           ↓
┌─────────────────────────────────────────────────────────────────┐
│  3. McpAgentManagementService Initialization                    │
│     - Check MCPSettings.Enabled                                 │
│     - Read StdioConnections from config                         │
│     - Validate connection parameters                            │
└──────────────────────────┬──────────────────────────────────────┘
                           ↓
┌─────────────────────────────────────────────────────────────────┐
│  4. Create MCP Connections                                      │
│     - Create McpConnection objects                              │
│     - Initialize STDIO transport                                │
│     - Start local MCP server processes                          │
└──────────────────────────┬──────────────────────────────────────┘
                           ↓
┌─────────────────────────────────────────────────────────────────┐
│  5. Load Tools from MCP Servers                                 │
│     - Connect to each MCP server                                │
│     - Fetch available tools (max per MaxToolsPerConnection)     │
│     - Parse tool metadata                                       │
└──────────────────────────┬──────────────────────────────────────┘
                           ↓
┌─────────────────────────────────────────────────────────────────┐
│  6. Register Tools in ToolFactory                               │
│     - McpToolsRepository.TryAddServer()                         │
│     - Register each tool (up to MaxToolsPerConnection)          │
│     - Log tool registration                                     │
└──────────────────────────┬──────────────────────────────────────┘
                           ↓
┌─────────────────────────────────────────────────────────────────┐
│  7. Start Heartbeat Monitoring                                  │
│     - Begin periodic health checks                              │
│     - Monitor connection status                                 │
└──────────────────────────┬──────────────────────────────────────┘
                           ↓
│  6. McpAgentManagementService.OnConnectionAdded()               │
│     - Refresh MCP tools in ToolFactory                          │
│     - Call AddMcpToolsToMetaAgent()                             │
└──────────────────────────┬──────────────────────────────────────┘
                           ↓
┌─────────────────────────────────────────────────────────────────┐
│  7. Update meta_agent.FactoryTools                              │
│     - Get current MCP tool names                                │
│     - Remove old MCP tools                                      │
│     - Add new MCP tool names                                    │
│     - meta_agent can now use the tools! ⭐                      │
└──────────────────────────┬──────────────────────────────────────┘
                           ↓
┌─────────────────────────────────────────────────────────────────┐
│  8. Start Heartbeat Monitoring                                  │
│     - Heartbeat timer begins (if not already running)           │
│     - Periodic ping checks every PingIntervalInSeconds          │
│     - Update LastHeartbeat on successful ping                   │
└─────────────────────────────────────────────────────────────────┘
```

### Implementation Components

#### Key Files

1. **McpConnectionController.cs**
   - `src/Agent/Agent.Web/Controllers/v1/McpConnectionController.cs`
   - REST API endpoints
   - Request/response handling

2. **McpConnectionEventManager.cs**
   - `src/Agent/Agent.Runtime/Services/McpConnectionEventManager.cs`
   - Connection lifecycle management
   - Event notifications
   - Heartbeat verification for dynamic connections

3. **McpAgentManagementService.cs**
   - `src/Agent/Agent.Runtime/Services/McpAgentManagementService.cs`
   - Orchestration and event handling
   - Dynamic tool addition to meta_agent
   - Heartbeat timer management

4. **MCPMetaAgentManagementService.cs**
   - `src/Agent/Agent.Runtime/Services/MCPMetaAgentManagementService.cs`
   - Static connection management
   - Configuration-based connections
   - Automatic reconnection

5. **AuthenticatedHttpClientTransportFactory.cs**
   - `src/Agent/Agent.Runtime/Services/AuthenticatedHttpClientTransportFactory.cs`
   - Creates HttpClientTransport with StreamableHttp mode
   - Authentication via AdditionalHeaders property
   - Multiple auth type support (Bearer, ApiKey, Basic, CustomHeaders)

6. **McpToolsRepository.cs**
   - `src/Agent/Agent.Runtime/Repositories/McpToolsRepository.cs`
   - Tool storage and retrieval
   - Tool limiting enforcement

---

## Testing

### Unit Tests Coverage

#### McpConnectionControllerTests.cs

**Connection Creation Tests:**
- ✅ CreateConnection_ValidSseRequest_ReturnsOk
- ✅ CreateConnection_ValidStdioRequest_ReturnsOk
- ✅ CreateConnection_InvalidRequest_ReturnsBadRequest
- ✅ CreateConnection_Exception_ReturnsInternalServerError
- ✅ CreateConnection_ResponseContainsToolInfo
- ✅ CreateConnection_WithMoreThan20Tools_LimitsToFirst20

**Authentication Tests:**
- ✅ CreateConnection_WithApiKeyAuth_ReturnsOkWithAuthType
- ✅ CreateConnection_WithBearerAuth_ReturnsOkWithAuthType
- ✅ CreateConnection_WithCustomHeaders_ReturnsOkWithAuthType

**List/Get Tests:**
- ✅ ListConnections_ReturnsAllConnections
- ✅ ListConnections_NoConnections_ReturnsEmptyList
- ✅ GetConnection_ExistingConnection_ReturnsOk
- ✅ GetConnection_NonExistentConnection_ReturnsNotFound

**Test Connection Tests:**
- ✅ TestConnection_ExistingConnection_ReturnsSuccessResult
- ✅ TestConnection_NonExistentConnection_ReturnsNotFound
- ✅ TestConnection_PingFails_ReturnsFailureResult
- ✅ TestConnection_ClientNotInitialized_ReturnsFailureResult

**Refresh Connection Tests:**
- ✅ RefreshConnection_ExistingConnection_ReturnsSuccessResult
- ✅ RefreshConnection_NonExistentConnection_ReturnsNotFound
- ✅ RefreshConnection_RefreshFails_ReturnsFailureResult
- ✅ RefreshConnection_ToolCountIncreased_ReturnsCorrectCounts
- ✅ RefreshConnection_ToolCountDecreased_ReturnsCorrectCounts

**Delete Tests:**
- ✅ DeleteConnection_ExistingConnection_ReturnsNoContent
- ✅ DeleteConnection_NonExistentConnection_ReturnsNotFound

**Total Controller Tests: 30+**

#### McpConnectionEventManagerTests.cs

**Status (as of October 2025):**

The unit tests for `McpConnectionEventManager` have been simplified to focus on validation logic only. Tests that require actual MCP server processes have been removed from the unit test suite and should be covered in integration tests instead.

**Current Unit Tests (5 validation tests):**
- ✅ CreateAndAddConnectionAsync_InvalidType_ThrowsArgumentException
- ✅ CreateAndAddConnectionAsync_SseMissingEndpoint_ThrowsArgumentException
- ✅ CreateAndAddConnectionAsync_StdioMissingCommand_ThrowsArgumentException
- ✅ GetConnectionAsync_NonExistentConnection_ReturnsNull
- ✅ RemoveConnectionAsync_NonExistentConnection_ReturnsFalse

**Tests Moved to Integration Test Scope:**

The following tests require actual MCP server processes (either HTTP/SSE endpoints or STDIO processes) and should be covered in integration tests:

- CreateAndAddConnectionAsync_StdioConnection_CreatesAndFiresEvent
- GetActiveConnections_ReturnsAllConnections
- GetConnectionAsync_ExistingConnection_ReturnsConnection
- RemoveConnectionAsync_ExistingConnection_RemovesAndFiresEvent
- BackendRegistersServer_WhenConnectionAdded
- ConnectionAdded_Event_ReceivesCorrectConnection
- ConnectionRemoved_Event_ReceivesCorrectId
- UpdateHeartbeat_UpdatesTimestamp
- CreateAndAddConnectionAsync_MultipleEvents_AllFire
- RefreshConnectionAsync_* (all variants)
- CreateAndAddConnectionAsync_Stdio_StoresCorrectMetadata
- CreateAndAddConnectionAsync_With*Auth_StoresMetadata (all auth variants)

**Rationale:**

Unit tests for `McpConnectionEventManager` are limited because:
1. The class directly calls `McpConnection.InitializeAsync()` which attempts real network/process connections
2. Mocking the connection initialization would require significant architectural changes
3. These tests are better suited for integration testing with real MCP servers

For comprehensive testing of connection lifecycle, events, and tool management, use the integration test approach with Jupyter notebooks or actual test MCP servers.

### Integration Testing

Use the provided Jupyter notebooks for integration testing:

1. **MCP_Complete_API_Demo.ipynb** - Complete API walkthrough
2. **MCP_Auth_Demo.ipynb** - Authentication testing
3. **GitHub_MCP_Demo.ipynb** - Real-world GitHub integration

---

## Troubleshooting

### Problem: Connection Returns 401 Unauthorized

**Cause:** Authentication not working

**Solutions:**

1. **Check logs for authentication configuration:**
   ```log
   INFO: ✅ Created HttpClientTransport with StreamableHttp mode
   INFO: 🔐 Authentication configured: Bearer token authentication
   ```

2. **Verify authentication configuration in appsettings.json:**
   ```json
   {
     "MCP": {
       "StdioConnections": [
         {
           "Name": "my_server",
           "Command": "node",
           "Arguments": ["server.js"],
           "Enabled": true
         }
       ]
     }
   }
   ```

3. **Test MCP server directly:**
   ```bash
   curl -H "X-API-Key: your-key" https://mcp-server.example.com/mcp
   ```

4. **Fix configuration and restart:**
   - Update `appsettings.json` with correct connection details
   - Restart the application to reload connections

---

### Problem: Tools Not Appearing in meta_agent

**Cause:** Tool loading or registration failed

**Check:**

1. **Review logs:**
   ```log
   Added 15 MCP tools to meta_agent.FactoryTools
   ```

2. **Verify connection status:**
   ```http
   GET /api/v1/mcp/connections/list
   ```
   Status should be `"Connected"`

3. **Check tool count:**
   Response should show `"toolCount": 15`

4. **Verify MCP is enabled:**
   Check `appsettings.json` - `MCPSettings.Enabled` should be `true`

5. **Check MaxToolsPerConnection:**
   Ensure the limit isn't preventing tools from loading

6. **Restart application:**
   Restart to reload connections from updated configuration

---

### Problem: Slow Connection Performance

**Diagnosis:** Use test endpoint to measure response time

```http
POST /api/v1/mcp/connections/test/my_server
```

**Response:**
```json
{
  "responseTimeMs": 1250
}
```

**Performance Indicators:**
- **< 100ms** - Excellent 🚀
- **100-500ms** - Good ✅
- **500-1000ms** - Acceptable ⚠️
- **> 1000ms** - Slow 🐌

**Solutions:**
- Check network latency to MCP server
- Verify MCP server performance
- Consider caching strategies
- Reduce number of tools (limit already at 20)

---

### Problem: Connection Keeps Disconnecting

**Cause:** Heartbeat monitoring removing connection

**Check:**

1. **Review heartbeat logs:**
   ```log
   [Warning] Ping timed out for 'my_server', removing connection
   ```

2. **Verify MCP server stability:**
   - Check server uptime
   - Monitor server logs
   - Test server independently

3. **Adjust heartbeat settings:**
   ```json
   {
     "MCP": {
       "PingIntervalInSeconds": 120,    // Increase interval
       "PingTimeoutInSeconds": 20       // Increase timeout
     }
   }
   ```

4. **Check network:**
   - Verify network stability
   - Check for intermittent connectivity issues
   - Review firewall logs

---

### Problem: Refresh Returns Old Tool Count

**Cause:** MCP server hasn't updated tools

**Check:**
1. Verify MCP server has new tools deployed
2. Check MCP server logs
3. Test direct MCP server API
4. Wait for server to fully start before refresh

---

### Problem: Authentication Not Working

**Cause:** SDK 0.4.0+ uses `AdditionalHeaders` instead of HttpClient injection

**Check Logs:**
```log
INFO: ✅ Created HttpClientTransport with StreamableHttp mode
INFO: 🔐 Authentication configured: Bearer token authentication
```

**Solution:** Ensure you're using SDK 0.4.0-preview.2 or later which properly supports `AdditionalHeaders` for authentication.

**Code Pattern:**
```csharp
var transport = new HttpClientTransport(new HttpClientTransportOptions
{
    Name = "MyServer",
    Endpoint = new Uri("https://server.com/mcp"),
    TransportMode = HttpTransportMode.StreamableHttp,
    AdditionalHeaders = new Dictionary<string, string>
    {
        ["Authorization"] = "Bearer your-token"
    }
});
```

---

## Best Practices

### 1. Connection Naming
- ✅ Use descriptive, lowercase names
- ✅ Use underscores for word separation
- ❌ Avoid spaces and special characters
- Example: `github_api` not `GitHub API!`

### 2. Authentication
- ✅ Store credentials securely (environment variables, Key Vault)
- ✅ Use SDK's `AdditionalHeaders` property for authentication
- ✅ Leverage `OAuth` property for OAuth 2.0 flows
- ❌ Don't hardcode credentials in code
- ✅ Never hardcode credentials in code
- ✅ Rotate credentials regularly
- ✅ Use least-privilege access
- ✅ Use HTTPS for all remote connections
- ✅ Separate credentials per environment

### 3. Tool Management
- ✅ Keep tool count under 20 per connection
- ✅ Use descriptive tool names
- ✅ Document tool purposes
- ✅ Remove unused connections
- ✅ Monitor tool usage

### 4. Monitoring
- ✅ Regularly test connections with `/test` endpoint
- ✅ Monitor response times
- ✅ Set up alerts for failed connections
- ✅ Review logs for authentication errors
- ✅ Track `lastHeartbeat` timestamps

### 5. Refresh Strategy
- ✅ Refresh after MCP server deployments
- ✅ Schedule periodic refreshes for critical connections
- ✅ Use refresh instead of remove/add for updates
- ✅ Monitor `oldToolCount` vs `newToolCount`
- ✅ Test after refresh to verify tools

### 6. Error Handling
- ✅ Always check API response status codes
- ✅ Log connection failures
- ✅ Implement retry logic for transient failures
- ✅ Have fallback strategies
- ✅ Parse error responses for diagnostics

### 7. Development vs Production
- ✅ Use separate MCP servers for dev/prod
- ✅ Test authentication in dev environment first
- ✅ Use different credentials per environment
- ✅ Monitor production connections closely
- ✅ Enable detailed logging in development
- ✅ Use generic errors in production

### 8. Performance
- ✅ Monitor connection response times
- ✅ Use local STDIO connections for development
- ✅ Use remote SSE connections for production
- ✅ Keep tool count minimal
- ✅ Cache tool metadata when possible

### 9. Security
- ✅ Use HTTPS endpoints in production
- ✅ Validate SSL certificates
- ✅ Implement rate limiting
- ✅ Monitor for suspicious activity
- ✅ Use firewall rules
- ✅ Audit credential access

### 10. Maintenance
- ✅ Regularly review active connections
- ✅ Remove unused connections
- ✅ Update credentials before expiration
- ✅ Test connections after server updates
- ✅ Document connection purposes
- ✅ Keep connection inventory

---

## Summary

The MCP integration provides a powerful, flexible way to extend the SRE Agent with external tools:

✅ **Simple** - Direct tool addition to meta_agent  
✅ **Fast** - No agent generation overhead  
✅ **Secure** - Multiple authentication options  
✅ **Flexible** - SSE and STDIO connection types  
✅ **Robust** - Health monitoring and automatic failover  
✅ **Dynamic** - Runtime connection management  
✅ **Scalable** - Tool limiting prevents overload  
✅ **Reliable** - Heartbeat monitoring ensures availability  
✅ **Observable** - Comprehensive logging and monitoring  
✅ **Maintainable** - Clear error messages and diagnostics  

---

## Quick Reference Card

### API Endpoints

| Method | Endpoint | Purpose |
|--------|----------|---------|
| GET | `/api/v1/mcp/connections/list` | List all connections |
| GET | `/api/v1/mcp/connections/{id}` | Get connection details |
| POST | `/api/v1/mcp/connections/test/{id}` | Test connection health |

**Note:** Connections are managed via `appsettings.json` configuration. See [Configuration](#configuration) section. Failed connections persist with "Failed" status.

### Configuration Settings

```json
{
  "MCP": {
    "Enabled": true,                // Master enable/disable switch
    "PingIntervalInSeconds": 60,    // Heartbeat interval
    "PingTimeoutInSeconds": 10,     // Ping timeout
    "MaxToolsPerConnection": 20,    // Tool limit per connection
    "StdioConnections": []          // Local process connections
  }
}
```

### Authentication Types

- **ApiKey** - Custom API key in headers
- **Bearer** - Bearer token authentication
- **Basic** - HTTP Basic authentication  
- **CustomHeaders** - Arbitrary headers (LogicApps-style)
- **AzureARM** - Azure ARM authentication for Logic Apps
- **None** - No authentication

### Connection Types

- **HTTP (StreamableHttp)** - Remote HTTP servers

### Tool Limits

- **20 tools** per connection (first 20 tools taken)
- Enforced in UI via Agent Builder
- Configured via `MaxToolsPerConnection` setting
