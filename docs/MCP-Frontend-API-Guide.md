# MCP REST API for the SRE Agent - Frontend Integration Guide

## Table of Contents

1. [Overview](#overview)
2. [Getting Started](#getting-started)
3. [TypeScript Type Definitions](#typescript-type-definitions)
4. [API Client Setup](#api-client-setup)
5. [API Endpoints with Examples](#api-endpoints-with-examples)
6. [React Hooks](#react-hooks)
7. [Error Handling](#error-handling)
8. [Real-World Examples](#real-world-examples)
9. [Best Practices](#best-practices)
10. [Troubleshooting](#troubleshooting)

---

## Overview

The MCP (Model Context Protocol) REST API enables you to view and test external tool connections in the SRE Agent. This guide provides TypeScript/React examples for frontend integration.

**Important:** As of October 2025, MCP connections are managed through configuration files (`appsettings.json`) rather than dynamically through the API. The API provides read-only access to view connections and test their health.

### Base URL

```typescript
const API_BASE_URL = 'https://your-agent-api.com/api/v1/mcp/connections';
```

### Key Capabilities

✅ List all active connections  
✅ Get detailed connection information  
✅ Test connection health and performance  
✅ View connection status and error messages  
✅ Monitor tool availability  

**Note:** To add, modify, or remove connections, update the `appsettings.json` configuration file and restart the application.  

---

## Getting Started

### Prerequisites

```bash
npm install axios
# or
npm install @tanstack/react-query  # Recommended for React apps
```

### Quick Example - List Connections

```typescript
import axios from 'axios';

const API_BASE_URL = 'https://localhost:7023/api/v1/mcp/connections';

// List all MCP connections
const listConnections = async () => {
  try {
    const response = await axios.get(`${API_BASE_URL}/list`);
    
    console.log('Active connections:', response.data);
    response.data.forEach(conn => {
      console.log(`${conn.name}: ${conn.status} (${conn.toolCount} tools)`);
    });
  } catch (error) {
    console.error('Failed to list connections:', error);
  }
};
```

### Configuration-Based Management

Connections are managed via `appsettings.json`:

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
        "Enabled": true
      }
    ]
  }
}
```

To add or modify connections:
1. Update `appsettings.json`
2. Restart the application
3. Verify via the List API endpoint

---

## TypeScript Type Definitions

### Core Types

```typescript
// src/types/mcp.types.ts

/**
 * MCP connection types
 */
export type ConnectionType = 'http';

/**
 * HTTP transport modes (for 'http' connection type)
 */
export type HttpTransportMode = 'StreamableHttp' | 'Sse' | 'AutoDetect';

/**
 * MCP connection status
 */
export type ConnectionStatus = 'Connected' | 'Disconnected' | 'Failed' | 'Error';

/**
 * Authentication types
 */
export type AuthenticationType = 'ApiKey' | 'Bearer' | 'Basic' | 'CustomHeaders' | 'AzureARM' | 'None';

/**
 * Service type is a free-form string for categorization.
 * Common values include: 'GitHub', 'Datadog', 'Dynatrace', 'LogicApps', 'Custom'
 * but any string value is acceptable.
 */
export type McpServiceType = string;

/**
 * API Key authentication configuration
 */
export interface ApiKeyAuth {
  type: 'ApiKey';
  apiKey: string;
  apiKeyHeader?: string;  // Default: "X-API-Key"
  apiKeyPrefix?: string | null;  // Optional prefix (e.g., "Bearer")
}

/**
 * Bearer token authentication configuration
 */
export interface BearerAuth {
  type: 'Bearer';
  bearerToken: string;
}

/**
 * Basic authentication configuration
 */
export interface BasicAuth {
  type: 'Basic';
  username: string;
  password: string;
}

/**
 * Custom headers authentication configuration
 */
export interface CustomHeadersAuth {
  type: 'CustomHeaders';
  customHeaders: Record<string, string>;
}

/**
 * Azure ARM authentication configuration (for Logic Apps)
 */
export interface AzureARMAuth {
  type: 'AzureARM';
  armScope: string;  // e.g., "https://management.azure.com/.default"
}

/**
 * No authentication
 */
export interface NoAuth {
  type: 'None';
}

/**
 * Union type for all authentication methods
 */
export type Authentication = ApiKeyAuth | BearerAuth | BasicAuth | CustomHeadersAuth | AzureARMAuth | NoAuth;

/**
 * HTTP connection configuration (StreamableHttp - Recommended)
 */
export interface HttpConnectionConfig {
  name: string;
  type: 'http';
  endpoint: string;
  authentication?: Authentication;
  description?: string;
  serviceType?: McpServiceType;
}

/**
 * SSE connection configuration (Legacy)
 */
export interface SseConnectionConfig {
  name: string;
  type: 'sse';
  endpoint: string;
  authentication?: Authentication;
  description?: string;
  serviceType?: McpServiceType;
}

/**
 * STDIO connection configuration
 */
export interface StdioConnectionConfig {
  name: string;
  type: 'stdio';
  command: string;
  arguments?: string[];
  workingDirectory?: string;
  authentication?: Authentication;
  description?: string;
  serviceType?: McpServiceType;
}

/**
 * Union type for connection configurations
 */
export type ConnectionConfig = HttpConnectionConfig | SseConnectionConfig | StdioConnectionConfig;

/**
 * Tool definition
 */
export interface Tool {
  name: string;
  description: string;
  id?: string;
  toolId?: string;
  type?: string;
  inputs?: any;
  parameters?: any;
  example?: string;
}

/**
 * MCP Connection response
 */
export interface McpConnection {
  connectionId: string;
  name: string;
  type: ConnectionType;
  endpoint?: string;
  status: ConnectionStatus;
  errorMessage?: string;  // Present if status is "Failed"
  toolCount: number;
  authenticationType: AuthenticationType;
  lastHeartbeat?: string;  // ISO 8601 date string
  tools?: Tool[];
  description?: string;
  serviceType?: McpServiceType;
  maxToolsPerConnection: number;  // Maximum tools allowed per connection
}

/**
 * Create connection request
 */
export interface CreateConnectionRequest extends ConnectionConfig {}

/**
 * Create connection response
 */
export interface CreateConnectionResponse extends McpConnection {}

/**
 * Test connection response
 */
export interface TestConnectionResponse {
  connectionId: string;
  success: boolean;
  message: string;
  responseTimeMs: number;
  error?: string;
}

/**
 * Refresh connection response
 */
export interface RefreshConnectionResponse {
  connectionId: string;
  success: boolean;
  message: string;
  refreshTimeMs: number;
  oldToolCount: number;
  newToolCount: number;
  connection?: McpConnection;
}

/**
 * API error response
 */
export interface ApiErrorResponse {
  error: string;
  exceptionType?: string;
  stackTrace?: string;
  innerException?: {
    error: string;
    exceptionType?: string;
    stackTrace?: string;
  };
}
```

---

## API Client Setup

### Basic Axios Client

```typescript
// src/api/mcpClient.ts

import axios, { AxiosInstance, AxiosError } from 'axios';
import { ApiErrorResponse } from '../types/mcp.types';

/**
 * MCP API Client configuration
 */
export class McpApiClient {
  private client: AxiosInstance;
  
  constructor(baseUrl: string) {
    this.client = axios.create({
      baseURL: `${baseUrl}/api/v1/mcp/connections`,
      headers: {
        'Content-Type': 'application/json',
      },
      timeout: 30000, // 30 seconds
    });
    
    // Add response interceptor for error handling
    this.client.interceptors.response.use(
      (response) => response,
      (error: AxiosError<ApiErrorResponse>) => {
        return Promise.reject(this.handleError(error));
      }
    );
  }
  
  /**
   * Handle API errors
   */
  private handleError(error: AxiosError<ApiErrorResponse>): Error {
    if (error.response?.data) {
      const apiError = error.response.data;
      const message = apiError.error || 'Unknown API error';
      const enrichedError = new Error(message);
      (enrichedError as any).apiError = apiError;
      (enrichedError as any).statusCode = error.response.status;
      return enrichedError;
    }
    
    return error;
  }
  
  /**
   * Get the axios instance for custom requests
   */
  public getClient(): AxiosInstance {
    return this.client;
  }
}

// Export singleton instance
export const mcpApi = new McpApiClient(
  process.env.REACT_APP_API_BASE_URL || 'https://localhost:7023'
);
```

### API Service Layer

```typescript
// src/services/mcpService.ts

import { mcpApi } from '../api/mcpClient';
import {
  CreateConnectionRequest,
  CreateConnectionResponse,
  McpConnection,
  TestConnectionResponse,
  RefreshConnectionResponse,
} from '../types/mcp.types';

/**
 * MCP Service - All API operations
 */
export class McpService {
  /**
   * List all MCP connections
   */
  static async listConnections(): Promise<McpConnection[]> {
    const response = await mcpApi.getClient().get<McpConnection[]>('/list');
    return response.data;
  }
  
  /**
   * Get a specific connection by ID
   */
  static async getConnection(connectionId: string): Promise<McpConnection> {
    const response = await mcpApi.getClient().get<McpConnection>(`/${connectionId}`);
    return response.data;
  }
  
  /**
   * Test connection health
   */
  static async testConnection(connectionId: string): Promise<TestConnectionResponse> {
    const response = await mcpApi.getClient().post<TestConnectionResponse>(`/test/${connectionId}`);
    return response.data;
  }
}

/**
 * Note: As of October 2025, the following operations are no longer supported via API:
 * - createConnection() - Use appsettings.json configuration instead
 * - refreshConnection() - Restart application to reload connections
 * - removeConnection() - Remove from appsettings.json and restart
 * 
 * To manage connections:
 * 1. Update appsettings.json configuration
 * 2. Restart the application
 * 3. Verify via listConnections()
 */
```

---

## API Endpoints with Examples

### Available Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/list` | List all MCP connections |
| `GET` | `/{id}` | Get connection details |
| `POST` | `/test/{id}` | Test connection health |

---

### 1. List Connections (GET /list)

```typescript
import { McpService } from '../services/mcpService';
import { CreateConnectionRequest } from '../types/mcp.types';

const createGitHubConnection = async () => {
  const config: CreateConnectionRequest = {
    name: 'github_mcp',
    type: 'http', // ⭐ StreamableHttp transport (recommended)
    endpoint: 'https://api.github.com/mcp',
    description: 'GitHub MCP for repository operations and issue management',
    serviceType: 'GitHub',
    authentication: {
      type: 'Bearer',
      bearerToken: process.env.GITHUB_TOKEN || ''
    }
  };
  
  try {
    const connection = await McpService.createConnection(config);
    
    console.log('✅ Connection created with StreamableHttp transport!');
    console.log('Connection ID:', connection.connectionId);
    console.log('Tools available:', connection.toolCount);
    console.log('Status:', connection.status);
    console.log('Transport:', connection.type); // "http"
    console.log('Service Type:', connection.serviceType); // "GitHub"
    
    return connection;
  } catch (error: any) {
    console.error('❌ Failed to create connection:', error.message);
    
    if (error.statusCode === 502) {
      console.error('Server unreachable or authentication failed');
    } else if (error.statusCode === 400) {
      console.error('Invalid request parameters');
    }
    
    throw error;
  }
};
```

#### Example 2: Datadog Service (HTTP)

```typescript
const createDatadogConnection = async (apiKey: string) => {
  const config: CreateConnectionRequest = {
    name: 'datadog_mcp',
    type: 'http', // ⭐ StreamableHttp transport
    endpoint: 'https://mcp.datadoghq.com/mcp',
    description: 'Datadog MCP for metrics, logs, and monitoring',
    serviceType: 'Datadog',
    authentication: {
      type: 'ApiKey',
      apiKey: apiKey,
      apiKeyHeader: 'DD-API-KEY'
    }
  };
  
  const connection = await McpService.createConnection(config);
  return connection;
};
```

#### Example 3: Dynatrace Service (HTTP)

```typescript
const createDynatraceConnection = async (apiToken: string, environment: string) => {
  const config: CreateConnectionRequest = {
    name: 'dynatrace_mcp',
    type: 'http', // ⭐ StreamableHttp transport
    endpoint: `https://${environment}.dynatrace.com/mcp`,
    description: 'Dynatrace MCP for observability and traces',
    serviceType: 'Dynatrace',
    authentication: {
      type: 'ApiKey',
      apiKey: apiToken,
      apiKeyHeader: 'Authorization',
      apiKeyPrefix: 'Api-Token'
    }
  };
  
  const connection = await McpService.createConnection(config);
  return connection;
};
```

#### Example 4: Logic App Service - Azure DevOps Connector (HTTP)

```typescript
const createLogicAppConnection = async (logicAppKey: string) => {
  const config: CreateConnectionRequest = {
    name: 'azure_devops_la',
    type: 'http', // ⭐ StreamableHttp transport
    endpoint: 'https://my-logic-app.azurewebsites.net/api/mcp',
    description: 'Azure DevOps connector via Logic App for work items and pipelines',
    serviceType: 'LogicApp',
    authentication: {
      type: 'ApiKey',
      apiKey: logicAppKey,
      apiKeyHeader: 'x-functions-key'
    }
  };
  
  const connection = await McpService.createConnection(config);
  return connection;
};
```

#### Example 5: Custom MCP Server with API Key (HTTP)

```typescript
const createCustomConnection = async (apiKey: string) => {
  const config: CreateConnectionRequest = {
    name: 'custom_mcp_server',
    type: 'http', // ⭐ StreamableHttp transport
    endpoint: 'https://my-mcp-server.com/mcp',
    description: 'Custom monitoring MCP server for internal metrics',
    serviceType: 'Custom',
    authentication: {
      type: 'ApiKey',
      apiKey: apiKey,
      apiKeyHeader: 'X-API-Key',
      apiKeyPrefix: null  // No prefix
    }
  };
  
  const connection = await McpService.createConnection(config);
  return connection;
};
```

#### Example 6: Local STDIO Connection

```typescript
const createLocalConnection = async () => {
  const config: CreateConnectionRequest = {
    name: 'local_mcp',
    type: 'stdio',
    command: 'node',
    arguments: ['./mcp-server.js'],
    workingDirectory: '/path/to/server',
    description: 'Local development MCP server',
    serviceType: 'Custom'
  };
  
  const connection = await McpService.createConnection(config);
  return connection;
};
```

#### Example 7: Custom Headers Authentication

```typescript
const createWithCustomHeaders = async (tenantId: string, apiToken: string) => {
  const config: CreateConnectionRequest = {
    name: 'enterprise_mcp',
    type: 'sse',
    endpoint: 'https://enterprise.example.com/mcp',
    description: 'Enterprise MCP server with multi-tenant support',
    serviceType: 'Custom',
    authentication: {
      type: 'CustomHeaders',
      customHeaders: {
        'X-Tenant-ID': tenantId,
        'X-API-Token': apiToken,
        'X-Request-ID': crypto.randomUUID()
      }
    }
  };
  
  const connection = await McpService.createConnection(config);
  return connection;
};
```

---

### 2. List Connections (GET /list)

```typescript
const listAllConnections = async () => {
  try {
    const connections = await McpService.listConnections();
    
    console.log(`Found ${connections.length} connections:`);
    
    connections.forEach((conn, index) => {
      console.log(`\n[${index + 1}] ${conn.name}`);
      console.log(`   Type: ${conn.type}`);
      console.log(`   Status: ${conn.status}`);
      console.log(`   Tools: ${conn.toolCount}`);
      console.log(`   Auth: ${conn.authenticationType}`);
      
      if (conn.lastHeartbeat) {
        const lastSeen = new Date(conn.lastHeartbeat);
        console.log(`   Last Heartbeat: ${lastSeen.toLocaleString()}`);
      }
    });
    
    return connections;
  } catch (error) {
    console.error('Failed to list connections:', error);
    throw error;
  }
};
```

---

### 2. Get Connection Details (GET /{id})

```typescript
const getConnectionDetails = async (connectionId: string) => {
  try {
    const connection = await McpService.getConnection(connectionId);
    
    console.log('Connection Details:');
    console.log('==================');
    console.log('ID:', connection.connectionId);
    console.log('Name:', connection.name);
    console.log('Type:', connection.type);
    console.log('Endpoint:', connection.endpoint);
    console.log('Status:', connection.status);
    console.log('Tool Count:', connection.toolCount);
    
    if (connection.tools && connection.tools.length > 0) {
      console.log('\nAvailable Tools:');
      connection.tools.forEach((tool, index) => {
        console.log(`  ${index + 1}. ${tool.name}`);
        console.log(`     ${tool.description}`);
      });
    }
    
    return connection;
  } catch (error: any) {
    if (error.statusCode === 404) {
      console.error(`Connection '${connectionId}' not found`);
    }
    throw error;
  }
};
```

---

### 3. Test Connection (POST /test/{id})

```typescript
const testConnectionHealth = async (connectionId: string) => {
  try {
    const result = await McpService.testConnection(connectionId);
    
    console.log('Connection Test Results:');
    console.log('========================');
    console.log('Success:', result.success ? '✅' : '❌');
    console.log('Message:', result.message);
    console.log('Response Time:', `${result.responseTimeMs}ms`);
    
    // Performance indicator
    if (result.success) {
      if (result.responseTimeMs < 100) {
        console.log('Performance: Excellent 🚀');
      } else if (result.responseTimeMs < 500) {
        console.log('Performance: Good ✅');
      } else if (result.responseTimeMs < 1000) {
        console.log('Performance: Acceptable ⚠️');
      } else {
        console.log('Performance: Slow 🐌');
      }
    } else {
      console.error('Error:', result.error);
    }
    
    return result;
  } catch (error) {
    console.error('Failed to test connection:', error);
    throw error;
  }
};
```

---

## Connection Health & Lifecycle (Updated October 2025)

### Important Behavioral Changes

**🔥 NEW**: Failed connections are **no longer automatically removed** from the system.

**Previous Behavior:**
- ❌ Connections removed after consecutive ping failures
- ❌ Tools disappeared from UI when connections failed
- ❌ Required reconfiguration to restore

**New Behavior:**
- ✅ **Failed connections persist** - Remain visible with `status: "Failed"`
- ✅ **Tools stay registered** - Visible in listings but throw errors when invoked
- ✅ **Easier recovery** - Restart application with updated configuration
- ✅ **Better UX** - Users see what's wrong via `errorMessage` field

### Connection Status Values

```typescript
export type ConnectionStatus = 'Connected' | 'Disconnected' | 'Failed' | 'Error';
```

- **`Connected`**: Healthy, tools are usable
- **`Disconnected`**: Not connected (initial state or manually disconnected)
- **`Failed`**: Health check failed but connection persists (NEW)
- **`Error`**: Error during connection creation

### Handling Failed Connections

When a connection status is `"Failed"`:

1. **Display Warning**: Show error message from `errorMessage` field
2. **Keep Visible**: Don't hide the connection from the UI
3. **Disable Tools**: Gray out or disable tool buttons with tooltip explaining issue
4. **Recovery Instructions**: Prompt user to check configuration and restart

**Example React Component:**

```typescript
const ConnectionCard: React.FC<{ connection: McpConnection }> = ({ connection }) => {
  const isFailed = connection.status === 'Failed';
  
  return (
    <div className={`connection-card ${isFailed ? 'failed' : ''}`}>
      <h3>{connection.name}</h3>
      <span className={`status status-${connection.status.toLowerCase()}`}>
        {connection.status}
      </span>
      
      {isFailed && (
        <>
          <p className="error-message">⚠️ {connection.errorMessage}</p>
          <p className="recovery-hint">
            To fix: Update appsettings.json configuration and restart the application
          </p>
        </>
      )}
      
      <div className="tools">
        {connection.tools?.map(tool => (
          <button 
            key={tool.name}
            disabled={isFailed}
            title={isFailed ? `Connection unhealthy: ${connection.errorMessage}` : tool.description}
          >
            {tool.name}
          </button>
        ))}
      </div>
      
      <div className="connection-info">
        <span>Max Tools: {connection.maxToolsPerConnection}</span>
        <span>Tool Count: {connection.toolCount}</span>
        {connection.lastHeartbeat && (
          <span>Last Check: {new Date(connection.lastHeartbeat).toLocaleString()}</span>
        )}
      </div>
    </div>
  );
};
```

### Configuration-Based Management

Connections cannot be modified via API. To manage connections:

1. **Add Connection**: Add to `MCP.StdioConnections` in `appsettings.json`
2. **Modify Connection**: Update the configuration entry
3. **Remove Connection**: Remove from configuration
4. **Apply Changes**: Restart the application

---

## React Hooks

### useConnections Hook

```typescript
// src/hooks/useMcpConnections.ts

import { useState, useEffect, useCallback } from 'react';
import { McpService } from '../services/mcpService';
import { McpConnection, CreateConnectionRequest } from '../types/mcp.types';

export const useMcpConnections = () => {
  const [connections, setConnections] = useState<McpConnection[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  
  /**
   * Fetch all connections
   */
  const fetchConnections = useCallback(async () => {
    setLoading(true);
    setError(null);
    
    try {
      const data = await McpService.listConnections();
      setConnections(data);
    } catch (err: any) {
      setError(err.message || 'Failed to fetch connections');
    } finally {
      setLoading(false);
    }
  }, []);
  
  /**
   * Create a new connection
   */
  const createConnection = useCallback(async (config: CreateConnectionRequest) => {
    setLoading(true);
    setError(null);
    
    try {
      const newConnection = await McpService.createConnection(config);
      setConnections((prev) => [...prev, newConnection]);
      return newConnection;
    } catch (err: any) {
      setError(err.message || 'Failed to create connection');
      throw err;
    } finally {
      setLoading(false);
    }
  }, []);
  
  /**
   * Remove a connection
   */
  const removeConnection = useCallback(async (connectionId: string) => {
    setLoading(true);
    setError(null);
    
    try {
      await McpService.removeConnection(connectionId);
      setConnections((prev) => prev.filter((c) => c.connectionId !== connectionId));
    } catch (err: any) {
      setError(err.message || 'Failed to remove connection');
      throw err;
    } finally {
      setLoading(false);
    }
  }, []);
  
  /**
   * Refresh a connection
   */
  const refreshConnection = useCallback(async (connectionId: string) => {
    setLoading(true);
    setError(null);
    
    try {
      const result = await McpService.refreshConnection(connectionId);
      
      // Update the connection in state
      if (result.connection) {
        setConnections((prev) =>
          prev.map((c) => (c.connectionId === connectionId ? result.connection! : c))
        );
      }
      
      return result;
    } catch (err: any) {
      setError(err.message || 'Failed to refresh connection');
      throw err;
    } finally {
      setLoading(false);
    }
  }, []);
  
  /**
   * Test a connection
   */
  const testConnection = useCallback(async (connectionId: string) => {
    setLoading(true);
    setError(null);
    
    try {
      const result = await McpService.testConnection(connectionId);
      return result;
    } catch (err: any) {
      setError(err.message || 'Failed to test connection');
      throw err;
    } finally {
      setLoading(false);
    }
  }, []);
  
  // Load connections on mount
  useEffect(() => {
    fetchConnections();
  }, [fetchConnections]);
  
  return {
    connections,
    loading,
    error,
    fetchConnections,
    createConnection,
    removeConnection,
    refreshConnection,
    testConnection,
  };
};
```

### React Query Hook (Recommended)

```typescript
// src/hooks/useMcpQuery.ts

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { McpService } from '../services/mcpService';
import { CreateConnectionRequest } from '../types/mcp.types';

/**
 * Query key factory
 */
export const mcpKeys = {
  all: ['mcp'] as const,
  lists: () => [...mcpKeys.all, 'list'] as const,
  list: () => [...mcpKeys.lists()] as const,
  details: () => [...mcpKeys.all, 'detail'] as const,
  detail: (id: string) => [...mcpKeys.details(), id] as const,
};

/**
 * List all connections
 */
export const useMcpConnections = () => {
  return useQuery({
    queryKey: mcpKeys.list(),
    queryFn: () => McpService.listConnections(),
    staleTime: 30000, // Consider data fresh for 30 seconds
    refetchInterval: 60000, // Refetch every minute
  });
};

/**
 * Get connection details
 */
export const useMcpConnection = (connectionId: string) => {
  return useQuery({
    queryKey: mcpKeys.detail(connectionId),
    queryFn: () => McpService.getConnection(connectionId),
    enabled: !!connectionId,
  });
};

/**
 * Create connection mutation
 */
export const useCreateConnection = () => {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: (config: CreateConnectionRequest) => McpService.createConnection(config),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: mcpKeys.list() });
    },
  });
};

