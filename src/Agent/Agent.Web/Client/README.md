# SRE Agent - UX

## Workflow

(Super simplifying the primary/top-level "setup/running" docs for UX dev)

**Pre:** Follow steps to deploy3p resources with your alias as prefix -> duplicate appsettings.json to `appsettings.Development.json` and set "EnvPrefix" to said prefix

1. Make sure `docker` running -> run `./src/run-durable-emulator.ps1`
1. Go to `./src/Agent/Agent.Web/Client`
    1. Run `npm run watch` to run a vite server to host the Agent site and start incremental builds. (NOTE: HTTP requests won't work yet until you run the ASP.Net backend)
1. Open `./src/Agent/Agent.Web.sln` to open up Visual Studio
1. On the debug button on the top of VS (it looks like a "play" button), choose the `react` profile. Then click on it to build and run.
    1. A browser window will automatically open pointing to the vite server and you should be good to go.

### Portal (PaasServerless extension) entrypoint

1. Register your subscription (probably not needed post-BUILD?): `az feature register -n SREAgentPreview --namespace Microsoft.App`
1. Links:
    - [Local Paas SRE Agent Home/Browse](https://portal.azure.com/?Microsoft_Azure_PaasServerless_clientoptimizations=false&feature.customportal=false&feature.canmodifyextensions=true#view/Microsoft_Azure_PaasServerless/SreAgentHome.ReactView?testExtensions=%7B%22Microsoft_Azure_PaasServerless%22:%22https://localhost:1338/paasserverless%22%7D)
    - https://aka.ms/sreagent-portal
    - https://aka.ms/sreagent-local (local agent site in canary(?) Paas)

## Localization

Define messages in `SREAgentResources.ts`, with a `description` if warranted. `eslint` will scream at you about missing the `id` prop, which is a hash of the message content - either hover over it and quick-fix -> formatjs/enforce-id (or Fix all auto-fixable) or commit it (pre-commit will run eslint to auto-fix it)...or manually calculate the hash in your head if that's your jam.

### Standard / in-component usage

```typescript
const intl = useIntl();

// If formatting strings ("placeholders"), pass second arg as an object with the values
// to replace in the string (Ex: { numThings: 5 } for message "You have {numThings} things")
const myMessage = intl.formatMessage(SreAgentResources.myMessage);
```

OR

```typescript
// Provide formatted values (if any) to the `values` prop
<FormattedMessage {...SreAgentResources.myMessage} />
```

### Utils usage

Swap the `useIntl` hook for `getIntl()`, then use the same way. This is how utils tap into what the hook consumes

## Pre-commit hook

The hook gets picked up even if changes are only made outside of this folder, so there's a specific check to make sure there's staged files within it before running

- lint-staged
    - `formatjs extract` string resources so they can be translated
    - `eslint` - longer step due to plugins; could explore removing, but this step is responsible for auto-ID'ng the defined loc strings
    - `prettier` - standard, and probably the main/most-important code formatter; super-quick
- (Currently) `formatjs compile` latest translated resources (same place as extracted to) so `react-intl` can actually consume them
