---
name: logic_app
description: |
  Load this skill when the user asks about Azure Logic Apps (Consumption or Standard) including failures, run history, trigger/action errors, performance, throttling, configuration, networking, security, cost optimization, or when they explicitly request architecture/design/recommendation guidance. Use it for: diagnosing incidents, analyzing workflows, proposing improvements, or validating connector usage. For high-level improvement requests ("optimize", "best practices", "reduce cost", "improve reliability/performance").
tools:
  - GetLogicAppInfo
  - GetManagedConnectors
  - LookupServiceProviderConnectorEquivalent
  - GetAppSetting
  - UpdateAppSetting
  - GetMissingDiagnosticSettings
  - ListHttpRequestTriggerWorkflows
  - IsApplicationInsightsConfigured
  - IsExtensionBundleVersionPinned
  - IsEasyAuthEnabled
  - GetDeploymentSlotsResourceIds
---

# Azure Logic Apps SRE Skill

## Purpose

Provide precise diagnostics, troubleshooting steps, and operational guidance for Azure Logic Apps (Consumption & Standard). Core scopes:

1. Trigger/action failures, workflow reliability, unexpected results.
2. Performance: latency, timeouts, throttling (429), concurrency, run duration.
3. Configuration & deployment choices (Consumption vs Standard, ISE) and networking/security.
4. Observability: run history, logs, metrics, correlation.
5. Cost & efficiency (connector usage, unnecessary polling, excessive loops).

When user intent is primarily improvement or strategic ("optimize", "best practices", architecture, cost/reliability/performance recommendations) open [logic_app_recommendation.md](logic_app_recommendation.md) after gathering baseline diagnostics here.

## Capabilities

You can:

- Inspect trigger health (schedule, HTTP, Service Bus, Storage, Event Grid) & missed runs.
- Diagnose action failures, connector auth issues (401/403), throttling (429), backend errors (5xx).
- Analyze run history for patterns: duration, retries, failure concentration, concurrency saturation.
- Surface platform or connector limits impacting reliability or performance.
- Review networking (private endpoints, DNS, firewall/IP restrictions) & identity configuration.
- Evaluate deployment model (Consumption vs Standard; ISE implications) and recommend adjustments.
- Produce remediation steps (auth refresh, retry/backoff tuning, batching, idempotency, segmentation into sub-workflows).
- Enhance observability (correlation IDs, structured logs, targeted metrics & alerts).

## Troubleshooting Flow

1. Clarify scope & impact: workflow(s) affected, frequency, first occurrence, recent changes, data loss risk.
2. Collect telemetry:
   - Run history (status, duration, error, retries)
   - Trigger diagnostics (last fire, next fire, outputs)
   - Action details (inputs/outputs, status codes, retry count)
   - Metrics (Runs, FailedRuns, Throttled, ActionDuration) & logs (WorkflowRuntime, Triggers, Actions)
3. Pattern recognition:
   - Auth (401/403), throttling (429), timeouts (408/504), long-running, schema/data validation, concurrency saturation, network/DNS/firewall.
4. Remediate by theme:
   - Auth: re-authorize, prefer Managed Identity, check RBAC scope.
   - Reliability: tuned exponential retries (bounded), dead-letter queues, backoff, idempotency keys.
   - Performance: controlled parallelism, segmentation into sub-workflows, batching, caching, reduce chatty calls.
   - Data: upfront schema validation, guard nulls, structured error scopes (Try/Catch/Finally) + compensations.
   - Networking: validate private endpoint DNS, required outbound IPs, TLS & ports.
   - Observability: correlation IDs, structured logs, custom tracking fields.
   - Governance: alerts on failure rate, throttling, latency SLO breach; maintain runbooks, automate repetitive mitigations.

### Triggers

- Scheduled: validate CRON, time zone, DST, overlap due to previous long run (use concurrency controls).
- Event-based (Service Bus/Storage/Event Grid): check subscription filters, DLQ usage, backlog size, lock renewal.
- HTTP: confirm auth (AAD or keys), payload size limits, proper status codes & structured error body.

### Actions & Connectors

- Auth & state: re-authorize stale connections; rotate secrets; shift to Managed Identity when feasible (apply least privilege RBAC).
- Error codes: 401/403 (scope/RBAC); 429 (throttle -> delay, batch, paginate); 5xx (transient -> bounded retries + fallback).
- Large payloads: chunk/paginate; offload to Blob storage & pass reference URIs; respect size/depth limits.

### Concurrency & Idempotency

- Set explicit concurrency caps (trigger & for-each) to prevent saturation.
- Idempotency: dedupe keys, check-before-write, guard side-effects on retry.
- High throughput: queue-based leveling, apply backpressure when downstream lag observed.

### Error Handling

- Wrap risky segments in scopes; branch on failure; emit structured error event.
- Poison messages: dead-letter queue + quarantine workflow.
- Compensations for reversible side-effects (e.g., revert writes) when necessary.

### Observability & Monitoring

- Ensure diagnostics routed (Log Analytics / App Insights). Track custom events & dependencies.
- Correlate runs via consistent IDs injected early.
- Availability tests for critical HTTP endpoints.
- Alerts: failed-run rate, abnormal duration, trigger inactivity, throttling spikes; define severity & escalation.

### Platform & Configuration

- Consumption: pay-per-execution; monitor connector rate limits & regional quotas.
- Standard: isolated runtime, higher throughput, versioning; check sizing & networking.
- ISE: VNET isolation / dedicated capacity; validate DNS & cost profile.

### Security

- Favor Managed Identity; minimize scope.
- Store secrets in Key Vault; rotate & audit.
- Validate inbound payloads; scrub sensitive fields in logs.
- Enforce least privilege RBAC & necessary network restrictions.

### Cost & Performance

- Remove redundant actions/loops; replace polling with event-based triggers.
- Batch & paginate; cache static data; store large artifacts externally.
- Monitor connectors generating cost (high call volume, failed retries).

## Examples (Condensed)

### Intermittent Connector Failures (429/5xx)

- Review run history & outputs -> pattern & frequency.
- Measure call rate vs documented limits.
- Apply bounded exponential retry + jitter, concurrency caps.
- Queue-based backpressure if sustained.
- Alert on throttling trend.

### HTTP Trigger 401

- Verify auth mechanism & key/app registration validity.
- Validate audience/scopes & token lifetime.
- Migrate to Managed Identity if possible; adjust RBAC.
- Return structured error with correlation ID.

### Large Payload Timeouts

- Assess size & duration vs limits.
- Externalize payload to Blob; pass URI; chunk processing.
- Adjust timeouts if supported; bounded retries.
- Alert on duration spike; consider async sub-workflows.

### Missed Schedule Runs

- Confirm CRON, TZ, DST adjustments.
- Detect prior run overlap (add concurrency cap).
- Alert on trigger inactivity vs SLA.

## Recommendations File

For strategic improvements (architecture, resiliency, performance, cost, best practices) open [logic_app_recommendation.md](logic_app_recommendation.md) after baseline diagnostics. It provides structured evaluation & reporting patterns.

## Reference Sources

- Official Logic Apps docs (Consumption, Standard) & connector limits.
- Application Insights (tracing, dependencies, exceptions).
- Azure Monitor metrics & alert rules.
- Networking & private endpoint guidance.
