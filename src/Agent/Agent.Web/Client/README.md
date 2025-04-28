# SRE Agent - UX

## Workflow

(Super simplifying the primary/top-level "setup/running" docs for UX dev)

**Pre:** Follow steps to deploy3p resources with your alias as prefix -> duplicate appsettings.json to `appsettings.Development.json` and set "EnvPrefix" to said prefix

1. Make sure `docker` running -> run `./src/run-durable-emulator.ps1`
1. Build and run `Agent.Web.sln` in Visual Studio
    1. A browser window will automatically open to the ASP.NET site that contains our static React app
1. Run `npm run dev` or `watch` in `./src/Agent/Agent.Web/Client`
    1. Add `/static` to the URL

## Localization

### Standard / in-component

```typescript
const intl = useIntl();

// `id` will be auto-populated by eslint in pre-commit
const myMessage = intl.formatMessage({
    defaultMessage: 'My message default value',
    // Optionally, `description` if warranted
});
```

### Utils

Swap the `useIntl` hook for `getIntl()`, then use the same way. This is how utils tap into what the hook consumes

## Pre-commit hook

The hook gets picked up even if changes are only made outside of this folder, so there's a specific check to make sure there's staged files within it before running

- lint-staged
    - `formatjs extract` string resources so they can be translated
    - `eslint` - longer step due to plugins; could explore removing, but this step is responsible for auto-ID'ng the defined loc strings
    - `prettier` - standard, and probably the main/most-important code formatter; super-quick
- (Currently) `formatjs compile` latest translated resources (same place as extracted to) so `react-intl` can actually consume them
