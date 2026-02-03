@AGENTS.md

## Build Tips

- **Stale compressed assets**: If `dotnet publish` for Agent.Web fails with errors like `The asset '...compressed/publish/...' can not be found`, delete `src/Agent/Agent.Web/obj/Release/net9.0/compressed` (or `obj/Release` entirely) and retry.

## Localization

- When adding new localized strings, always include `id=''` in the message descriptor. Do not auto-generate or omit the `id` field.
- After adding strings, run from the Client directory to generate IDs: `npx eslint "src/**/*.{ts,tsx}" --fix`
