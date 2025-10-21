# Agent Portal Client Context

> ⚠️ **Authoring reminder:** future updates must stay extremely concise and capture only the most critical context needed for agents to reason about this project. Prefer links to verbose docs elsewhere.

## Overview

- **Project**: `Agent.Portal/Client` – forthcoming Vite/React SPA parallel to `Agent.Web/Client`, delivered as a static build output.
- **Purpose**: Power the SRE Agent portal experience with Fluent UI v9 components, shared localization patterns, and common utilities.
- **Status**: Initial scaffolding in progress; localization directories mirror the web client structure to enable string extraction.

## Key Directories

- `src/Strings` – Localization resources, extraction targets, enforcement tests, and React Intl helpers.
- `src/Strings/__test__` – Vitest-based string and localization guardrails.
- `src/Strings/Intl` – Shared Intl provider and helper utilities.
- `src/Strings/extracted` – Generated `strings.json` source for localization (empty placeholder committed).
- `src/Strings/compiled` – Placeholder for compiled translation bundles produced by localization tooling.
- `src/Common/Hooks` – Reusable React hooks (localStorage persistence, user preferences, etc.).
- `src/Common/Contexts` – React context providers (Auth, UserPreferences, etc.).

## Architecture & Patterns

### Static Hosting

- This portal is served as **static HTML/JS** with no server-side rendering (SSR).
- All hooks and utilities assume `window` is always available at runtime (although are likely wrapped in try/catch blocks)

### No Barrel Exports

- **Do not create `index.ts` barrel files** for re-exporting hooks, components, or utilities.
- Use direct imports (e.g., `import { useUserPreferences } from '../Hooks/useUserPreferences'`).

### Arrow Functions

- Use **arrow function syntax** for all function declarations (components, hooks, utilities).
- Example: `export const useMyHook = () => { ... }` instead of `export function useMyHook() { ... }`

## Authentication

- Redirect-based Entra ID auth wired with `@azure/msal-browser` + `@azure/msal-react`; configuration lives in `src/Common/Auth/msalConfig.ts`.
- `AuthContext` now proxies MSAL state (`status`, `user`, `signIn`, `signOut`), enabling the navbar persona to trigger sign-in/out flows.

## User Preferences

- **`useLocalStorage`** – Generic hook for type-safe localStorage persistence with cross-tab sync.
- **`useUserPreferences`** – Domain-specific hook built on `useLocalStorage` for managing theme, locale, tenant, subscriptions, resource groups, and last accessed agent.
- **`UserPreferencesContext`** – Context provider wrapping `useUserPreferences` for app-wide access.
- Storage key: `sre-agent-portal-preferences`

## Notifications

- **`NotificationContext`** – Global notification system with support for in-progress, success, error, warning, and info states.
- **`useNotifications()`** – Hook providing explicit API (`start/succeed/fail`), one-off notifications (`info/warning/error`), Promise tracking, and polling support.
- **UI Components** – Navbar bell icon with badge/spinner, slide-out drawer with history, and auto-dismissing toasts at top-right.
- **Full documentation**: See `docs/NOTIFICATIONS.md` for usage patterns and examples.

## Related Artifacts

- Shared localization config lives in `Agent.Web/Client/src/Strings/LocProject.json` (includes portal extraction paths).
- UX patterns, component guidance, and Fluent usage are documented in `docs/UX/`.
- Agent Web client serves as the canonical reference for project layout, tooling, and testing strategy.

## Future Additions

- Token acquisition helpers and authenticated API clients planned for a follow-on milestone.
- Add concise sub-docs in this directory (e.g., `Tooling.md`, `Testing.md`) when topics grow beyond a few bullets. Link them here and keep summaries tight.
