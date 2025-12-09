---
applyTo: "src/Agent/Agent.Cli*/**/*"
---

# Instructions for src/Agent/Agent.Cli
## Overview

This directory contains the source code for the SRE Agent CLI, providing command-line tools for interacting with the SRE Agent platform.

## Key Projects & Structure

### Agent.Cli

The main CLI project.

```
src/Agent/Agent.Cli
├── Commands/
│   ├── [Command]CommandHandlers.cs         # Handlers for each CLI command
│   ├── [Command]CommandOptions.cs          # Options definitions for each CLI command
│   ├── CommandBuilder.cs                   # Builds the command structure, this is partial class contains the main logic for RootCommand
│   ├── CommandBuilder.[Command].cs         # Builds the command structure, this is partial class contains individual command builders
│   ├── CommandExamples.cs                  # Content of --help examples
│   └── GlobalOptions.cs                    # Global options definitions for the CLI (e.g., --debug, --quiet)
├── Converters/                             # Converters for model classes
├── Helpers/                                # Helper classes and utilities
├── Models/                                 # Data models used by the CLI
└── Services/                               # Services for business logic and external interactions
```

### Agent.Cli.E2ETests

The End-to-End Tests project for the CLI.

```
src/Agent/Agent.Cli.E2ETests
├── Tool/                                        # E2E tests for Tool commands
│   ├── [Command]CommandTests.cs                 # Core functionality E2E tests for each command
│   └── [Command]CommandInvalidParameterTests.cs # Invalid parameter E2E tests for each command
├── Helpers/                                     # Test helper utilities
├── CliTestRunner.cs                             # CLI test runner
```

### Agent.Cli.UnitTests

The Unit Tests project for the CLI.

## Development Guidelines

- Follow .NET best practices for CLI tools
- Ensure the individual components are following the guidelines below
- DO NOT create readme file unless you are explicitly instructed to do so
- Write unit tests and E2E tests for command logic and utilities:
  - Focus on testing core functionality
  - Avoid adding trivial tests like null checks
  - Add sufficient comments to test code for readability
  - Avoid making assumptions about test output
- When modifying existing code, gradually refactor to align with these guidelines rather than performing large-scale refactoring in a single change
- When testing CLI commands manually, `cd` to `TestPlayground` folder first and use relative paths from there.

### CommandBuilder.cs and CommandBuilder.[Command].cs Guidelines

The `CommandBuilder.cs` and `CommandBuilder.[Command].cs` files define the CLI command structure and delegate execution to handler classes. Each command group (e.g., Tool, Agent, Thread) has its own partial class file.

#### Rules

- Keep CommandBuilder focused on structure only - no business logic
- Define options in separate `[Command]CommandOptions` classes with nested subcommand classes
- Use `CommandExamples` for all command descriptions and help text
- Add validators for mutually exclusive options or required combinations
- Every command needs corresponding handlers and tests
- Use collection initializer syntax for adding subcommands to parent commands

#### Key Patterns

**CommandBuilder.cs** - Main partial class containing:
- `public static RootCommand BuildCommands()` - Entry point that constructs the complete command tree

**CommandBuilder.[Command].cs** - Partial class for each command group containing:
- `private static class [Command]Command` - Nested class encapsulating command structure
- `public static Command Build()` - Builds the command with all subcommands using collection initializer
- `private static Command Create[SubCommand]Command()` - Individual factory methods for each subcommand

**Command Group Structure:**
Each command group follows this pattern:
```csharp
private static class [Command]Command
{
    public static Command Build()
    {
        var [command] = new Command("[command]", "Description")
        {
            CreateSubCommand1(),
            CreateSubCommand2(),
            CreateSubCommand3()
        };

        return [command];
    }

    private static Command CreateSubCommand1()
    {
        var cmd = new Command("subcommand1", CommandExamples.[Command].SubCommand1Description)
        {
            [Command]CommandOptions.SubCommand1.Option1,
            [Command]CommandOptions.SubCommand1.Option2
        };

        cmd.SetAction([Command]CommandHandlers.HandleSubCommand1);
        return cmd;
    }
}
```

