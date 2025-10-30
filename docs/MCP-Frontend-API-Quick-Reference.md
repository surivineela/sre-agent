# MCP Frontend Guide for the SRE Agent - Quick Reference

## 📋 What's Included

The complete frontend integration guide (`MCP-Frontend-API-Guide.md`) includes:

### 1. **TypeScript Type Definitions** (Complete)
- ✅ All connection types and configurations
- ✅ **HTTP transport (StreamableHttp - recommended)** and legacy SSE support
- ✅ Uses `ModelContextProtocol.Client.HttpClientTransport` with `HttpTransportMode.StreamableHttp`
- ✅ Request/response interfaces
- ✅ Authentication types (ApiKey, Bearer, Basic, CustomHeaders, AzureARM)
- ✅ Error response types
- ✅ Tool definitions with maxToolsPerConnection support

### 2. **API Client Setup**
- ✅ Axios client configuration
- ✅ Error interceptors
- ✅ Service layer pattern
- ✅ Singleton pattern

### 3. **All 6 REST API Endpoints with TypeScript Examples**

**Updated October 2025**: All connection management endpoints are available.

| Endpoint | Method | Purpose | Example |
|----------|--------|---------|---------|
| `/list` | GET | List connections | ✅ Full TypeScript example |
| `/{id}` | GET | Get connection details | ✅ Full TypeScript example |
| `/test/{id}` | POST | Test connection health | ✅ Full TypeScript example |
| `/connect` | POST | Create new connection | ✅ Full TypeScript example |
| `/disconnect/{id}` | DELETE | Remove connection | ✅ Full TypeScript example |
| `/reconnect/{id}` | POST | Refresh connection | ✅ Full TypeScript example |

### 4. **React Integration**
- ✅ Custom hooks (`useMcpConnections`, `useCreateConnection`, `useDeleteConnection`, `useRefreshConnection`)
- ✅ React Query hooks (recommended)
- ✅ Complete component examples
- ✅ Loading states and error handling
- ✅ Failed connection handling with persistence

### 5. **Real-World Components**
- ✅ `ConnectionManager` - Full CRUD operations
- ✅ `ConnectionCard` - Individual connection display with actions
- ✅ `ConnectionForm` - Create/edit connections
- ✅ `ErrorAlert` - Error display component
- ✅ Failed connection UI with recovery hints

### 6. **Error Handling**
- ✅ Error handler utilities
- ✅ User-friendly error messages
- ✅ HTTP status code mapping
- ✅ 503 Service Unavailable handling for disabled MCP

### 7. **Best Practices**
- ✅ Dynamic connection management via API
- ✅ Type safety
- ✅ Loading states
- ✅ Error recovery
- ✅ Optimistic updates
- ✅ Connection lifecycle management

### 8. **Troubleshooting**
- ✅ CORS configuration
- ✅ 503 Service Unavailable (MCP disabled)
- ✅ Stale data issues
- ✅ Authentication problems
- ✅ Failed connection recovery

---

## 🚀 Quick Start (5 minutes)

### Step 1: Install Dependencies

```bash
npm install axios @tanstack/react-query
```

### Step 2: Copy Types

Copy the TypeScript type definitions from the guide into:
```
src/types/mcp.types.ts
```

### Step 3: Set up API Client

Copy the API client setup into:
```
src/api/mcpClient.ts
src/services/mcpService.ts
```

**Note:** The service includes all CRUD operations: `listConnections`, `getConnection`, `createConnection`, `deleteConnection`, `refreshConnection`, and `testConnection`.

### Step 4: Use in Components

```typescript
import { useMcpConnections, useCreateConnection, useDeleteConnection } from './hooks/useMcpQuery';

function MyComponent() {
  const { data: connections, isLoading } = useMcpConnections();
  const createMutation = useCreateConnection();
  const deleteMutation = useDeleteConnection();
  
  if (isLoading) return <div>Loading...</div>;
  
  return (
    <div>
      <button onClick={() => createMutation.mutate({
        name: 'my-server',
        type: 'sse',
        endpoint: 'http://localhost:3000/sse'
      })}>
        Add Connection
      </button>
      
      {connections?.map(conn => (
        <div key={conn.connectionId}>
          <h3>{conn.name}</h3>
          <span className={`status ${conn.status.toLowerCase()}`}>
            {conn.status}
          </span>
          {conn.status === 'Failed' && (
            <p className="error">⚠️ {conn.errorMessage}</p>
          )}
          <p>Tools: {conn.toolCount} / {conn.maxToolsPerConnection}</p>
          <button onClick={() => deleteMutation.mutate(conn.connectionId)}>
            Remove
          </button>
        </div>
      ))}
    </div>
  );
}
```

---

## 🔑 Key Concepts

### API-Based Management (October 2025)

Connections can be dynamically managed via REST API endpoints:

**Creating a Connection:**
```typescript
POST /api/v1/mcp/connections/connect
{
  "name": "github_mcp",
  "type": "stdio",
  "command": "node",
  "arguments": ["path/to/github-mcp-server.js"],
  "enabled": true
}
```

**Managing Connections:**
1. Create: `POST /connect`
2. List: `GET /list`
3. Get: `GET /{id}`
4. Test: `POST /test/{id}`
5. Refresh: `POST /reconnect/{id}`
6. Delete: `DELETE /disconnect/{id}`

