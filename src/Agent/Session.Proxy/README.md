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

**Query Parameters:**
- `cmd` (required): The command to execute (e.g., `npx`)
- `args` (required): JSON-encoded array of arguments (e.g., `["-y", "@azure-devops/mcp@next", "msazure"]`)

**Protocol:**
1. Client connects to WebSocket endpoint with query parameters
2. Server launches the MCP server process
3. Server sends first message: `"ok"` on success, or an error message on failure
4. If error, connection is closed immediately
5. If success, bidirectional proxying begins between WebSocket and MCP server stdio
6. Connection remains open until client disconnects or MCP server exits

**Example URL:**
```
ws://localhost:5000/mcp/run?cmd=npx&args=%5B%22-y%22%2C%22%40azure-devops%2Fmcp%40next%22%2C%22msazure%22%5D
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

A simple test client is included in the project. Run it with:

```bash
cd src/Agent/Session.Proxy
dotnet run -c Release -- TestClient ws://localhost:5000/mcp/run npx -y @modelcontextprotocol/server-everything
```

and then send the initialization message:
```json
{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{"roots":{"listChanged":true},"sampling":{},"elicitation":{}},"clientInfo":{"name":"ExampleClient","title":"Example Client Display Name","version":"1.0.0","icons":[{"src":"https://example.com/icon.png","mimeType":"image/png","sizes":["48x48"]}],"websiteUrl":"https://example.com"}}}
```

if everything is set up correctly, you should see responses from the MCP server like the following:
```json
{"result":{"protocolVersion":"2024-11-05","capabilities":{"prompts":{},"resources":{"subscribe":true},"tools":{},"logging":{},"completions":{}},"serverInfo":{"name":"example-servers/everything","title":"Everything Example Server","version":"1.0.0"},"instructions":"Testing and demonstration server for MCP protocol features..."},"jsonrpc":"2.0","id":1}
```