/**
 * Remove connection mutation
 */
export const useRemoveConnection = () => {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: (connectionId: string) => McpService.removeConnection(connectionId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: mcpKeys.list() });
    },
  });
};

/**
 * Refresh connection mutation
 */
export const useRefreshConnection = () => {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: (connectionId: string) => McpService.refreshConnection(connectionId),
    onSuccess: (_, connectionId) => {
      queryClient.invalidateQueries({ queryKey: mcpKeys.list() });
      queryClient.invalidateQueries({ queryKey: mcpKeys.detail(connectionId) });
    },
  });
};

/**
 * Test connection mutation
 */
export const useTestConnection = () => {
  return useMutation({
    mutationFn: (connectionId: string) => McpService.testConnection(connectionId),
  });
};
```

---

## Error Handling

### Error Handler Utility

```typescript
// src/utils/errorHandler.ts

import { ApiErrorResponse } from '../types/mcp.types';

/**
 * Format API error for display
 */
export const formatApiError = (error: any): string => {
  if (error.apiError) {
    const apiError = error.apiError as ApiErrorResponse;
    return apiError.error;
  }
  
  if (error.response?.data?.error) {
    return error.response.data.error;
  }
  
  if (error.message) {
    return error.message;
  }
  
  return 'An unknown error occurred';
};

