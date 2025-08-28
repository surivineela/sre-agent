# SRECTL Code Documentation

## Overview

SRECTL (Site Reliability Engineering Command Line Tool) is a .NET CLI application built using the System.CommandLine library. This tool helps developers create, validate, and manage SRE agents and tools through a command-line interface.

## Project Structure

```
Agent.Cli/
├── Program.cs                    # Entry point - minimal, delegates to CommandBuilder
├── Commands/                     # Command-related classes
│   ├── CommandBuilder.cs        # CLI structure and command configuration
│   ├── AgentCommandHandlers.cs  # Agent-related command implementations
│   ├── ToolCommandHandlers.cs   # Tool-related command implementations
│   ├── GeneralCommandHandlers.cs # General command implementations
│   ├── DocumentCommandHandlers.cs # Document management implementations
│   ├── ThreadCommandHandlers.cs # Thread management implementations
│   ├── ProfileCommandHandlers.cs # Profile management implementations
│   ├── IncidentHandlerCommandHandlers.cs # Incident handler command implementations
│   ├── AgentCommandOptions.cs   # Agent command option definitions
│   ├── ToolCommandOptions.cs    # Tool command option definitions
│   ├── DocumentCommandOptions.cs # Document command option definitions
│   ├── ProfileCommandOptions.cs # Profile command option definitions
│   └── IncidentHandlerCommandOptions.cs # Incident handler command option definitions
├── Helpers/                      # Utility classes
│   ├── YamlHelper.cs            # YAML serialization and formatting
│   ├── ArgumentParser.cs        # Key-value pair parsing utilities
│   └── ExampleFileManager.cs    # Example file management and copying
├── Services/                     # Service layer
│   ├── ApiService.cs            # API interaction services
│   └── ToolDefinitionService.cs # Tool and connector type discovery
├── Models/                       # Data models
│   └── YamlAgentDescriptor.cs   # Agent descriptor model
├── Validations/                  # Validation logic
│   ├── AgentDescriptorValidation.cs # Agent validation rules
│   └── ToolValidation.cs        # Tool validation rules
├── scripts/                      # PowerShell and batch scripts
├── templates/                    # Template files for agents/tools
├── agents/                       # Generated agent files
├── tools/                        # Generated tool files
└── connectors/                   # Connector configurations
```

## Architecture Patterns

### 1. Command Pattern
- **CommandBuilder**: Centralizes CLI structure definition
- **Command Handlers**: Separate classes for different command categories
- **Options Classes**: Strongly-typed command option definitions

### 2. Separation of Concerns
- **Presentation Layer**: Program.cs (minimal entry point)
- **Command Layer**: CommandBuilder and command handlers
- **Business Logic**: Services and validation classes
- **Utilities**: Helper classes for common operations
- **Data Models**: POCOs for serialization/deserialization

### 3. Dependency Injection Ready
- All services and handlers are designed to be stateless
- Easy to integrate with DI containers in the future
- Clear separation of dependencies

### 4. Model-Driven Tool and Connector Management
- **Type Discovery**: Uses reflection to discover tool and connector types at runtime
- **Attribute-Based Registration**: Tools register via `ToolTypeAttribute` decorations
- **Template Generation**: Sample YAML templates generated from actual model definitions
- **Framework Integration**: Direct integration with Agent.Framework and Agent.Plugins assemblies
- **Consistency Assurance**: Tool creation uses the same models as the runtime system

## Coding Guidelines

### 1. File Organization
- **One class per file** with matching filename
- **Namespace alignment** with folder structure
- **Logical grouping** by functionality (Commands, Helpers, Services, etc.)

### 2. Naming Conventions
- **PascalCase** for classes, methods, and properties
- **camelCase** for local variables and parameters
- **UPPER_CASE** for constants
- **Descriptive names** that clearly indicate purpose

### 3. Error Handling
- **Consistent error messages** with clear descriptions
- **Validation at boundaries** (command input validation)
- **Graceful degradation** where possible
- **Proper exception propagation** for unrecoverable errors

