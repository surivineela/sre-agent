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

- Follow .NET best practices for CLI tools.
- Ensure all commands have clear help text and comprehensive error handling.
- Write unit tests and E2E tests for command logic and utilities:
  - Focus on testing core functionality
  - Avoid adding trivial tests like null checks
  - Add sufficient comments to test code for readability
  - Avoid making assumptions about test output

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


## Building & Testing

To build the CLI, run:
```pwsh
dotnet build src/Agent/Agent.Cli/Agent.Cli.csproj --no-restore
```

To run the CLI:
```pwsh
dotnet run --project src/Agent/Agent.Cli/Agent.Cli.csproj --no-restore
```

Run unit tests for the CLI only (avoid full solution test runs for speed):
```pwsh
dotnet test src/Agent/Agent.Cli.UnitTests/Agent.Cli.UnitTests.csproj --no-restore
```