/**
 * Get user-friendly error message based on status code
 */
export const getUserFriendlyErrorMessage = (statusCode: number, defaultMessage: string): string => {
  const errorMessages: Record<number, string> = {
    400: 'Invalid request. Please check your input and try again.',
    401: 'Authentication failed. Please check your credentials.',
    403: 'Access denied. You do not have permission to perform this action.',
    404: 'Connection not found. It may have been removed.',
    500: 'Server error. Please try again later.',
    502: 'Unable to connect to the MCP server. Please verify the server is running and authentication is correct.',
    503: 'Service temporarily unavailable. Please try again later.',
  };
  
  return errorMessages[statusCode] || defaultMessage;
};

/**
 * Check if error is a network error
 */
export const isNetworkError = (error: any): boolean => {
  return error.message === 'Network Error' || !error.response;
};

/**
 * Check if error is an authentication error
 */
export const isAuthError = (error: any): boolean => {
  return error.statusCode === 401 || error.statusCode === 403;
};

/**
 * Extract detailed error information for logging
 */
export const getErrorDetails = (error: any): {
  message: string;
  statusCode?: number;
  exceptionType?: string;
  stackTrace?: string;
} => {
  return {
    message: formatApiError(error),
    statusCode: error.statusCode || error.response?.status,
    exceptionType: error.apiError?.exceptionType,
    stackTrace: error.apiError?.stackTrace,
  };
};
```

### Error Display Component

```typescript
// src/components/ErrorAlert.tsx