### Component Pattern

```tsx
function ConnectionManager() {
  const { data: connections } = useMcpConnections();
  const createMutation = useCreateConnection();
  const deleteMutation = useDeleteConnection();
  const refreshMutation = useRefreshConnection();

  return (
    <div>
      {connections?.map(conn => (
        <ConnectionCard key={conn.connectionId} connection={conn} />
      ))}
    </div>
  );
}
```

---

## 📖 Code Examples Included

### Managing Connections

**1. Create Connection**
```typescript
const createMutation = useCreateConnection();

const handleCreate = async () => {
  await createMutation.mutateAsync({
    name: 'my-server',
    type: 'sse',
    endpoint: 'http://localhost:3000/sse',
    description: 'My MCP Server'
  });
};
```

**2. Delete Connection**
```typescript
const deleteMutation = useDeleteConnection();

const handleDelete = async (id: string) => {
  await deleteMutation.mutateAsync(id);
};
```

**3. Refresh Connection**
```typescript
const refreshMutation = useRefreshConnection();

const handleRefresh = async (id: string) => {
  const result = await refreshMutation.mutateAsync(id);
  console.log('Old tools:', result.oldToolCount);
  console.log('New tools:', result.newToolCount);
};
```

**4. List Connections**
```typescript
const { data: connections } = useMcpConnections();

return (
  <div>
    {connections?.map(conn => (
      <ConnectionCard key={conn.connectionId} connection={conn} />
    ))}
  </div>
);
```

```typescript
const { data: connections } = useMcpConnections();

return (
  <div>
    {connections?.map(conn => (
      <ConnectionCard key={conn.connectionId} connection={conn} />
    ))}
  </div>
);
```

**5. Test Connection Health**
```typescript
const testMutation = useTestConnection();

const handleTest = async (id: string) => {
  const result = await testMutation.mutateAsync(id);
  console.log('Response time:', result.responseTimeMs, 'ms');
  
  if (!result.success) {
    console.error('Test failed:', result.error);
  }
};
```

---

## 📦 What Frontend Developers Get

### Type Safety
- ✅ Complete TypeScript definitions
- ✅ IDE autocomplete support
- ✅ Compile-time error checking
- ✅ Refactoring safety

### Developer Experience
- ✅ Copy-paste ready code
- ✅ React hooks for CRUD operations
- ✅ React Query support (recommended)
- ✅ Error handling built-in
- ✅ Full connection management UI patterns

### Production Ready
- ✅ Loading states
- ✅ Error boundaries
- ✅ Failed connection handling
- ✅ Optimistic updates
- ✅ Real-time connection status

### Security
- ✅ API-based access control
- ✅ ARM operation authorization
- ✅ Secure credential handling
- ✅ HTTPS enforcement

---

## 🎯 Use Cases Covered

1. **Creating Connections**
   - Add new MCP servers dynamically
   - Configure authentication
   - Set connection parameters
   - Validate before creation

2. **Managing Connections**
   - View all connections with status
   - View connection details and tools
   - Delete unused connections
   - Refresh connection tools

3. **Monitoring Health**
   - Test connection responsiveness
   - View last heartbeat timestamps
   - Track tool availability
   - Monitor connection status changes

4. **Error Handling**
   - Network errors
   - 503 Service Unavailable (MCP disabled)
   - Failed connection display
   - User-friendly messages

5. **React Integration**
   - Custom hooks for all operations
   - React Query hooks
   - Component examples
   - Status display patterns

---

## 📝 File Structure Recommendation

```
src/
├── api/
│   └── mcpClient.ts           # Axios client setup
├── services/
│   └── mcpService.ts          # API service layer (full CRUD)
├── hooks/
│   ├── useMcpConnections.ts   # Custom hooks for all operations
│   └── useMcpQuery.ts         # React Query hooks
├── types/
│   └── mcp.types.ts           # TypeScript types
├── components/
│   ├── ConnectionManager.tsx   # Full CRUD component
│   ├── ConnectionCard.tsx      # Card display with actions
│   ├── ConnectionForm.tsx      # Create/edit form
│   ├── ConnectionTest.tsx      # Health testing UI
│   └── ErrorAlert.tsx          # Error display
└── utils/
    └── errorHandler.ts         # Error utilities
```

---

## 🔗 Key Takeaways

### For React Developers
- ✅ Use React Query hooks (recommended)
- ✅ Handle loading/error states
- ✅ Use TypeScript for type safety
- ✅ Implement optimistic updates
- ✅ Display connection status with actions

### For API Integration
- ✅ 6 full CRUD endpoints (list, get, create, delete, refresh, test)
- ✅ Request/response examples
- ✅ Error handling patterns
- ✅ Dynamic connection management

### For Production
- ✅ API-based connection management
- ✅ Comprehensive error handling
- ✅ Performance monitoring via test endpoint
- ✅ User-friendly messages for failures
- ✅ Tool limit enforcement display

---

## 📚 Additional Resources

- **Full Guide**: `docs/MCP-Frontend-API-Guide.md` (detailed examples)
- **Backend Guide**: `docs/MCP-Complete-Guide.md` (API reference)
- **Type Definitions**: Complete in frontend guide
- **Component Library**: Ready-to-use React components for viewing connections

---