### 4. Async/Await Patterns
- **Async all the way** - no mixed sync/async patterns
- **ConfigureAwait(false)** for library code
- **Proper exception handling** in async methods
- **CancellationToken support** where applicable

### 5. YAML Handling
- **Snake_case conversion** for property names in YAML output
- **Consistent formatting** with proper indentation
- **Schema validation** before serialization
- **Human-readable output** with proper spacing

## Quality Control Rules

### 1. Build Configuration
- **TreatWarningsAsErrors**: true (Zero tolerance for warnings)
- **Nullable reference types**: enabled
- **Language version**: Latest stable (.NET 9)
- **Deterministic builds**: enabled

### 2. Code Analysis
- **Built-in .NET analyzers**: enabled
- **StyleCop rules**: enforced for consistency
- **Custom analyzers**: for domain-specific rules
- **Static analysis**: as part of CI/CD pipeline

### 3. Testing Strategy
- **Comprehensive test suite**: 23 automated tests
- **Positive and negative test cases**: covering success and failure scenarios
- **Integration testing**: real CLI command execution
- **Format validation**: YAML structure and content verification
- **Bulk operation testing**: ensuring scalability

### 4. Performance Guidelines
- **Minimal object allocation** in hot paths
- **Efficient string operations** (StringBuilder for concatenation)
- **Stream-based I/O** for large files
- **Lazy initialization** where appropriate

### 5. Security Considerations
- **Input validation** for all user-provided data
- **Path traversal protection** for file operations
- **Sanitized error messages** (no sensitive information exposure)
- **Secure defaults** for all configurations

## Command Handler Design

### Pattern Structure
```csharp
public static class [Category]CommandHandlers
{
    public static async Task Handle[Command](ParseResult parseResult)
    {
        // 1. Extract options from parseResult
        // 2. Validate inputs
        // 3. Execute business logic
        // 4. Handle errors gracefully
        // 5. Provide user feedback
    }
}
```

### Error Handling Standard
```csharp
try
{
    // Business logic
    Console.WriteLine($"[SUCCESS] Operation completed");
}
catch (Exception ex)
{
    Console.WriteLine($"[ERROR] {ex.Message}");
    Environment.Exit(1);
}
```

## Helper Class Design

### Stateless Utilities
- All helper classes contain only static methods
- No state management within helpers
- Pure functions where possible
- Clear input/output contracts

### Example Helper Method
```csharp
public static class YamlHelper
{
    public static string SerializeToYaml<T>(T obj)
    {
        // Consistent YAML formatting
        // Snake_case property conversion
        // Proper error handling
    }
}
```

## Testing Standards

### Test Categories
1. **Positive Tests**: Verify successful execution paths
2. **Negative Tests**: Verify proper error handling
3. **Format Tests**: Verify output formatting and structure
4. **Bulk Tests**: Verify performance with multiple operations

### Test File Naming
- Test files use descriptive names with test purpose
- Temporary files are automatically cleaned up
- Test isolation ensures no cross-test dependencies

## Release and Deployment

### Build Process
1. **Compile**: `dotnet build --configuration Release`
2. **Package**: Creates NuGet package for distribution
3. **Install**: `dotnet tool install` for global access
4. **Validate**: Automated test suite execution

### Version Management
- Semantic versioning (Major.Minor.Patch)
- Version updated in .csproj file
- Release notes for each version
- Backward compatibility considerations

## Extension Points

### Adding New Commands
1. Define options class in `Commands/` folder (e.g., ProfileCommandOptions.cs)
2. Implement handler in appropriate handler class (e.g., ProfileCommandHandlers.cs)
3. Register command in `CommandBuilder.cs`
4. Add validation logic if needed
5. Create tests for new functionality
6. Update documentation

**Recent Examples:**
- Profile management commands demonstrate multi-instance support
- Document commands show file system integration patterns
- Thread track command illustrates real-time monitoring capabilities

**Example: Tool Discovery Commands**

The `srectl tool show-types` and `srectl tool show-connectors` commands demonstrate the model-driven approach:

1. **ToolDefinitionService**: Discovers types using reflection and attributes
2. **Command Options**: Defined in `ToolCommandOptions.cs` with verbose and type filtering
3. **Command Handlers**: Implemented in `ToolCommandHandlers.cs` with proper error handling
4. **Integration**: Uses actual framework models for consistency

**Example: Agent Test Command**

The `srectl agent test` command demonstrates integration between agent management and thread functionality:

1. **Command Options**: Defined in `AgentCommandOptions.cs` with agent name, message, and user parameters
2. **Command Handlers**: Implemented in `AgentCommandHandlers.cs` with thread creation and response handling
3. **Message Formatting**: Automatically prefixes user message with agent-specific instructions
4. **Thread Integration**: Creates new conversation threads and optionally starts interactive sessions
5. **Error Handling**: Comprehensive validation and graceful error reporting

### Adding New Validators
1. Create validator class in `Validations/` folder
2. Implement validation interface
3. Integrate with command handlers
4. Add comprehensive test cases

### Adding New Helpers
1. Create helper class in `Helpers/` folder
2. Follow stateless utility pattern
3. Add comprehensive documentation
4. Include unit tests

## Maintenance Guidelines

### Regular Tasks
- **Dependency updates**: Keep NuGet packages current
- **Security scanning**: Regular vulnerability assessments
- **Performance profiling**: Monitor for performance regressions
- **Documentation updates**: Keep documentation synchronized with code

### Code Review Checklist
- [ ] Follows naming conventions
- [ ] Proper error handling
- [ ] Comprehensive tests added
- [ ] Documentation updated
- [ ] No warnings or analyzer violations
- [ ] Performance impact considered
- [ ] Security implications reviewed

## Future Enhancements

### Planned Improvements
- **Configuration management**: External configuration files
- **Plugin architecture**: Support for custom extensions
- **Enhanced logging**: Structured logging with correlation IDs
- **Telemetry integration**: Usage analytics and performance monitoring
- **Interactive mode**: Guided command execution
- **Tool type discovery**: Enhanced introspection and documentation of available tool types

### Technical Debt
- Consider dependency injection container integration
- Evaluate async streaming for large file operations
- Implement caching for frequently accessed data
- Add comprehensive benchmarking suite

---

This documentation serves as the definitive guide for understanding, maintaining, and extending the SRECTL codebase. All developers working on this project should follow these guidelines to ensure consistency, quality, and maintainability.

## Apply YAML Command

### Usage

```
srectl apply-yaml --file <path-to-yaml-file>
```

- Directly applies the specified YAML file to the remote API endpoint without parsing or validation.
- The file is sent as-is with content type `application/yaml`.
- Useful for advanced users who want to push raw YAML definitions.

### Implementation Notes
- See `GeneralCommandHandlers.HandleApplyYamlCommand` and `ApiService.ApplyYamlFileAsync` for details.
- Option defined in `AgentCommandOptions.ApplyYamlFileOption`.

## Profile Management Commands

### Overview
Profile management enables users to work with multiple SRE Agent instances seamlessly. Profiles store connection settings and can be switched between easily.

### Implementation Architecture
- **Profile Storage**: JSON files in `.sreagent-profiles/` directory
- **Current Profile**: Tracked in `.sreagent-current-profile` file
- **Profile Structure**: Contains resource URL and authentication requirements

### Design Principles
- **Isolation**: Each profile maintains independent configuration
- **Persistence**: Profiles survive across sessions
- **Safety**: Cannot delete the active profile
- **Simplicity**: Easy switching between environments

### Command Categories
- **List**: Display all available profiles with active indicator
- **Get**: Retrieve details of specific or current profile
- **Create**: Add new profile with resource URL configuration
- **Set**: Switch active profile context
- **Delete**: Remove unused profiles (with safety checks)

### Implementation Details

#### Profile Structure
```json
{
  "name": "production",
  "resourceUrl": "https://prod.azuresre.ai",
  "authRequired": true,
  "createdAt": "2024-08-26T10:30:00Z"
}
```

#### File System Organization
```
.sreagent-profiles/
├── local-dev.json           # Local development profile
├── staging.json             # Staging environment profile
└── production.json          # Production environment profile
.sreagent-current-profile    # Text file containing active profile name
```

