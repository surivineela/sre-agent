# Self Manual Skill

## Purpose

Provide concise, up‑to‑date manual guidance on: capabilities, configuration patterns for PagerDuty & Azure Monitor, and billing/cost optimization. Answers should align with the system prompt priorities (safety, accuracy, conciseness, efficiency).

## When This Skill Applies

Use when the user explicitly asks about: what the agent can do, how to integrate or configure PagerDuty or Azure Monitor alerts/action groups, billing/cost drivers, or end‑to‑end setup/validation of those integrations. For general resource diagnostics or service‑specific troubleshooting, rely on other skills or standard tools instead.

## Query Strategy

1. If the query is about capabilities or integrations, form a focused search including the feature/integration + an outcome (e.g. "PagerDuty routing key rotation", "Azure Monitor action group validation").
2. Only include the phrase "Azure SRE Agent" in the query if source documentation indexing obviously benefits; otherwise prefer the user's phrasing. Preserve first‑person identity as per system prompt in responses.
3. Narrow overly broad queries by clarifying scope (environment, service, integration target) only when essential information is missing.

## Using Retrieved Content

Summarize first (1–2 sentences), then provide structured steps: prerequisites → setup → validation → optimization → troubleshooting. Do not mention searching or tools. Paraphrase source text; retain technical accuracy.

## Response Formatting

Use numbered steps for procedures. Include: Azure Portal navigation paths, key field names, API endpoints, mapping decisions (severity, dedup keys), validation checks, rollback notes where relevant. Tables acceptable for comparing ≥3 items. Avoid unnecessary narrative.

## Core Capability Areas

- Manual feature explanation (what, why, boundaries).
- Incident integration setup (PagerDuty, Azure Monitor).
- Alert design quality (actionable thresholds, suppression, deduplication, severity mapping).
- Billing & cost estimation (Monitor, Log Analytics, Logic Apps, PagerDuty plan impacts) + optimization tactics.

## Best Practices (Condensed)

- Environment & Access: Confirm subscription, resource group, region, RBAC, PagerDuty permissions.
- Secrets: Store routing/integration keys in Key Vault; rotate & limit scope.
- IaC: Favor Bicep/Terraform/ARM; parameterize environment specifics.
- Alert Hygiene: Actionable thresholds, suppression windows, consistent severity mapping, stable dedup keys.
- Noise Reduction: Aggregate similar signals; enrich events with remediation/runbook links.
- Validation: Use synthetic alerts in non‑prod; track MTTA/MTTR trendlines.
- Governance: Version control alert & integration definitions; audit destinations periodically.

## PagerDuty Integration (Actionable Steps)

Prerequisites: Azure rights (create Action Groups/Alert Rules), PagerDuty service/integration management rights, Key Vault for routing key.

1. Plan Routing: Decide single vs. multiple services (by workload/env). Define escalation & schedules.
2. Create/Confirm Service: Add Events API v2 integration; obtain routing (integration) key; note Service ID.
3. Secure Key: Store in Key Vault; grant least‑privilege access to automation identity.
4. Action Group: Monitor → Alerts → Action groups → Create. Include webhook or Logic App.
   - Logic App pattern: POST <https://events.pagerduty.com/v2/enqueue> (JSON, retries, log 4xx/5xx).
   - Body essentials: routing_key, event_action (trigger/resolve), payload(summary,severity,source,timestamp,component,group,class), dedup_key.
5. Field Mapping: Define severity translation (Azure → info|warning|error|critical). Stable dedup key (alertRuleName|resourceId). Add runbook URL references.
6. Alert Rules: Scope resources; condition (metric/log query); associate PagerDuty action group; consistent naming.
7. Test: Synthetic alert → verify incident creation, correct escalation, dedup holds, resolve flow if implemented.
8. Operate: Dashboards (volume, dedup %, failures), periodic threshold tuning.

Troubleshooting:

- Missing incidents: Check Logic App/webhook history & key validity.
- Noise: Tighten thresholds, suppression, dedup key uniqueness.
- Wrong severity: Audit mapping transform.

### Extended Reference: PagerDuty Integration Details

Use this internal reference when deeper implementation specifics are needed (adapt detail level to user request):

Detailed Prerequisites:

1. Azure: Create Action Groups, Alert Rules, Logic Apps; Key Vault access for secrets.
2. PagerDuty: Create Services, Integrations, manage escalation policies & on‑call schedules.
3. Secret Management: Key Vault access policy granting Get for routing key only to automation identity.

Naming Conventions:

- Service: `<workload>-<env>-svc` (e.g. `payments-prod-svc`).
- Alert Rule: `<signal>-<scope>-<severity>` (e.g. `cpu-high-aks-critical`).
- Action Group short name: ≤12 chars (e.g. `paypdcrit`).

Severity Mapping Example:

| Azure Severity | PagerDuty Severity | Notes |
| -------------- | ------------------ | ----- |
| 0 (Critical)   | critical           | Immediate human action |
| 1 (Error)      | error              | High impact but contained |
| 2 (Warning)    | warning            | Degraded, monitor |
| 3 (Informational) | info            | Low priority signal |
| 4 (Verbose)    | info               | Often suppress/aggregate |

Sample Logic App POST Body (trigger):

```json
{
   "routing_key": "<keyVault:pd-routing-key>",
   "event_action": "trigger",
   "payload": {
      "summary": "{{alertName}}: {{description}}",
      "severity": "{{mappedSeverity}}",
      "source": "{{resourceId}}",
      "timestamp": "{{utcTimestamp}}",
      "component": "{{serviceName}}",
      "group": "{{environment}}",
      "class": "{{alertCategory}}"
   },
   "dedup_key": "{{alertRuleName}}|{{resourceId}}",
   "links": [
      { "href": "{{runbookUrl}}", "text": "Runbook" },
      { "href": "{{dashboardUrl}}", "text": "Dashboard" }
   ]
}
```

Resolve Body:

```json
{
   "routing_key": "<keyVault:pd-routing-key>",
   "event_action": "resolve",
   "dedup_key": "{{alertRuleName}}|{{resourceId}}",
   "payload": {
      "summary": "{{alertName}} resolved",
      "source": "{{resourceId}}"
   }
}
```

Dedup Key Guidance:

- Stable across retriggers until clear; use invariant IDs (rule name + resourceId).
- Avoid dynamic fields (timestamps, counts).
- Multi‑resource queries: hash normalized resource list if needed.

Retries & Error Handling (Logic App):

- Exponential backoff for 429/5xx (e.g. 1s, 5s, 30s).
- Log status + correlation ID to Azure Monitor Logs.
- Dead‑letter: persist failed payloads in storage queue for manual review.

Operational Dashboards:

- Incidents by service over time.
- Dedup % (suppressed duplicates / total raw alerts).
- MTTA & MTTR trends.
- Delivery failure rate (non‑2xx responses).

Common Failures & Fixes:

- 403 from Events API: Rotated routing key → refresh Key Vault secret.
- Duplicate incidents: Dedup key includes dynamic element → simplify.
- Slow ack: Escalation misconfiguration → audit policy tiers & schedules.

Routing Key Rotation:

1. Generate new integration key (keep old active).
2. Add new version in Key Vault.
3. Logic App references latest version automatically if using secret reference.
4. Fire synthetic alert; confirm incident creation with new key.
5. Retire old key.

Security Notes:

- Mask routing key in logs (show last 4 chars).
- Use managed identity for Key Vault access.
- Quarterly access review.

Escalation Policy Tips:

- ≤3 tiers to reduce latency.
- Include fallback channel (SMS/push) for critical tier.
- Periodic manual ack drills.

## Azure Monitor Alerting (Actionable Steps)

Prerequisites: Proper RBAC, Log Analytics ingestion (for log alerts).

1. Strategy: Identify critical metrics/log signals & severity tiers; plan suppression windows & dedup scheme.
2. Action Groups: Create with required channels (email/SMS/webhook/ITSM/Logic App). Tag by env/team.
3. Metric Alerts: Define scope, metric, threshold, evaluation period, frequency; consider dynamic thresholds selectively.
4. Log (KQL) Alerts: Author focused KQL returning incident rows; configure lookback period, frequency, trigger threshold; attach action group.
5. Validation: Test against historical data & synthetic events; confirm notification delivery.
6. Maintenance: Periodic review for false positives/negatives; adjust recipients & thresholds after topology changes.

Troubleshooting:

- Not firing: Review frequency/lookback & ingestion health.
- Storming: Increase thresholds, add filters, suppression windows.
- Delivery failures: Inspect action group endpoints & Logic App errors.

### Extended Reference: Azure Monitor Alerting Details

Metric vs Log Alerts:

- Metric: Near real‑time numeric thresholds (CPU %, Memory %, Requests/sec errors).
- Log (KQL): Complex pattern/count detection; latency depends on ingestion + query window.

Dynamic Thresholds:

- Good for cyclical usage patterns.
- Avoid for binary failure conditions (e.g. heartbeat missing) or strict SLO breach metrics.
- Observe false positive rate for 7 days post‑enable.

KQL Pattern Example (Error Rate):

```kql
AppRequests
| where Timestamp > ago(15m)
| summarize errorRate = sum(case(StatusCode >= 500, 1, 0)) / count() by bin(Timestamp, 5m)
| where errorRate > 0.05
```

Trigger if result count > 0; use stable dedup: `errorRateHigh|<service>`.

SLO Alignment:

- Threshold maps to error budget burn (>2× normal burn rate over evaluation period triggers early warning).

Suppression Windows:

- Tag planned maintenance (`maintenance=true`) and filter out in KQL.
- Temporarily disable non‑critical alerts during large migrations.

Resource Scoping:

- Use resource group scoping to exclude test assets.
- AKS: Node metrics via cluster resource; pod/container metrics typically via Log Analytics.

Action Group Design:

- Separate critical (PagerDuty/webhook) from informational (email) to reduce fatigue.
- Tag groups for ownership & cost tracking: `env=prod`, `owner=payments`.

Validation Techniques:

- Historical replay: run KQL for past 7 days, count hypothetical triggers.
- Synthetic injection: add test log entry matching pattern in non‑prod.

Maintenance Cadence:

- Monthly: Triage false positives/negatives.
- Quarterly: Audit recipients & endpoints.
- Semiannual: Rationalize alert set vs architecture changes.

Common Pitfalls:

- Over‑broad KQL (missing filters) → storming.
- Static threshold on cyclic metric → peak hour noise.
- Missing ingestion → silent failures.

Cost Impact Notes:

- 1‑minute evaluation frequency increases cost; reserve for critical SLO metrics.
- Broad queries over large workspaces elevate cost—narrow tables and use `project`.

Optimization Patterns:

- Aggregate low‑severity signals into structured JSON payload for single alert.
- Use metric dimensions judiciously; avoid creating many near‑duplicate alerts.

Rollback Guidance:

- Disable misbehaving alert, analyze fire history, adjust filter/threshold, re‑enable.
- Keep versioned IaC definitions for quick restoration.

## Billing & Cost Guidance

- Drivers: Monitor evaluations, Log ingestion/retention (GB), Logic App executions, PagerDuty plan & event volume.
- Estimation Flow: Inventory resources → forecast telemetry & evaluation frequency → estimate triggers & Logic App runs → add PagerDuty user/plan costs.
- Cost Controls: Scope ingestion, sampling, dynamic thresholds, consolidate alerts, stable dedup keys, budgets & cost tags, periodic usage reviews (Cost Management, Log Analytics, Logic App runs).

## Example Query Reframes

"What can you do?" → capabilities manual features summary.
"Set up PagerDuty" → pagerduty integration routing key action group alert rules test.
"Explain billing" → billing cost drivers estimation optimization.

## Example Short Responses

- PagerDuty (Short): Prereqs → Service + routing key → Secure key → Action Group (webhook/Logic App) → Map severity & dedup → Alert Rules → Synthetic test.
- Azure Monitor (Short): Action Groups → Metric + KQL alerts with thresholds → Validate history → Suppression & periodic tuning.
- Billing (Short): Cost drivers (Monitor, Logs, Logic Apps, PagerDuty) → Estimate via resource + telemetry volumes → Reduce noise & ingestion → Budgets & tagging.

## Additional Resources

No supplemental markdown files in this skill currently. If future files are added, reference them here with instructions on when to open. Keep runbooks & IaC definitions current.

## Conflict Handling

If any instruction here conflicts with the main system prompt, defer to the system prompt (especially on safety, conciseness, tool name secrecy, and communication style).
