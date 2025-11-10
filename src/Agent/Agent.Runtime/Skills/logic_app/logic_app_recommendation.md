# Azure Logic App Recommendations

## Scope

Use after baseline diagnostics (from SKILL.md) when the user wants improvement / optimization (architecture, reliability, performance, cost, security, maintainability). Distinguish the parent Logic App (Standard) resource from each contained workflow; produce actionable, prioritized recommendations with rationale & implementation steps.

## Context Collection

Gather first:

- Full ARM resource ID.
- Environment / deployment model (app settings, plan, region).
- Workload traits (volume, concurrency targets, SLAs).
- Pain points or goals (throttling, latency, cost, reliability).
- Inventory of workflows & connectors (managed vs built-in/service provider).

If identifiers are incomplete, leverage general Azure resource identification capabilities (as provided by the main system prompt) to fill gaps—do not assume unavailable data.

## Structure

- App-level (Logic App Standard): runtime hosting, shared settings.
- Workflow-level: definition logic, connectors, error handling, performance patterns.

Enumerate all workflows and assess both scopes.

## Analysis Steps

1. Enumerate: ARM ID + all workflows.
1. Inventory connectors per workflow (managed vs built-in/service provider).
1. Evaluate rules:

- Connector replacement candidates.
- Extension bundle version.
- Reliability & performance patterns (retries, concurrency, segmentation).
- Security posture (identity, secrets, RBAC, network isolation).

1. Prioritize (impact vs effort) & define validation metrics.

## Recommendation Domains

### A. Service Provider Connector Replacement (Workflow-Level)

Goal: Replace managed connectors where a built-in/service provider equivalent improves throughput, isolation, private networking, cost predictability.

Checklist:

- List managed connectors per workflow.
- For each, determine viable equivalent & note feature parity gaps.

Recommendation format (per workflow & connector): connector_current -> connector_equivalent | benefits | trade-offs | steps | rollback.

Common benefits: fewer throttles, lower latency, private endpoint/VNET, unified Managed Identity access. Trade-offs: migration effort, parity gaps.

Implementation: update definition, migrate auth (Managed Identity preferred), validate inputs/outputs & error handling, load test & monitor.

### B. Extension Bundle Version (App-Level)

Check AzureFunctionsJobHost__extensionBundle__version.

If pinned (e.g., 1.17.2) -> recommend floating range [1.*, 2.0.0) for non-breaking updates.

Steps: update setting (Portal or IaC), redeploy, restart, validate workflows, record previous value for rollback.

## Reporting Template

Summary:

- App: name, region, ARM ID
- Workflows analyzed (list)
- Objectives (reliability, performance, cost, security)

Findings:

- App-Level: extension bundle (current vs recommended) + steps
- Workflow-Level (repeat): workflow name, managed connectors -> equivalents (benefits/trade-offs/prereqs), migration steps & test plan

Prioritization: High-impact, Medium, Quick wins

Validation: metrics (failure rate, latency, throttling, cost/run) + rollback steps.

## Example Output (Condensed)

App: contoso-la-standard | Region: westus2 | ARM: /subscriptions/.../logicApps/...

Workflows: orders-intake, billing-sync

Findings:

- orders-intake: AzureBlob -> Storage (built-in). Benefits: private routing, fewer throttles. Steps: replace actions X/Y/Z, assign MI Blob Data Contributor, load test (2x peak). Rollback: revert definition commit.
- billing-sync: ServiceBus (managed) -> Service Bus (service provider). Benefits: consistent MI auth, reliability. Steps: replace send/receive, assign MI roles, verify sessions.
- App: Bundle pinned 1.17.2 -> Recommend [1.*, 2.0.0); update setting & redeploy.

Prioritization: High=billing-sync replacement | Medium=orders-intake replacement | Quick Win=bundle update

Validation Metrics: throttling events ↓, avg latency ↓, cost/run ↓.

## Best Practices

- Prefer Managed Identity; apply least privilege RBAC.
- Confirm feature parity before connector migration (sessions, transactions, chunking).
- Stage & load test; compare pre/post metrics (latency, errors, 429s, cost).
- Preserve observability (correlation IDs, structured logs) through changes.
- Define rollback path & keep workflow version history.
