# Source Code Analysis

## Purpose

Provide targeted, semantic understanding of source code across connected repositories: locate implementations, explain behavior, trace errors to code, outline architecture, and recommend precise remediation or refactor steps. Align responses with the main system prompt: answer first concisely (1–2 sentences), then supply only the supporting code/data needed.

## When to Use (Skill Triggers)

Load (or keep active) when the user asks to:

- Find where a feature, API route, method, or behavior is implemented
- Explain a data flow, lifecycle, or architectural relationship
- Correlate stack traces, exceptions, or logs with source locations
- Assess code quality or suggest scoped refactors
- Map runtime symptoms (timeouts, failures, anomalies) to code paths or configuration

Do NOT use for simple file listings, repository counts, or trivial name lookups already served by basic resource or code search tools.

## Progressive Discovery

Start with this file. Open supplemental files only when their trigger applies:

- Open `github_issue.md` when formal incident tracking or cross-team coordination is required (issue creation/update, labeling, audit trail).
- Open `change_propagation.md` after remediation actions have altered runtime infrastructure or configuration and those changes must be recorded (not executed) for reconciliation with IaC.

If both triggers apply, open `github_issue.md` first (establish tracking), then `change_propagation.md`.

## Core Workflow (Feature / Behavior / Architecture)

1. Clarify intent: feature location | behavior explanation | pattern | dependency | data flow.
2. Form search terms: domain nouns, class/function names, error tokens, config keys, endpoint paths, file patterns.
3. Search & collect: symbols, definitions, usages, related tests, config, and integration points.
4. Trace flow: entry point → service/business logic → persistence/integration → exit/side effects.
5. Synthesize: minimal snippets (with file path:line(s)), describe roles, data transformations, constraints.
6. Respond: concise answer → structured supporting details (paths, ranges, confidence) → next steps only if valuable.

## Error / Exception Workflow

Use when stack traces, error messages, or logs are provided.

1. Parse: exception type, message, code, top stack frames (file, method, line), any IDs or environment.
2. Classify: validation | dependency | resource timeout | concurrency | configuration.
3. Locate: definitions/usages of stacked symbols; error message constants; retry/timeout blocks; config branches.
4. Correlate: build call chain; align logs (logger tags / structured fields) with code paths; check conditional logic.
5. Prioritize causes: direct stack references > recent changes > fragile patterns (broad catch, silent null). Note anti‑patterns.
6. Output: ranked suspected files/functions with reasoning, confidence, and focused fix/test/log recommendations.

## Precision & Evidence

- Always include repository + file path and line range when possible.
- Use only necessary snippet lines; elide with `…` if skipping.
- Cite tests that assert the behavior (if found) to validate intent or edge cases.
- Distinguish static config vs runtime conditions (feature flags, environment overrides).

## Quality & Refactor Assessment (When Requested)

Identify: duplication, tight coupling, implicit state, error handling gaps, large methods, mixed concerns.
Recommend only scoped, high-impact refactors (e.g., extract method, introduce interface, pure function for side-effect isolation). Avoid speculative large redesigns unless user explicitly asks.

## Confidence Reporting

Tag key conclusions as High / Medium / Low:

- High: direct code match + supporting tests
- Medium: plausible path; partial coverage; missing one layer
- Low: inferred; missing definitions or private/unindexed code

State missing artifacts (e.g., generated files not present) rather than guessing.

## Output Format (Default)

Answer (≤2 sentences) → Supporting Details table or bullets → Optional Next Steps.

Supporting details should include (when relevant):

- File path:line-range
- Role (entry, orchestrator, adapter, persistence)
- Key logic or condition summary
- Confidence

## Actionable Next Steps (Only If Needed)

Examples: add targeted logging around a conditional, create reproducer test, adjust timeout parameter, isolate side‑effect, confirm config propagation.
Avoid generic “refactor code” statements.

## Examples (Condensed)

Feature Location → Password reset email:

- `services/password_reset_service.ts:58–102`: token + payload assembly (High)
- Called by `controllers/auth_controller.ts:120–160`

Next: verify template variables; add send-failure logging.

Data Flow → Order status to Shipped:

- Entry `api/orders_controller.py:210–245` → `domain/order_service.py:90–140` (state machine) → `repositories/order_repo.py:45–78` (persist) → constraints `domain/states.py:10–45` (Medium; integration dispatch pending).

Error Correlation → Payment authorization timeout:

- Timeout in `payments/authorization_service.rb:75–118` calling `integrations/payment_gateway_client.rb:30–66`; config mismatch `config/payment.yml` (prod 2s vs staging 5s). Confidence: Medium. Next: align timeout, add jittered retry, log correlation IDs.

## Supplemental File Triggers

Open `github_issue.md` when you need: formal tracking, duplicate check, structured issue template, stakeholder labeling.
Open `change_propagation.md` when: runtime infra/config adjustments must be recorded for IaC parity (scale, SKU, flags, resource adds/removals) without applying changes.

## Do & Don’t Summary

DO: minimal precise snippets, direct file paths, ranked causes, confidence, focused next steps.
DON’T: verbose narrative, speculative architecture, unverified guesses, broad refactor prescriptions.

## Related Files

- [github_issue.md](github_issue.md) – incident issue creation/update workflow & template.
- [change_propagation.md](change_propagation.md) – record-only infrastructure/config change documentation.

Maintain alignment with system prompt constraints (Safety > Accuracy > Conciseness > Efficiency). This skill augments domain depth; it does not relax conciseness requirements.
