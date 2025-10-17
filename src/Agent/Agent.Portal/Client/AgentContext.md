# Agent Portal Client Context

> ⚠️ **Authoring reminder:** future updates must stay extremely concise and capture only the most critical context needed for agents to reason about this project. Prefer links to verbose docs elsewhere.

## Overview

- **Project**: `Agent.Portal/Client` – forthcoming Vite/React SPA parallel to `Agent.Web/Client`, delivered as a static build output.
- **Purpose**: Power the SRE Agent portal experience with Fluent UI v9 components, shared localization patterns, and common utilities.
- **Status**: Initial scaffolding in progress; localization directories mirror the web client structure to enable string extraction.

## Key Directories

- `src/src/Strings` – Localization resources, extraction targets, enforcement tests, and React Intl helpers.
- `src/src/Strings/__test__` – Vitest-based string and localization guardrails.
- `src/src/Strings/Intl` – Shared Intl provider and helper utilities.
- `src/src/Strings/extracted` – Generated `strings.json` source for localization (empty placeholder committed).
- `src/src/Strings/compiled` – Placeholder for compiled translation bundles produced by localization tooling.

## Related Artifacts

- Shared localization config lives in `Agent.Web/Client/src/src/Strings/LocProject.json` (includes portal extraction paths).
- UX patterns, component guidance, and Fluent usage are documented in `docs/UX/`.
- Agent Web client serves as the canonical reference for project layout, tooling, and testing strategy.

## Future Additions

Add concise sub-docs in this directory (e.g., `Tooling.md`, `Testing.md`) when topics grow beyond a few bullets. Link them here and keep summaries tight.
