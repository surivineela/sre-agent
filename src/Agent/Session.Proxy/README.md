# Session Proxy Server

A generic proxy server for running subprocesses in session environments. This server combines two main functionalities:
1. **MCP Proxy**: WebSocket-based proxy for local MCP (Model Context Protocol) servers
2. **CLI Execution**: HTTP endpoints for executing Azure CLI and kubectl commands

## Features

### MCP Proxy
- Accepts WebSocket connections at `/mcp/run` endpoint
- Launches local MCP servers as child processes based on client commands
- Proxies data bidirectionally between WebSocket and MCP server stdio
- Supports multiple concurrent WebSocket connections, each with its own MCP server instance

#### Data Flow

```
              [agent]                       http                  [session pool]
[MCP client <---------> WebSocket Client] <------> [WebSocket Server <--> MCP Server Process]
 (tool call)              (messages)                  (messages)            (stdin/stdout)
```

The MCP protocol uses line-based JSON-RPC communication:
- Each WebSocket message is written as a single line to the MCP server's stdin
- Each line from the MCP server's stdout is sent as a WebSocket message
- Empty lines are skipped (following the official SDK pattern)
- On Windows, commands are wrapped with `cmd.exe /c` for proper PATH resolution

### CLI Execution
- HTTP endpoints for executing shell commands in a managed session
- Supports Azure CLI (`az`) commands with managed identity authentication
- Supports kubectl commands (planned)
- Redirects identity requests to local MSI endpoint

## API

### WebSocket Endpoint: `/mcp/run` (MCP Proxy)

**Protocol:**
1. Client connects to WebSocket endpoint (no query parameters required)
2. Client sends a JSON message with connection parameters:
   ```json
   {
     "cmd": "npx",
     "args": ["-y", "@modelcontextprotocol/server-everything"],
     "envVars": {
       "API_KEY": "secret",
       "DEBUG": "true"
     },
     "protocolVersion": 2
   }
   ```
3. Server validates the connection request and protocol version
4. Server sends first message: `"ok"` on success, or an error message on failure
5. If error, connection is closed immediately
6. If success, server launches the MCP server process with the specified environment variables
7. Bidirectional proxying begins between WebSocket and MCP server stdio
8. Connection remains open until client disconnects or MCP server exits

**Connection Request Format:**
- `cmd` (required, string): The command to execute (e.g., `npx`, `node`, `uvx`)
- `args` (required, array): Array of command arguments
- `envVars` (optional, object): Dictionary of environment variables to set for the MCP server process
- `actionTokens` (optional, object): Dictionary of action tokens (scope -> token mapping) for managed identity authentication
- `protocolVersion` (optional, integer): Protocol version to use (1 or 2, defaults to 1 if not specified)

**Protocol Versions:**

The MCP Proxy supports two protocol versions:

**Protocol v2 (Recommended):**
- All messages from the MCP server are prefixed with a channel indicator:
  - `'1'` = stdout (JSON-RPC messages)
  - `'2'` = stderr (diagnostic/error output)
- Example stdout message: `1{"jsonrpc":"2.0","result":{...},"id":1}`
- Example stderr message: `2Invalid arguments!`
- Handshake message (`"ok"`) is NOT prefixed with a channel indicator
- Stderr is forwarded to the client in real-time for debugging
- Multi-line stderr and UTF-8 characters (including emojis) are properly handled

**Protocol v1 (Legacy):**
- Messages are sent as plain text without channel indicators
- Only stdout is forwarded to the client
- Stderr is logged on the server but not forwarded to the client

**Example Connection Requests:**

```json
{
  "cmd": "npx",
  "args": ["-y", "@modelcontextprotocol/server-everything"],
  "protocolVersion": 2
}
```

With environment variables:
```json
{
  "cmd": "node",
  "args": ["server.js"],
  "envVars": {
    "API_KEY": "secret",
    "DEBUG": "true"
  },
  "protocolVersion": 2
}
```

### HTTP Endpoints: `/shellexecute` (CLI Execution)

**POST /shellexecute/azcli**
- Executes Azure CLI commands with managed identity authentication
- Request body: `AzCliExecutionRequest`
- Returns: `ShellExecuteResponse`

**POST /shellexecute/kubectl**
- Executes kubectl commands (planned)
- Request body: `KubectlExecutionRequest`
- Returns: `ShellExecuteResponse`

## Running the Server

```bash
cd src/Agent/Session.Proxy
dotnet run
```

The server will start on `http://localhost:5000` by default.

## Testing with the MCP Test Client

A simple test client is included in the project. The test client uses **protocol v2** by default and displays:
- Stdout messages normally
- Stderr messages in **red** with a `[STDERR]` prefix

Run it with:

**Basic usage:**
```bash
cd src/Agent/Session.Proxy
dotnet run -c Release -- TestClient ws://localhost:5000/mcp/run npx -y @modelcontextprotocol/server-everything
```

**With environment variables:**
```bash
cd src/Agent/Session.Proxy
dotnet run -c Release -- TestClient --env API_KEY=secret --env DEBUG=true ws://localhost:5000/mcp/run node server.js
```

**With Azure session mode:**
```bash
cd src/Agent/Session.Proxy
dotnet run -c Release -- TestClient --session wss://<session-pool-hostname>/mcp/run npx -y @modelcontextprotocol/server-everything
```

After connecting, send the initialization message:
```json
{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{"roots":{"listChanged":true},"sampling":{},"elicitation":{}},"clientInfo":{"name":"ExampleClient","title":"Example Client Display Name","version":"1.0.0"}}}
```

If everything is set up correctly, you should see responses from the MCP server like the following (note the '1' prefix indicating stdout in protocol v2):
```json
{"result":{"protocolVersion":"2024-11-05","capabilities":{"prompts":{},"resources":{"subscribe":true},"tools":{},"logging":{},"completions":{}},"serverInfo":{"name":"example-servers/everything","title":"Everything Example Server","version":"1.0.0"},"instructions":"Testing and demonstration server for MCP protocol features..."},"jsonrpc":"2.0","id":1}
```