import React from 'react';
import { formatApiError, getUserFriendlyErrorMessage } from '../utils/errorHandler';

interface ErrorAlertProps {
  error: any;
  onRetry?: () => void;
  onDismiss?: () => void;
}

export const ErrorAlert: React.FC<ErrorAlertProps> = ({ error, onRetry, onDismiss }) => {
  const message = formatApiError(error);
  const statusCode = error.statusCode || error.response?.status;
  const friendlyMessage = statusCode
    ? getUserFriendlyErrorMessage(statusCode, message)
    : message;
  
  return (
    <div className="error-alert" role="alert">
      <div className="error-alert-header">
        <span className="error-icon">❌</span>
        <h3>Error</h3>
      </div>
      
      <p className="error-message">{friendlyMessage}</p>
      
      {statusCode && (
        <p className="error-code">Error Code: {statusCode}</p>
      )}
      
      <div className="error-actions">
        {onRetry && (
          <button onClick={onRetry} className="btn-retry">
            Retry
          </button>
        )}
        {onDismiss && (
          <button onClick={onDismiss} className="btn-dismiss">
            Dismiss
          </button>
        )}
      </div>
    </div>
  );
};
```

---

## Real-World Examples

### Example 1: Connection Management Component

```typescript
// src/components/ConnectionManager.tsx