#### Safety Features
- **Active Profile Protection**: Cannot delete currently active profile
- **Validation**: Profile names and URLs validated before creation
- **Error Handling**: Comprehensive error handling for missing profiles
- **Backup**: Current profile context preserved during switches

## Document Management Commands

### Overview
The document management commands provide comprehensive document handling capabilities for the SRE Agent's knowledge base. These commands integrate with the AgentMemory API to enable document upload, search, and indexing operations.

### Command Categories
- **Upload**: Add documents and folders to the agent's knowledge base
- **Search**: Query indexed documents for relevant information  
- **Reindex**: Rebuild the document index for improved search performance

### Implementation Architecture

#### File Structure
```
Commands/
├── DocumentCommandOptions.cs    # Document command option definitions
├── DocumentCommandHandlers.cs   # Document command implementations
└── CommandBuilder.cs           # Registration of document commands
Services/
└── ApiService.cs               # API integration for document operations
```

#### Design Patterns
- **Validation First**: All inputs are validated before API calls
- **Recursive Operations**: Support for folder-based operations with recursive discovery
- **Error Handling**: Comprehensive error handling with user-friendly messages
- **Progress Feedback**: Real-time feedback for long-running operations

### Upload Command

**Purpose**: Upload individual files or entire folders to the agent's knowledge base

**API Integration**: POST `/api/v1/AgentMemory/upload` with multipart form data

**Features**:
- Single file upload with path validation
- Folder upload with recursive file discovery
- File filtering based on extensions and content
- Indexing control (immediate or deferred)
- Progress tracking and user feedback

**Implementation Details**:
```csharp
public static async Task HandleDocumentUpload(ParseResult parseResult)
{
    // 1. Extract and validate options
    // 2. Determine upload type (file vs folder)
    // 3. Collect files for upload
    // 4. Execute multipart upload via API
    // 5. Provide completion feedback
}
```

### Search Command

**Purpose**: Query the document knowledge base for relevant information

**API Integration**: GET `/api/v1/AgentMemory/documents` with query parameters

**Features**:
- Text-based semantic search
- Configurable result limits
- Structured result display
- No results handling

**Implementation Details**:
```csharp
public static async Task HandleDocumentSearch(ParseResult parseResult)
{
    // 1. Extract search query and options
    // 2. Execute search API call
    // 3. Format and display results
    // 4. Handle empty result sets
}
```

### Reindex Command

**Purpose**: Rebuild the document index for improved search performance

**API Integration**: POST `/api/v1/AgentMemory/rebuildIndex`

**Features**:
- Full index rebuild
- Progress indication
- Completion confirmation
- Error handling for reindex failures

**Implementation Details**:
```csharp
public static async Task HandleDocumentReindex(ParseResult parseResult)
{
    // 1. Initiate reindex operation
    // 2. Provide user feedback
    // 3. Confirm completion
    // 4. Handle any errors
}
```

### API Service Integration

The `ApiService.cs` class provides the following document-related methods:

```csharp
// Upload documents with multipart form data
public static async Task UploadDocumentsAsync(List<string> filePaths, bool index = true)

// Search documents with query and result limit
public static async Task<List<DocumentSearchResult>> SearchDocumentsAsync(string query, int k = 10)

// Trigger full document index rebuild
public static async Task ReindexDocumentsAsync()
```

### Agent and Tool Management API

The `ApiService.cs` class also provides comprehensive CRUD operations for agents and tools:

```csharp
// Agent Management
public static async Task ApplyAgentAsync(string agentName, string yamlContent)
public static async Task DeleteAgentAsync(string agentName)
public static async Task<List<AgentInfo>> ListAgentsAsync()

// Tool Management  
public static async Task ApplyToolAsync(string toolName, string yamlContent)
public static async Task DeleteToolAsync(string toolName)
public static async Task<List<ToolInfo>> ListExtendedToolsAsync()
```

