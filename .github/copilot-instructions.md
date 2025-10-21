# GitHub Copilot Instructions

## Building, Running, and Testing

Save time and use --no-restore when running `dotnet build`, `dotnet test` and `dotnet run` to avoid restoring packages, as the project is usually set up with the necessary dependencies.

When running tests, specify a target test project rather than running all tests in the solution as the full test suite is slow. Prefer to use the --filter parameter to limit the run to a specific test class or method.

## UX Development

### General

- Use Fluent V9 components in place of raw HTML where possible
- Use Fluent V9 design/styling tokens
- Separate large components into their own files
- Write unit tests for pure-logic utilities
- Reuse or even add to various components, contracts, utilities, etc. under the "Common" folder
- **Do not use barrel exports (index.ts files)** - import directly from specific files

### Agent.Web/Client

- Localize strings following existing usages (SREAgentResources.ts -> intl.formatMessage(SREAgentResources.someString)))

### Agent.Portal/Client

- When working within Agent.Portal/Client, reference `src/Agent/Agent.Portal/Client/AgentContext.md` for architectural guidelines and best practices
- Localize strings following existing usages (PortalResources.ts -> intl.formatMessage(PortalResources.someString)))