import React, { useState } from 'react';
import { useMcpConnections, useCreateConnection, useRemoveConnection } from '../hooks/useMcpQuery';
import { CreateConnectionRequest } from '../types/mcp.types';
import { ErrorAlert } from './ErrorAlert';

export const ConnectionManager: React.FC = () => {
  const { data: connections, isLoading, error, refetch } = useMcpConnections();
  const createMutation = useCreateConnection();
  const removeMutation = useRemoveConnection();
  
  const [showCreateForm, setShowCreateForm] = useState(false);
  
  const handleCreate = async (config: CreateConnectionRequest) => {
    try {
      await createMutation.mutateAsync(config);
      setShowCreateForm(false);
    } catch (error) {
      console.error('Failed to create connection:', error);
    }
  };
  
  const handleRemove = async (connectionId: string) => {
    if (!confirm('Are you sure you want to remove this connection?')) {
      return;
    }
    
    try {
      await removeMutation.mutateAsync(connectionId);
    } catch (error) {
      console.error('Failed to remove connection:', error);
    }
  };
  
  if (isLoading) {
    return <div>Loading connections...</div>;
  }
  
  if (error) {
    return <ErrorAlert error={error} onRetry={refetch} />;
  }
  
  return (
    <div className="connection-manager">
      <div className="header">
        <h2>MCP Connections</h2>
        <button onClick={() => setShowCreateForm(true)}>
          + New Connection
        </button>
      </div>
      
      {connections && connections.length === 0 && (
        <div className="empty-state">
          <p>No connections yet. Create your first MCP connection!</p>
        </div>
      )}
      
      <div className="connections-grid">
        {connections?.map((connection) => (
          <ConnectionCard
            key={connection.connectionId}
            connection={connection}
            onRemove={() => handleRemove(connection.connectionId)}
          />
        ))}
      </div>
      
      {showCreateForm && (
        <CreateConnectionModal
          onSubmit={handleCreate}
          onClose={() => setShowCreateForm(false)}
          isLoading={createMutation.isPending}
        />
      )}
    </div>
  );
};
```

### Example 2: Connection Card Component

```typescript
// src/components/ConnectionCard.tsx

