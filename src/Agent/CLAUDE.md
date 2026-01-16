# SRE Agent Runtime - Code Guidelines for Claude

This file contains coding standards derived from this repository's patterns, existing guidelines, and analysis of 500+ historical commits. Claude Code should follow these guidelines when reviewing or modifying code.

## Quick Reference - Required Reading

Before writing or reviewing code, reference these files:
- `.github/copilot-instructions.md` - Build, test, and UX guidelines
- `.github/agents/code-guidelines.md` - Test patterns and serialization
- `.github/agents/UXCoding.agent.md` - React/TypeScript patterns
- `Agent.Framework/CONTRIBUTING.md` - Framework vs business logic boundaries

## Repository Architecture

```
src/Agent/
├── Agent.Cli/              # CLI tool (srectl) - System.CommandLine based
├── Agent.Web/              # Web API + Portal (ASP.NET Core)
│   └── Client/             # React/TypeScript frontend
├── Agent.Portal/           # Standalone portal
│   └── Client/             # React/TypeScript frontend
├── Agent.Core/             # Domain logic, services, interfaces
│   ├── Interfaces/         # Service contracts
│   ├── Services/           # Implementations
│   ├── Models/             # DTOs and entities
│   ├── Exceptions/         # Custom exception hierarchy
│   ├── Helpers/            # Utility classes
│   └── Extensions/         # Extension methods
├── Agent.Framework/        # Reusable agent framework (DOMAIN-AGNOSTIC)
├── Agent.Runtime/          # Execution runtime, agents, skills, prompts
├── Agent.Data/             # Cosmos DB data access
├── Agent.Plugins/          # External integrations (ICM, Kusto, Azure, etc.)
├── Agent.Common/           # Shared API models
├── Agent.Logging/          # Application Insights integration
├── Agent.Prometheus/       # Metrics
└── Session.Proxy/Identity/ # Session management
```

---

## C# Backend Guidelines

### 1. Single-File App Compatibility (Critical)

When publishing as self-contained single-file executables, `Assembly.Location` returns empty string.

```csharp
// WRONG - Causes IL3000 error in single-file apps
var dir = Path.GetDirectoryName(typeof(MyClass).Assembly.Location);

// CORRECT - Works in all deployment scenarios
var dir = AppContext.BaseDirectory;
```

### 2. Null Reference Prevention

**Constructor Validation (Required)**
```csharp
public class AgentHelperService
{
    private readonly IAuthenticationService _authService;
    private readonly ILogger<AgentHelperService> _logger;

    public AgentHelperService(
        IAuthenticationService authService,
        ILogger<AgentHelperService> logger)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
}
```

**Use Extension Methods for Argument Validation**
```csharp
// From ArgumentValidation.cs
public static string ThrowIfNullOrEmpty(
    [NotNull] this string? argument,
    [CallerArgumentExpression(nameof(argument))] string? paramName = null)
{
    if (string.IsNullOrEmpty(argument))
        throw new ArgumentException("The argument is null or empty.", paramName);
    return argument;
}

// Usage
var name = request.Name.ThrowIfNullOrEmpty();
```

**Null-Safe Access**
```csharp
var name = user?.Profile?.Name ?? "Unknown";

// Guard clauses
if (user?.Profile is null) return;
```

### 3. JSON Serialization

**Use WebJsonSerializer** (from code-guidelines.md)
```csharp
// WRONG
var json = JsonSerializer.Serialize(obj);

// CORRECT
var json = WebJsonSerializer.Serialize(obj);
```

**Polymorphic Types**
```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(KustoTool), "kustoTool")]
[JsonDerivedType(typeof(AzureTool), "azureTool")]
public abstract class ToolBase { }
```

### 4. Async/Await Patterns

```csharp
// WRONG - Causes deadlocks
var result = GetDataAsync().Result;
var result = GetDataAsync().GetAwaiter().GetResult();

// CORRECT - Async all the way
var result = await GetDataAsync();

// Library code should use ConfigureAwait(false)
var data = await httpClient.GetAsync(url).ConfigureAwait(false);
```

### 5. Authentication & Security