**Delete Operations Features:**
- **Dependency Checking**: Validates that no other agents/tools depend on the item being deleted
- **Conflict Detection**: Returns HTTP 409 with detailed dependency information
- **Error Handling**: Comprehensive error messages for not found (404) and conflict (409) scenarios
- **Authentication**: Automatic Bearer token integration for remote servers
- **Validation**: Confirms existence before attempting deletion

**Example Delete Handler Pattern:**
```csharp
public static async Task HandleDeleteCommand(ParseResult parseResult)
{
    var name = parseResult.GetValue(DeleteNameOption);
    
    try 
    {
        await ApiService.DeleteAgentAsync(name);
        AnsiConsole.MarkupLine($"[green]✅ Agent '{name}' deleted successfully.[/]");
    }
    catch (HttpRequestException ex) when (ex.Message.Contains("409"))
    {
        // Handle dependency conflicts with detailed messaging
        AnsiConsole.MarkupLine($"[red]❌ Cannot delete agent '{name}': {conflictDetails}[/]");
    }
    catch (HttpRequestException ex) when (ex.Message.Contains("404"))
    {
        AnsiConsole.MarkupLine($"[red]❌ Agent '{name}' not found on the server.[/]");
    }
}
```

## Incident Handler Management

### Overview
The incident handler management functionality enables mapping YAML-based agents to incident filters, replacing traditional incident handlers with more flexible agent-based handling.

### Implementation Architecture
- **Command Options**: Defined in `IncidentHandlerCommandOptions.cs`
- **Command Handlers**: Implemented in `IncidentHandlerCommandHandlers.cs`
- **API Integration**: Extended `ApiService.cs` with incident filter and handler methods
- **Command Registration**: Added to `CommandBuilder.cs` under `incidenthandler` command

### Design Principles
- **Validation First**: Ensures both filter and agent exist before mapping
- **Clean Migration**: Automatically removes old handlers when mapping agents
- **User Feedback**: Clear progress indicators throughout the process
- **Error Recovery**: Comprehensive error handling with actionable messages

### Workflow
1. **Filter Retrieval**: Fetches the filter by name from the server
2. **Agent Validation**: Confirms the specified agent exists
3. **Filter Update**: Sets the `HandlingAgent` property on the filter
4. **Handler Cleanup**: Identifies and deletes traditional handlers
5. **Confirmation**: Provides clear success/failure feedback

### Commands Implemented
- **`srectl incidenthandler create`**: Creates new incident filters with comprehensive configuration options
- **`srectl incidenthandler map-agent`**: Maps YAML agents to incident filters
- **`srectl list incidenthandlers`**: Lists all incident handlers (also available as `srectl incidenthandler list`)

### API Methods Added
```csharp
// Create a new incident filter
public async Task<(bool success, string message)> CreateIncidentFilterAsync(JsonNode filter)

// Get all incident filters
public async Task<List<JsonNode>?> GetIncidentFiltersAsync()

// Update an incident filter
public async Task<bool> UpdateIncidentFilterAsync(string filterId, JsonNode filter)

// Get all incident handlers
public async Task<List<JsonNode>?> GetIncidentHandlersAsync()

// Delete an incident handler
public async Task<bool> DeleteIncidentHandlerAsync(string handlerId)
```

### HTTP Method Patterns
- **PUT**: Used for creating new resources (`CreateIncidentFilterAsync`)
- **POST**: Used for updating existing resources (`UpdateIncidentFilterAsync`)
- **GET**: Used for retrieving resources
- **DELETE**: Used for removing resources

### Command Integration
- Added comprehensive `create` command with 10+ configuration options
- Added to main `list` command for consistency with other resources
- Available both as `srectl list incidenthandlers` and `srectl incidenthandler list`
- Supports `--verbose` flag for detailed output including filter information
- Follows established patterns for authentication and error handling

### Field Name Mapping
The implementation handles JSON field name inconsistencies between different API endpoints:
- **Incident Handlers**: Use lowercase field names (`id`, `incidentFilterId`, `createdAt`, `updatedAt`)
- **Incident Filters**: Use PascalCase field names (`Id`, `Name`, `HandlingAgent`)
- **Mapping Logic**: Handles both cases gracefully for maximum compatibility