import React, { useState } from 'react';
import { McpConnection } from '../types/mcp.types';
import { useTestConnection, useRefreshConnection } from '../hooks/useMcpQuery';

interface ConnectionCardProps {
  connection: McpConnection;
  onRemove: () => void;
}

export const ConnectionCard: React.FC<ConnectionCardProps> = ({ connection, onRemove }) => {
  const testMutation = useTestConnection();
  const refreshMutation = useRefreshConnection();
  const [testResult, setTestResult] = useState<any>(null);
  
  const handleTest = async () => {
    try {
      const result = await testMutation.mutateAsync(connection.connectionId);
      setTestResult(result);
    } catch (error) {
      console.error('Test failed:', error);
    }
  };
  
  const handleRefresh = async () => {
    try {
      await refreshMutation.mutateAsync(connection.connectionId);
    } catch (error) {
      console.error('Refresh failed:', error);
    }
  };
  
  const getStatusColor = () => {
    switch (connection.status) {
      case 'Connected': return 'green';
      case 'Disconnected': return 'gray';
      case 'Error': return 'red';
      default: return 'gray';
    }
  };
  
  const getPerformanceIndicator = (ms: number) => {
    if (ms < 100) return '🚀 Excellent';
    if (ms < 500) return '✅ Good';
    if (ms < 1000) return '⚠️ Acceptable';
    return '🐌 Slow';
  };
  
  return (
    <div className="connection-card">
      <div className="card-header">
        <h3>{connection.name}</h3>
        <span className={`status-badge status-${getStatusColor()}`}>
          {connection.status}
        </span>
      </div>
      
      <div className="card-body">
        <div className="info-row">
          <label>Type:</label>
          <span>{connection.type.toUpperCase()}</span>
        </div>
        
        {connection.endpoint && (
          <div className="info-row">
            <label>Endpoint:</label>
            <span className="endpoint">{connection.endpoint}</span>
          </div>
        )}
        
        <div className="info-row">
          <label>Tools:</label>
          <span>{connection.toolCount}</span>
        </div>
        
        <div className="info-row">
          <label>Auth:</label>
          <span>{connection.authenticationType}</span>
        </div>
        
        {connection.lastHeartbeat && (
          <div className="info-row">
            <label>Last Heartbeat:</label>
            <span>{new Date(connection.lastHeartbeat).toLocaleString()}</span>
          </div>
        )}
        
        {testResult && (
          <div className="test-result">
            <p className={testResult.success ? 'success' : 'error'}>
              {testResult.success ? '✅' : '❌'} {testResult.message}
            </p>
            {testResult.success && (
              <p className="performance">
                {getPerformanceIndicator(testResult.responseTimeMs)} ({testResult.responseTimeMs}ms)
              </p>
            )}
          </div>
        )}
      </div>
      
      <div className="card-actions">
        <button
          onClick={handleTest}
          disabled={testMutation.isPending}
          className="btn-secondary"
        >
          {testMutation.isPending ? 'Testing...' : 'Test'}
        </button>
        
        <button
          onClick={handleRefresh}
          disabled={refreshMutation.isPending}
          className="btn-secondary"
        >
          {refreshMutation.isPending ? 'Refreshing...' : 'Refresh'}
        </button>
        
        <button onClick={onRemove} className="btn-danger">
          Remove
        </button>
      </div>
    </div>
  );
};
```

### Example 3: Create Connection Form

```typescript
// src/components/CreateConnectionForm.tsx