**Never Log Tokens**
```csharp
// WRONG - Security vulnerability
_logger.LogDebug("Token: {Token}", accessToken);

// CORRECT - Log only non-sensitive info
_logger.LogDebug("Token acquired for scope: {Scope}", scope);
```

**Use Token Scope Constants** (from Constants.cs)
```csharp
public const string ArmOboTokenScope = "https://management.core.windows.net/.default";
public const string GraphTokenScope = "https://graph.microsoft.com/.default";
public const string KustoTokenScope = "https://kusto.kusto.windows.net/.default";
```

### 6. Error Handling

**Use Custom Exceptions** (from Agent.Core/Exceptions/)
```csharp
// Use specific exception types
throw new ToolExecutionException("Tool failed to execute");
throw new ToolExecutionUnauthorizedException("Unauthorized access");

// Never swallow exceptions silently
catch (Exception ex)
{
    _logger.LogError(ex, "Operation failed for {ResourceId}", resourceId);
    throw; // or handle appropriately
}
```

### 7. Dependency Injection

```csharp
// Transient for stateless services
services.AddTransient<IMyService, MyService>();

// Scoped for request-based services
services.AddScoped<IUserContext, UserContext>();

// Singleton for shared state (ensure thread safety)
services.AddSingleton<ICacheService, CacheService>();
```

### 8. Validation

**Use ValidationHelper Pattern**
```csharp
var (isValid, errorMessage) = ValidationHelper.ValidateResourceName(request.Name);
if (!isValid)
{
    return BadRequest(errorMessage);
}
```

**Generated Regex for Performance**
```csharp
[GeneratedRegex("^[a-zA-Z0-9_-]+$")]
private static partial Regex ValidNameRegex();
```

### 9. Code Style (from .editorconfig)

```csharp
// File-scoped namespaces
namespace Agent.Core.Services;

// Use var for all declarations
var client = new HttpClient();
var items = await GetItemsAsync();

// Private fields with underscore prefix
private readonly ILogger _logger;
private int _count;

// Async suffix for async methods
public async Task<Result> GetDataAsync()

// Public sealed classes and records preferred
public sealed class MyService { }
public sealed record AgentConfig(string Name, string[] Tools);
```

---

## Testing Guidelines (from code-guidelines.md)

### Key Principles

1. **No Mocking** - Use of Moq is prohibited. Connect to real services.
2. **Use WebJsonSerializer** - Not raw JsonSerializer
3. **Reference Agent.Web** - Tests use its appsettings file
4. **Sealed Classes/Records** - Use where possible

### Test Structure

```csharp
[Fact]
[Trait("Category", "Agent")]
[Trait("Command", "Apply")]
public async Task AgentApply_ValidAgent_Succeeds()
{
    // Arrange
    var agentName = "test-agent";
    var agentYaml = TestYamlHelper.GetMinimalAgentV2(agentName);
    Runner.CreateFile($"agents/{agentName}.yaml", agentYaml);

    // Act
    var result = await Runner.RunAsync("agent", "apply", "--name", agentName);

    // Assert
    Assert.True(result.Success, $"Command failed: {result.Output}");
    Assert.Contains("applied successfully", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
}
```

### Running Tests

```bash
# Specify target project (full suite is slow)
dotnet test src/Agent/Agent.Cli.UnitTests --no-restore

# Filter to specific test
dotnet test --filter "FullyQualifiedName~AgentApply" --no-restore
```

---

## UX/Frontend Guidelines (from copilot-instructions.md & UXCoding.agent.md)

### General Rules

- **Fluent V9 Components** over raw HTML
- **No Barrel Exports** - Never create index.ts files
- **Direct Imports** - Import from specific files
- **Arrow Functions Only** - No function declarations

### Component Pattern

```typescript
import { makeStyles, tokens } from "@fluentui/react-components";
import { useIntl } from "react-intl";
import { SREAgentResources } from "../../Strings/SREAgentResources";

const useStyles = makeStyles({
  root: {
    display: "flex",
    gap: tokens.spacingHorizontalM,
    backgroundColor: tokens.colorNeutralBackground1,
  },
});

interface MyComponentProps {
  title: string;
  onAction?: () => void;
}

export const MyComponent: FC<MyComponentProps> = ({ title, onAction }) => {
  const styles = useStyles();
  const intl = useIntl();

  return (
    <div className={styles.root}>
      <Text>{intl.formatMessage(SREAgentResources.myLabel)}</Text>
    </div>
  );
};
```

