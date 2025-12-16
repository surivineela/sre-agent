# Agent Portal Client Context

> ⚠️ **Authoring reminder:** future updates must stay extremely concise and capture only the most critical context needed for agents to reason about this project. Prefer links to verbose docs elsewhere.

## Overview

- **Project**: `Agent.Portal/Client` – Vite/React SPA parallel to `Agent.Web/Client`, delivered as a static build output.
- **Purpose**: Power the SRE Agent portal experience with Fluent UI v9 components, shared localization patterns, and common utilities.
- **Tech Stack**: React 18, Vite, Fluent UI v9, React Router v7, React Intl, MSAL, Vitest

## Project Structure

```plaintext
src/
├── Views/              # Page-level components and routes
│   ├── Home/          # Agent browse/list view + Create wizard
│   ├── Agent/         # IFrame view for embedding agent UX
│   ├── Navbar/        # Top navigation (feedback, notifications, settings, auth)
│   ├── Notifications/ # Toast container and drawer UI
│   └── LandingPage/   # Signed-out welcome page
├── Common/
│   ├── Auth/          # MSAL config and cloud endpoints
│   ├── Clients/       # API clients (ARM, Graph, SreAgent, Subscriptions, etc.)
│   ├── Components/    # Reusable UI (Wizard, pickers, dropdowns, etc.)
│   ├── Contexts/      # React contexts (Auth, Notifications, UserPreferences, Subscriptions)
│   ├── Hooks/         # Custom hooks (telemetry, localStorage, auth tokens, etc.)
│   ├── Contracts/     # TypeScript interfaces (ARM, SreAgent, Telemetry, etc.)
│   ├── Constants/     # API versions, telemetry sources, links
│   └── Utilities/     # Pure functions (ARM parsing, GUID, JWT, sanitization, etc.)
└── Strings/           # Localization (Resources.ts, extracted/, compiled/, Intl/)
```

## Architecture & Patterns

### Static Hosting

- This portal is served as **static HTML/JS** with no server-side rendering (SSR).
- All hooks and utilities assume `window` is always available at runtime (although are likely wrapped in try/catch blocks)

### Telemetry

- **`useTelemetry(source, resourceId?)`** – Hook for logging events with automatic sanitization and console output.
- **`logTelemetryEvent(event)`** – Non-hook version for use outside React components (classes, utilities).
- Use telemetry **instead of `console.log`/`console.error`** for all application logging, API requests, and error tracking.
- Telemetry sources are defined in `TelemetrySource` enum; add new sources as needed for major features/areas.
- **Pass telemetry source from caller context** - hooks accept `telemetrySource` parameter representing where they're used (e.g., `HomeBrowseView`), not what they are. This enables feature-level visibility.

### Amplitude Telemetry (Structured Events)

For richer analytics with structured event types, use the Amplitude telemetry system:

- **`useAmplitudeTelemetry()`** – Hook providing `logControlEvent`, `logNavigationEvent`, `logOperationEvent`
- **`AmplitudeContextProvider`** – Wrap views to provide automatic resource metadata enrichment
- **`logAmplitudeEvent()`** – Non-hook version for use outside React components

**Event Types:**
- **Control Events** – User interactions (button clicks, dropdown changes, toggles)
- **Navigation Events** – Page/tab navigation, blade opens, external links
- **Operation Events** – Backend operations (create, update, delete, load) with optional error info

**Usage:**
```tsx
// Wrap your view with the context provider
<AmplitudeContextProvider
    resourceId={resourceId}
    telemetrySource={TelemetrySource.MyView}
>
    <MyComponent />
</AmplitudeContextProvider>

// In components, use the hook
const { logControlEvent, logNavigationEvent, logOperationEvent } = useAmplitudeTelemetry();

logControlEvent({
    targetType: 'button',
    targetAction: 'clicked',
    targetName: 'createAgentButton',
    targetFriendlyName: 'Create Agent',
    valueObjectName: SpecialControlValue.SubmitForm,
    valueObjectFriendlyName: SpecialControlValue.SubmitForm,
});
```

### API Clients

All client classes use singleton pattern via `getInstance()`, handle MSAL token acquisition automatically, and return `Response<T>` objects (see Error Handling below).

**Available Clients:**

- `ArmClient` - Generic ARM resource operations
- `SreAgentClient` - SRE Agent-specific APIs (extends ArmClient)
- `GraphClient` - Microsoft Graph API (users, photos, etc.)
- `SubscriptionClient` - List/filter Azure subscriptions
- `ResourceGroupClient` - Resource group operations and queries
- `LocationClient` - Azure regions and location metadata
- `DeploymentClient` - ARM template deployments
- `PermissionsClient` - RBAC permission checks
- `TelemetryClient` - Telemetry logging (use `useTelemetry` hook instead in components)

**Usage:** `const client = SreAgentClient.getInstance(TelemetrySource.MyView);`

### Error Handling