import React, { useState } from 'react';
import { CreateConnectionRequest, ConnectionType, AuthenticationType } from '../types/mcp.types';

interface CreateConnectionFormProps {
  onSubmit: (config: CreateConnectionRequest) => Promise<void>;
  onCancel: () => void;
  isLoading?: boolean;
}

export const CreateConnectionForm: React.FC<CreateConnectionFormProps> = ({
  onSubmit,
  onCancel,
  isLoading = false,
}) => {
  const [name, setName] = useState('');
  const [type, setType] = useState<ConnectionType>('http'); // ⭐ Default to HTTP (StreamableHttp)
  const [endpoint, setEndpoint] = useState('');
  const [authType, setAuthType] = useState<AuthenticationType>('None');
  
  // Auth fields
  const [apiKey, setApiKey] = useState('');
  const [apiKeyHeader, setApiKeyHeader] = useState('X-API-Key');
  const [bearerToken, setBearerToken] = useState('');
  
  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    let authentication: any = undefined;
    
    if (authType === 'ApiKey') {
      authentication = {
        type: 'ApiKey',
        apiKey,
        apiKeyHeader,
        apiKeyPrefix: null,
      };
    } else if (authType === 'Bearer') {
      authentication = {
        type: 'Bearer',
        bearerToken,
      };
    }
    
    const config: CreateConnectionRequest = {
      name,
      type,
      endpoint,
      authentication,
    };
    
    await onSubmit(config);
  };
  
  return (
    <form onSubmit={handleSubmit} className="create-connection-form">
      <h3>Create New MCP Connection</h3>
      
      <div className="form-group">
        <label htmlFor="name">Connection Name *</label>
        <input
          id="name"
          type="text"
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder="e.g., github_mcp"
          required
        />
      </div>
      
      <div className="form-group">
        <label htmlFor="type">Connection Type *</label>
        <select
          id="type"
          value={type}
          onChange={(e) => setType(e.target.value as ConnectionType)}
          required
        >
          <option value="http">HTTP (StreamableHttp - Recommended)</option>
          <option value="sse">SSE (Legacy - uses StreamableHttp internally)</option>
          <option value="stdio">STDIO (Local Process)</option>
        </select>
      </div>
      
      {(type === 'http' || type === 'sse') && (
        <div className="form-group">
          <label htmlFor="endpoint">Endpoint URL *</label>
          <input
            id="endpoint"
            type="url"
            value={endpoint}
            onChange={(e) => setEndpoint(e.target.value)}
            placeholder="https://api.example.com/mcp/sse"
            required
          />
        </div>
      )}
      
      <div className="form-group">
        <label htmlFor="authType">Authentication</label>
        <select
          id="authType"
          value={authType}
          onChange={(e) => setAuthType(e.target.value as AuthenticationType)}
        >
          <option value="None">None</option>
          <option value="ApiKey">API Key</option>
          <option value="Bearer">Bearer Token</option>
          <option value="CustomHeaders">Custom Headers</option>
        </select>
      </div>
      
      {authType === 'ApiKey' && (
        <>
          <div className="form-group">
            <label htmlFor="apiKey">API Key *</label>
            <input
              id="apiKey"
              type="password"
              value={apiKey}
              onChange={(e) => setApiKey(e.target.value)}
              required
            />
          </div>
          
          <div className="form-group">
            <label htmlFor="apiKeyHeader">Header Name</label>
            <input
              id="apiKeyHeader"
              type="text"
              value={apiKeyHeader}
              onChange={(e) => setApiKeyHeader(e.target.value)}
            />
          </div>
        </>
      )}
      
      {authType === 'Bearer' && (
        <div className="form-group">
          <label htmlFor="bearerToken">Bearer Token *</label>
          <input
            id="bearerToken"
            type="password"
            value={bearerToken}
            onChange={(e) => setBearerToken(e.target.value)}
            required
          />
        </div>
      )}
      
      <div className="form-actions">
        <button type="button" onClick={onCancel} className="btn-secondary">
          Cancel
        </button>
        <button type="submit" disabled={isLoading} className="btn-primary">
          {isLoading ? 'Creating...' : 'Create Connection'}
        </button>
      </div>
    </form>
  );
};
```

---

## Best Practices

### 1. Error Handling

```typescript
// ✅ Good: Comprehensive error handling
try {
  const connection = await McpService.createConnection(config);
  showSuccessNotification('Connection created successfully!');
} catch (error: any) {
  if (error.statusCode === 502) {
    showErrorNotification('Cannot connect to MCP server. Check server status and credentials.');
  } else if (error.statusCode === 400) {
    showErrorNotification('Invalid configuration. Please check your inputs.');
  } else {
    showErrorNotification('An unexpected error occurred. Please try again.');
  }
  
  // Log detailed error for debugging
  console.error('Connection creation failed:', {
    message: error.message,
    statusCode: error.statusCode,
    details: error.apiError,
  });
}