**Parameter Validation:**
Add validators directly to commands for input validation:
```csharp
cmd.AddValidator(result =>
{
    var option1 = result.GetValue(SomeOption);
    var option2 = result.GetValue(AnotherOption);
    if (/* conflict condition */)
        result.AddError(ErrorMessageHelper.InvalidParameter("Error message"));
});
```

### [Command]CommandHandlers.cs Guidelines

The `[Command]CommandHandlers.cs` files contain the business logic for CLI commands. Each command group has its own handler class with methods that execute the actual command operations.

#### Rules

- Keep handlers focused on business logic only - no command structure definitions
- All handler methods are `public static` and follow consistent naming convention
- Use `Task<int> HandleListCommand(ParseResult parseResult, CancellationToken cancellationToken = default)` signature for all handlers
- **Never use `Environment.Exit()`** - always return error codes and let the framework handle process exit
- **Avoid try-catch blocks** unless there's a specific reason - let the framework handle unhandled exceptions
- Use `ConsoleUI` for all user-facing output and `DebugLogger` for debug-only output

#### Key Patterns

**Handler Method Names:**
- Format: `Handle[SubCommand]Command`, e.g., `HandleCreateCommand` for `create` subcommand
- Use PascalCase for multi-word subcommands: `HandleShowTypesCommand` for `show-types` subcommand

**Method Signature Pattern:**
```csharp
public static async Task<int> Handle[SubCommand]Command(ParseResult parseResult, CancellationToken cancellationToken = default)
{
    DebugLogger.Debug("Command", "Starting [command] [subcommand] command");
    
    // Get options from parseResult
    var option1 = parseResult.GetValue([Command]CommandOptions.[SubCommand].Option1);
    var option2 = parseResult.GetValue([Command]CommandOptions.[SubCommand].Option2);
    
    DebugLogger.Debug("Parameters", $"Option1: {option1}, Option2: {option2}");
    
    // Execute command logic
    // ...
    
    return success ? 0 : 1;
}
```

### [Command]CommandOptions.cs Guidelines

The `[Command]CommandOptions.cs` files define all command-line options for commands. 

#### Rules

- Keep CommandOptions focused on structure only - no business logic
- Use nested static classes to organize options by subcommand
- **Do NOT add validators to options** - validators belong in `CommandBuilder.cs`
- Keep descriptions **clean, correct, and short**:
  - **Clean**: Use simple, direct language without jargon
  - **Correct**: Accurately describe what the option does
  - **Short**: Aim for one concise sentence (under 80 characters when possible)

#### Key Patterns

```csharp
/// <summary>
/// Builds command-line options for [command] commands.
/// </summary>
public static class [Command]CommandOptions
{
    // ============================================================
    // [Command] [SubCommand] Command Options
    // ============================================================

    public static class [SubCommand]
    {
        public static readonly Option<string> [OptionName]Option = new("--option-name")
        {
            Description = "Brief description of what this option does",
            Required = true
        };
    }
}
```

**Class Names:**
- Top-level: `[Command]CommandOptions` (e.g., `ToolCommandOptions`, `AgentCommandOptions`)
- Nested classes: Match subcommand name (e.g., `Create`, `Validate`, `Apply`, `Delete`)
- Use PascalCase for multi-word subcommands (e.g., `ShowTypes` for `show-types` subcommand)

**Option Property Names:**
- End with `Option` suffix (e.g., `NameOption`, `DryRunOption`)
- Use descriptive names that indicate the option's purpose
- Avoid redundant prefixes that repeat the subcommand name
  - ✅ Good: `ToolCommandOptions.Create.NameOption`
  - ❌ Bad: `ToolCommandOptions.CreateNameOption` (old pattern - avoid)

### CommandExamples.cs Guidelines