- Client methods return `Response<T>` with `isSuccessful` flag and optional `error` field
- **Do NOT wrap client calls in try/catch** - errors are already captured in the response object
- Check `response.isSuccessful` before accessing `response.data`
- Example: `if (!response.isSuccessful) { /* handle response.error */ }`

### No Barrel Exports

- **Do not create `index.ts` barrel files** for re-exporting hooks, components, or utilities.
- Use direct imports (e.g., `import { useUserPreferences } from '../Hooks/useUserPreferences'`).

### Arrow Functions

- Use **arrow function syntax** for all function declarations (components, hooks, utilities).
- Example: `export const useMyHook = () => { ... }` instead of `export function useMyHook() { ... }`

## Authentication

- Redirect-based Entra ID auth wired with `@azure/msal-browser` + `@azure/msal-react`; configuration lives in `src/Common/Auth/msalConfig.ts`.
- `AuthContext` now proxies MSAL state (`status`, `user`, `signIn`, `signOut`), enabling the navbar persona to trigger sign-in/out flows.
- **API clients** use MSAL's `acquireTokenSilent` for automatic token management (caching, refresh).
- **Iframe token manager** (`useAuthTokenManager`) handles proactive token refresh for iframe communication scenarios.
- **Full documentation**: See `docs/Authentication.md` for usage patterns and examples.

## Core Contexts

### UserPreferencesContext

- **`useUserPreferences()`** – Manages theme, locale, tenant, subscriptions, resource groups, and last accessed agent
- Built on `useLocalStorage` hook for type-safe persistence with cross-tab sync
- Storage key: `sre-agent-portal-preferences`

### SubscriptionsContext

- **`useSubscriptions()`** – App-wide subscription management with filtering, selection, and search
- Key methods: `setSelectedSubscriptions`, `toggleSubscription`, `filterSubscriptions`, `refresh`
- Handles multi-select with max 100 subscriptions, persists selection to user preferences
- Provides `isLoading`, `error`, `selectedSubscriptions`, `totalCount`, `isAllSelected` state

### NotificationContext

- **`useNotifications()`** – Global notification system with explicit API (`start/succeed/fail`), one-off notifications, Promise tracking, and polling
- **UI Components** – Navbar bell icon with badge/spinner, slide-out drawer with history, auto-dismissing toasts
- **Full documentation**: See `docs/Notifications.md` for usage patterns and examples

## Reusable Components

See `docs/Components.md` for detailed usage. Key components:

- **Wizard** - Multi-step dialog with stepper UI (see Create Agent flow)
- **ResourceGroupPicker** - Multi-select resource group picker with search/filter across subscriptions
- **SubscriptionDropdown** - Dropdown for subscription selection with Formik integration
- **ResourceGroupDropdown** - Dropdown for single resource group selection
- **ImageRadioGroup** - Radio group with image icons (used for permission templates)
- **PillFilter** - Filter UI with pill-based selection
- **TextWithLink** - Inline text with embedded links

## Common Utilities

Located in `src/Common/Utilities/`:

- **ArmId** - Parse ARM resource IDs into components (subscription, resource group, provider, etc.)
- **Guid** - Generate short GUIDs for UI element IDs
- **Url** - URL manipulation and query string helpers
- **Sanitization** - Sanitize data for telemetry logging
- **String** - String formatting helpers
- **Deployment** - ARM template building utilities (in `ArmTemplateBuilder/`)
- **Client** - Shared client error handling utilities

## Localization

- **All user-facing strings** must use `react-intl` via `PortalResources` (defined in `src/Strings/Resources.ts`)
- Add new strings to `Resources.ts` using `defineMessages` format
- **ESLint enforces** localization rules (enforce-id, enforce-default-message, etc.)
- String extraction happens automatically on commit via husky (`npm run extract:loc`)
- Usage: `const intl = useIntl(); intl.formatMessage(PortalResources.myString)`

## Common Pitfalls

Avoid these mistakes when working in this codebase:

1. **No barrel exports** - Don't create `index.ts` files; use direct imports
2. **Arrow functions only** - Use `export const MyComponent = () => {}` not `export function MyComponent()`
3. **Telemetry source from caller** - Pass `TelemetrySource.MyView` representing WHERE it's used, not what it is
4. **No console.log** - Use `useTelemetry` hook or `logTelemetryEvent` for all logging
5. **Window is always available** - Static hosting assumption; no need for `typeof window !== 'undefined'` checks
6. **Don't try/catch client responses** - Client methods return `Response<T>` with `isSuccessful` flag; check that instead
7. **Localize all strings** - ESLint will error if you use string literals in JSX

## Related Documentation

- `docs/Authentication.md` - Auth patterns, MSAL usage, token management
- `docs/Notifications.md` - Notification system API and examples
- `docs/Components.md` - Reusable component reference
- `docs/Routing.md` - Route structure and deep linking
- `docs/UX/` - UX patterns and Fluent UI guidance (in main repo)
- `Agent.Web/Client/` - Canonical reference for tooling and testing strategy