// ❌ Bad: Generic error handling
try {
  await McpService.createConnection(config);
} catch (error) {
  alert('Error!');
}
```

### 2. Loading States

```typescript
// ✅ Good: Clear loading states
const { data, isLoading, isError, error } = useMcpConnections();

if (isLoading) {
  return <LoadingSpinner message="Loading connections..." />;
}

if (isError) {
  return <ErrorAlert error={error} onRetry={refetch} />;
}

return <ConnectionList connections={data} />;

// ❌ Bad: No feedback during loading
const { data } = useMcpConnections();
return <ConnectionList connections={data} />;
```

### 3. Secure Credential Handling

```typescript
// ✅ Good: Environment variables for secrets
const config: CreateConnectionRequest = {
  name: 'github_mcp',
  type: 'sse',
  endpoint: process.env.REACT_APP_GITHUB_MCP_ENDPOINT!,
  authentication: {
    type: 'Bearer',
    bearerToken: process.env.REACT_APP_GITHUB_TOKEN!,
  },
};

// ❌ Bad: Hardcoded credentials
const config = {
  authentication: {
    type: 'Bearer',
    bearerToken: 'ghp_hardcoded_token_123',  // NEVER DO THIS!
  },
};
```

### 4. Optimistic Updates

```typescript
// ✅ Good: Optimistic UI updates with React Query
const removeConnection = useMutation({
  mutationFn: (id: string) => McpService.removeConnection(id),
  onMutate: async (connectionId) => {
    // Cancel outgoing refetches
    await queryClient.cancelQueries({ queryKey: mcpKeys.list() });
    
    // Snapshot previous value
    const previousConnections = queryClient.getQueryData(mcpKeys.list());
    
    // Optimistically update
    queryClient.setQueryData(mcpKeys.list(), (old: McpConnection[]) =>
      old.filter((c) => c.connectionId !== connectionId)
    );
    
    return { previousConnections };
  },
  onError: (err, connectionId, context) => {
    // Rollback on error
    if (context?.previousConnections) {
      queryClient.setQueryData(mcpKeys.list(), context.previousConnections);
    }
  },
  onSettled: () => {
    queryClient.invalidateQueries({ queryKey: mcpKeys.list() });
  },
});
```

### 5. Type Safety

```typescript
// ✅ Good: Proper TypeScript types
const createConnection = async (config: CreateConnectionRequest): Promise<McpConnection> => {
  const response = await McpService.createConnection(config);
  return response;
};

// ❌ Bad: Using 'any'
const createConnection = async (config: any): Promise<any> => {
  return await McpService.createConnection(config);
};
```

---

## Troubleshooting

### Common Issues

#### Issue 1: CORS Errors

```typescript
// Problem: CORS policy blocking requests

// Solution: Configure proxy in package.json (development)
{
  "proxy": "https://localhost:7023"
}

// Or use proxy configuration in vite.config.ts
export default defineConfig({
  server: {
    proxy: {
      '/api': {
        target: 'https://localhost:7023',
        changeOrigin: true,
        secure: false,
      },
    },
  },
});
```

#### Issue 2: 502 Bad Gateway

```typescript
// Problem: MCP server unreachable or auth failed

// Debug steps:
const debugConnection = async (config: CreateConnectionRequest) => {
  console.log('Testing connection with config:', {
    ...config,
    authentication: config.authentication
      ? { ...config.authentication, apiKey: '***', bearerToken: '***' }
      : undefined,
  });
  
  try {
    const result = await McpService.createConnection(config);
    console.log('✅ Connection successful:', result);
  } catch (error: any) {
    console.error('❌ Connection failed:', {
      statusCode: error.statusCode,
      message: error.message,
      details: error.apiError,
    });
    
    // Check specific issues
    if (error.message.includes('401')) {
      console.error('Authentication issue: Check your credentials');
    }
    if (error.message.includes('Connection refused')) {
      console.error('Server unreachable: Verify endpoint URL and server status');
    }
  }
};
```

#### Issue 3: Stale Data

```typescript
// Problem: UI not updating after mutations

// Solution: Invalidate queries after mutations
const createMutation = useMutation({
  mutationFn: McpService.createConnection,
  onSuccess: () => {
    // Invalidate and refetch
    queryClient.invalidateQueries({ queryKey: mcpKeys.list() });
  },
});
```

---

## Summary

This guide provides everything needed to integrate MCP REST API into your frontend application:

✅ **Complete TypeScript types** for type-safe development  
✅ **API client setup** with error handling  
✅ **Service layer** for all operations  
✅ **React hooks** for easy integration  
✅ **Real-world components** you can adapt  
✅ **Best practices** for production use  
✅ **Troubleshooting** common issues  

### Quick Start Checklist

1. ✅ Copy type definitions to your project
2. ✅ Set up API client with your base URL
3. ✅ Create service layer
4. ✅ Build React hooks (or use React Query)
5. ✅ Create UI components
6. ✅ Add error handling
7. ✅ Test with real MCP servers
