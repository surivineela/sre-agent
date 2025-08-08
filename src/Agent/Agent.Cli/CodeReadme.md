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
│   ├── GeneralCommandHandlers.cs # General command implementations (validate-all)
│   ├── AgentCommandOptions.cs   # Agent command option definitions
│   └── ToolCommandOptions.cs    # Tool command option definitions
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
1. Define options class in `Commands/` folder
2. Implement handler in appropriate handler class
3. Register command in `CommandBuilder.cs`
4. Add validation logic if needed
5. Create tests for new functionality

**Example: Tool Discovery Commands**

The `srectl tool show-types` and `srectl tool show-connectors` commands demonstrate the model-driven approach:

1. **ToolDefinitionService**: Discovers types using reflection and attributes
2. **Command Options**: Defined in `ToolCommandOptions.cs` with verbose and type filtering
3. **Command Handlers**: Implemented in `ToolCommandHandlers.cs` with proper error handling
4. **Integration**: Uses actual framework models for consistency

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

## Thread Management Commands

### Enhanced Streaming Experience

The thread commands now provide a real-time streaming experience:

- **Default Wait Behavior**: Commands wait for agent responses by default
- **Streaming Messages**: Messages appear as soon as they arrive from the server
- **Smart Completion Detection**: Automatically detects when the agent stops responding
- **Override Options**: Use `--no-wait` to disable waiting behavior

### Thread Command Options

- `--wait`: Wait for agent response (default: true)
- `--no-wait`: Don't wait for agent response (overrides default)
- `--message`: Message to send
- `--user-id`: User ID (defaults to current user)
- `--display-name`: Display name (defaults to current user)
- `--thread-id`: Specific thread ID to use

### Implementation Details

The streaming implementation (`GetThreadMessagesStreamingAsync`) uses the following approach:

1. **Real-time Display**: Shows messages immediately as they arrive
2. **Smart Polling**: Continues polling until agent stops responding
3. **Completion Detection**: Monitors for periods of inactivity after agent response
4. **User Feedback**: Provides clear waiting indicators and completion messages

### Completion Logic

The system determines when an agent has finished responding by:
- Waiting for initial agent response
- Monitoring for new messages after agent starts responding
- Stopping when no new messages arrive for 3 consecutive polling attempts (6 seconds by default)
- Providing timeout protection with maximum retry limits

## Thread Track Command

The `track` command allows you to monitor an existing thread for new messages in real-time:

```bash
srectl thread track --thread-id <thread-id>
```

**Parameters:**
- `--thread-id`: The ID of the thread to track (required)

**What it does:**
- Displays all existing messages in the thread
- Continuously monitors for new messages
- Shows incoming messages as they arrive
- Automatically stops when the conversation becomes idle
- Updates thread last-used timestamp

**Usage Examples:**

```bash
# Track a specific thread
srectl thread track --thread-id abc123-def456-ghi789

# Track using thread ID from previous commands
srectl thread track --thread-id $(srectl thread list | grep "→" | awk '{print $2}')
```

**Features:**
- **Real-time Monitoring**: Shows new messages as they arrive
- **Historical Context**: Displays all existing messages first
- **Smart Completion**: Automatically detects when the conversation ends
- **Interrupt Support**: Use Ctrl+C to stop tracking at any time
- **Thread Management**: Updates thread last-used for easy continuation

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