The `CommandExamples.cs` file provides all help text and usage examples for CLI commands. This centralizes help content for consistency and maintainability.

#### Key Patterns

Organized as nested static classes matching command hierarchy:
```csharp
public static class [Command]
{
    public const string [SubCommand]Description = @"Brief one-line description

Examples:
  # Comment explaining this example
  srectl [command] [subcommand] --option value
  
  # More complex example with explanation
  srectl [command] [subcommand] --option1 value1 --option2 value2";
}
```

#### Content Guidelines

**Help Text Quality:**
- Keep descriptions clean, correct, and simple
- First line: Brief command purpose (what it does)
- Examples section: Show real-world usage patterns
- Use comments (`#`) to explain each example
- Progress from simple to complex examples

**Writing Examples:**
- Start with the most common use case
- Show variations with different options
- Include real parameter values (not placeholders when possible)
- Use multi-line format (`\`) for complex commands
- Group related examples together

### Console Output Guidelines

The CLI uses two specialized helper classes for consistent, portable console output across different terminals:

#### Helpers/ConsoleUI.cs

- `WithColor(ConsoleColor color, Action body)` - Executes an action with specified console color
- `DrawPanel(string title, string content, ConsoleColor titleColor)` - Draw a panel with title and content
- `DrawLine(int length, ConsoleColor color)` - Draw a simple border line
- `Progress(double percentage, string label, int width)` - Show a progress bar with precise fractional display
- `WriteStatus(bool success, string message, ConsoleColor? color)` - Write a status message with appropriate symbol
- `WriteInfo(string message, ConsoleColor color)` - Write an info message with bullet point
- `WriteExamples((string Comment, string Command)[] examples, int indent)` - Renders an "Examples:" block with consistent spacing and colors
- `WriteSubcommand(string name, string description, (string Comment, string Command)[]? examples, int nameWidth)` - One-shot renderer for a subcommand row + optional examples
- `WriteBullet(string message, ConsoleColor color, int indent)` - Write a bullet point for lists
- `WriteTreeItem(string message, bool isLast, int level, ConsoleColor color)` - Write a tree-style hierarchical item
- `Write(string message, ConsoleColor? color)` - Write plain text with optional color
- `WriteInline(string message, ConsoleColor? color)` - Write text without newline
- `GetSpinnerFrame(int frameIndex)` - Spinner animation frame (ASCII-safe)
- `WriteSection(string title, ConsoleColor color, bool topMargin, bool bottomMargin)` - Section header with underline
- `WriteCommand(string description, string command, ConsoleColor descColor, ConsoleColor cmdColor)` - Show a command example with proper formatting
- `WriteKeyValue(string key, string value, int keyWidth, ConsoleColor keyColor, ConsoleColor valueColor)` - Show key-value pairs in a structured format
- `Confirm(string message, bool defaultYes)` - Yes/No prompt
- `WriteTimestamp(DateTime timestamp, ConsoleColor color)` - Timestamp writer
- `WriteDuration(TimeSpan duration, string operation, ConsoleColor color)` - Duration writer
- `WriteCommandGroup(string groupName, (string name, string description)[] commands)` - Group of commands with consistent spacing
- `CaptureOutput(Action outputAction)` - Capture ConsoleUI output to a string (for list commands)

#### Helpers/DebugLogger.cs

- `Debug(string message)` - General debug messages
- `Debug(string category, string message)` - Debug messages with category
- `LogHttpRequest/LogHttpResponse` - HTTP traffic logging

## Building & Testing

To build the CLI, run:
```pwsh
dotnet build src/Agent/Agent.Cli/Agent.Cli.csproj --no-restore
```

To run the CLI:
```pwsh
cd TestPlayground
dotnet run --project ../src/Agent/Agent.Cli/Agent.Cli.csproj --no-restore
```

Run unit tests for the CLI only (avoid full solution test runs for speed):
```pwsh
dotnet test src/Agent/Agent.Cli.UnitTests/Agent.Cli.UnitTests.csproj --no-restore
```