### Localization

```typescript
// Agent.Web/Client uses SREAgentResources
import { SREAgentResources } from "../../Strings/SREAgentResources";

// Agent.Portal/Client uses PortalResources
import { PortalResources } from "../../Strings/Resources";

const intl = useIntl();
<Text>{intl.formatMessage(SREAgentResources.myString)}</Text>
```

### Telemetry (Not console.log)

```typescript
// Agent.Portal - useTelemetry hook
const { logEvent, logError } = useTelemetry(TelemetrySource.MyView);
logEvent("LoadingData", { context: "initialization" });

// Agent.Web - AzPortalProxy
const { log, logAmplitudeControlEvent } = useAzPortalContext();
```

### Error Handling in Components

```typescript
// Don't wrap in try/catch - use isSuccessful pattern
const response = await client.getData();
if (!response.isSuccessful) {
  logError("DataLoadFailed", response.error);
  return;
}
// Use response.data
```

### Accessibility

```typescript
// Dynamic content announcements
<div role="alert" aria-live="assertive">{errorMessage}</div>

// Decorative icons
<Icon aria-hidden="true" />

// Icon-only buttons
<Button icon={<DeleteIcon />} aria-label={intl.formatMessage(Resources.delete)} />
```

---

## Framework Guidelines (from CONTRIBUTING.md)

### What Belongs in Agent.Framework

- Agent lifecycle management
- Tool integration and management
- Prompt handling
- Context management
- Handoff mechanisms
- Generic utilities

### What Does NOT Belong

- Domain-specific business logic
- Project-specific features
- Custom tool implementations
- Environment-specific configuration

### Before Adding to Framework

1. Is this truly a framework enhancement?
2. Could it be implemented in the consuming project?
3. Does it remain domain-agnostic?
4. Will it benefit other framework users?

---

## AI Code Cleanup (from CodeCompletionCheck.md)

After AI-assisted coding, remove:

1. **Extra Comments** - Inconsistent with rest of file style
2. **Defensive Checks** - Abnormal try/catch blocks for trusted codepaths
3. **Type Bypasses** - Casts to `any` to work around type issues
4. **Single-Use Variables** - Inline variables used only once after declaration

---

## Common Issue Patterns (from 500+ commit analysis)

| Category | Frequency | What to Check |
|----------|-----------|---------------|
| UI/UX | 44% | Accessibility, loading states, null states, localization |
| Build/Publish | 11% | Single-file compat, NuGet warnings, missing dependencies |
| Serialization | 3% | Polymorphic JSON, type converters, WebJsonSerializer |
| Authentication | 2% | Token refresh, scope validation, OBO flow |
| Null Reference | 1% | Constructor validation, null-safe access |

---

## Code Review Checklist

### C# Backend
- [ ] No `Assembly.Location` usage (use `AppContext.BaseDirectory`)
- [ ] Constructor parameters validated for null
- [ ] No `.Result` or `.GetAwaiter().GetResult()` blocking calls
- [ ] No credentials/tokens in logs
- [ ] Custom exceptions used (not generic `Exception`)
- [ ] WebJsonSerializer used (not raw JsonSerializer)
- [ ] Async methods have `Async` suffix
- [ ] Public APIs have XML documentation

### Testing
- [ ] No Moq usage - real connections only
- [ ] Tests cover success AND error paths
- [ ] `--no-restore` used in test commands

### UX/Frontend
- [ ] Fluent V9 components (no raw HTML)
- [ ] No barrel exports (index.ts)
- [ ] Localization via SREAgentResources/PortalResources
- [ ] Telemetry (no console.log)
- [ ] Accessibility attributes where needed
- [ ] Loading and empty states handled

### Framework Changes
- [ ] Remains domain-agnostic
- [ ] Could benefit other framework users
- [ ] Has proper extension points
