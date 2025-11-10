# GitHub Issue Management

## Overview

Use this file when an incident or technical problem needs formal tracking, coordination, and an auditable record. Focus on producing a complete, structured issue with correlated source code and (if infrastructure changed) recorded adjustments.

Triggers:

- Outages, elevated error rates, failed deployments
- Security or configuration incidents
- Need for coordinated fixes across repos/teams
- Requirement to document infra or APIM capacity context

## Capabilities

- Create or update issues with full context (errors, stack traces, logs)
- Select correct repository; avoid duplicates
- Apply labels, assign owners, notify stakeholders
- Link related issues and PRs; capture dependencies
- Document IaC / APIM capacity deltas (CPU, memory, instances) when relevant
- Connect/unlink Azure resource ↔ repository (visibility, automation)
- Return issue URL, status, tracking ID

## When to Use

Start or update an issue once severity or persistence is established OR immediately for high-impact incidents. If only a quick clarification is needed, skip until impact confirmed.

## Prerequisites

- Verify repository access (create/update labels, assign)
- Know required labels/templates/severity conventions
- On access/API failure: record failure, continue analysis (don’t block)

## Repository Selection

Prefer repo containing the failing code path. If ambiguous, weigh: stack trace modules, deployment manifests, IaC folder, integration clients. Multiple repos: parent tracking issue + linked children or separate coordinated issues.

## Duplicate Check

Search by exception message/code, key stack frames, endpoint/service name, recent incident keywords. If match: update with new context (don’t duplicate). If none: create new using template.

## Issue Template (Fill What’s Available)

Title: Component: concise symptom (e.g., Payments: Authorization timeouts in prod)

Summary: 1 paragraph impact + symptom.
Context: environments, timestamps, correlation/request IDs, commit SHAs, affected endpoints/services.
Error Details: exception types/messages/codes; full (or top) stack traces; key logs (timestamps, redact secrets).
Reproduction: steps, inputs, expected vs observed.
Correlated Source Code: file paths + line ranges + roles + suspected root cause (from source_code_analysis skill).
Config & Deployment: relevant flags, timeouts, feature toggles, manifests/containers changes.
IaC / APIM: mechanism used ("Embeddings" or "grep"), capacity (CPU, memory, instances) and runtime deltas.
Impact: user/business effect, error rate, SLO/SLA deviation.
Mitigation: actions taken (rollbacks, toggles, scaling), current status.
Next Steps: focused fixes, test additions, logging/instrumentation.
Attachments: dashboards, PRs, related issues, runbooks.
Metadata: severity label, component, env, assignees, milestone/project.

## Correlate with Source Code

Open/consult the source_code_analysis skill to gather:

- Symbols → definitions/usages
- File path:line-range + minimal snippet
- Call chain + config/timeout/retry junctions
- Ranked suspected locations (with reasoning + confidence)
- Suggested focused test or logging additions

Insert concise "Correlated Source Code" section into the issue body.

## IaC / APIM Documentation

Identify IaC mechanism (state "Embeddings" or "grep"). Note Terraform / Bicep / ARM / Helm / K8s presence. For APIM incidents: capture declared CPU, memory, instances vs runtime observed; flag drift. Summarize recent infra PRs or pending plan/apply. Runtime-only changes → record using `change_propagation.md`.

## Labels & Cross-Referencing

Apply severity, env, component labels. Notify on‑call & owners. Link related incidents, prior regressions, investigating PRs, dashboards, runbooks.

## Azure Resource ↔ Repository Linkage

If linking/unlinking: record resource ID, subscription, region, repo path, rationale, expected effect. Capture auth failures explicitly.

## Failure Handling

On API/permission failure: explain briefly, record details, continue analysis (don’t block correlation or mitigation).

## Workflow (Condensed)

1. Duplicate check → update or create
2. Select repository + verify access
3. Gather error context (exceptions, stack, logs, IDs, env)
4. Correlate source (source_code_analysis skill)
5. Identify IaC mechanism; capture APIM capacity if relevant
6. Fill template; apply labels; assign owners; link related artifacts
7. Publish → return URL + tracking ID
8. Record uncommitted infra changes (open `change_propagation.md` if needed)

## Examples (Condensed)

Orders API Unauthorized:

- Stack: AuthMiddleware.ValidateToken → OrdersController.Submit
- Code: `middleware/auth_middleware.ts:88–134`, `services/token_service.ts:45–92` (Medium; IdP config missing)
- Drift: APIM instances declared 2, runtime 1 (grep). Next: add clock skew, reconcile APIM, enhance auth logging.

Payment Authorization Timeout:

- Code: `authorization_service.rb:75–118` calls `payment_gateway_client.rb:30–66` (Medium)
- APIM: declared 2 instances runtime shows 1 (Embeddings). Next: increase timeout + jittered retry, restore capacity, add correlation IDs.

## Change Recording & Audit

If runtime infra or config was altered: open `change_propagation.md` to produce record-only structured change entries (no execution). Link resulting records in the issue.

## Outputs

- Issue URL + tracking ID
- Created/updated status, labels, assignees
- Correlated code summary (paths, confidence)
- IaC/APIM findings + drift notes
