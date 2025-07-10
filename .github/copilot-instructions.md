# GitHub Copilot Instructions

## Building, Running, and Testing

Save time and use --no-restore when running `dotnet build`, `dotnet test` and `dotnet run` to avoid restoring packages, as the project is usually set up with the necessary dependencies.

When running tests, specify a target test project rather than running all tests in the solution as the full test suite is slow. Prefer to use the --filter parameter to limit the run to a specific test class or method.

## UX Development (Agent.Web/Client)

- Use Fluent V9 components in place of raw HTML where possible
- Use Fluent V9 design/styling tokens
- Separate large components into their own files
- Write unit tests for pure-logic utilities
- Reuse or even add to various components, contracts, utilities, etc. under the "Common" folder
- Localize strings following existing usages (SREAgentResources.ts -> intl.formatMessage(SREAgentResources.someString)))
