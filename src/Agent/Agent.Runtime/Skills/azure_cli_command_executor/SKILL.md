# Azure CLI Command Executor Skill

## Overview
Provides concise, safe guidance for executing Azure CLI read and write operations. Emphasizes: validated context, clear separation of read vs write, minimal change scope, explicit approval before any modification, and privacy/safety aligned with the system prompt hierarchy (Safety → Accuracy → Conciseness → Efficiency).

## Core Principles

1. Read operations (list/show/get/non‑mutating) are SAFE: execute after validating subscription + resource scope.
2. Write / destructive operations (create, update, scale, restart, security rule changes) require explicit approval (plan + impact + rollback) before execution.
3. Never guess syntax—consult help (`GetAzCliHelp`) for write operations or unfamiliar flags.
4. Prefer resource IDs over name + group; always include `--subscription` when ambiguity possible.
5. One write at a time; verify before initiating another.
6. Deletions only on explicit request with irreversible impact disclosed; otherwise recommend portal for high‑risk deletes.
7. Mask secrets / sensitive values; never echo credentials, keys, tokens, connection strings.
8. Internal tool names allowed in this file for clarity; never surface them directly to the user.
9. Escalate after two identical failures (concise explanation + needed input or manual check).

## Internal Tooling Reference (Not surfaced verbatim to user)

| Purpose | Tool |
|---------|------|
| Read commands | RunAzCliReadCommands |
| Write commands | RunAzCliWriteCommands |
| Command help lookup | GetAzCliHelp |
| External docs search (fallback) | SearchDocuments |

## Progressive Discovery & When to Open Other Skills

Open supplementary skills only when a trigger below is met. Provide the trigger rationale first, then load exactly one additional skill; return here if another domain arises.

| Trigger (Observed Need) | Action | Rationale |
|-------------------------|--------|-----------|
| Missing or ambiguous resource scope after 2 read attempts (ID, subscription, type) | Use built‑in discovery tools (ListSubscriptions, ListResourceGroups, SearchResource, GetResourceIdForResourceName) | Establish precise scope before further CLI actions |
| Need multi‑metric trend or anomaly validation beyond immediate CLI output (e.g. sustained CPU spike justification before scaling) | Open `metrics_and_chart_visualization` | Time‑series analysis and correlation prior to modification |
| Performance degradation cause unclear (utilization vs configuration) | Open `metrics_and_chart_visualization` | Deep metric inspection to separate config vs load issues |

## Minimal Operational Workflow

1. Clarify Intent: Resource(s), desired outcome, urgency. Distinguish READ vs WRITE.
2. Context Validation: Subscription present? Resource ID available? If not → invoke built‑in discovery tools (ListSubscriptions → select; ListResourceGroups / SearchResource → locate; GetResourceIdForResourceName → resolve ID) before proceeding.
3. Baseline Reads: Execute targeted list/show commands (use `--query` for focus) to collect current state.
4. State Analysis: Summarize key properties vs desired outcome (e.g., instances 2 → need 4).
5. Help Lookup (WRITE only): Use `GetAzCliHelp` to confirm required parameters + flags; stop after logical scope exhaustion.
6. Plan Draft: Exact command, impact (availability, performance, cost, security), rollback command.
7. Approval Gate: Present plan; execute only after explicit yes.
8. Execute Write: Single command via `RunAzCliWriteCommands` (optionally `--no-wait` with status plan).
9. Verification: Follow‑up reads; confirm before/after diff; if async, poll status responsibly.
10. Report: Concise outcome + next recommendation or closure.

## Command Construction Guidelines

| Goal | Pattern | Notes |
|------|---------|-------|
| Read (single) | az [group] show --ids [resourceId] | Prefer --ids for precision |
| Read (list) | az [group] list -g [rg] --subscription [subId] | Apply --query to narrow |
| Targeted property | az [group] show --ids [id] --query "[jsonPath]" | Keep JSON path short |
| Existence check | az [group] show --ids [id] 2>/dev/null \|\| echo "Not found" | Avoid false positives |
| Update | az [group] update --ids [id] --set key=value | Single key change where possible |
| Scale | az webapp scale --ids [id] --instance-count [n] | Verify plan limits first |
| Long running | [command] --no-wait + status read plan | Communicate polling strategy |

Use `GetAzCliHelp` before unfamiliar create/update/scale operations or when a required flag is uncertain.

## Approval Package Template (WRITE)

```text
Action: <concise description>
Resource(s): <IDs>
Current State: <key properties>
Proposed Change: <exact CLI command>
Impact: <availability | performance | cost | security | blast radius>
Rollback: <CLI command or reversal steps>
Proceed? (yes/no)
```

## Risk Dimensions (Assess briefly for each WRITE)

- Availability (downtime / transient impact)
- Performance (latency, throughput shift)
- Cost (tier/instance changes)
- Security (access scope, network exposure)
- Blast Radius (scope of effect)
- Reversibility (ease + time to rollback)

## Error Handling (Condensed)

| Failure Type | Recovery |
|--------------|---------|
| Not Found | Confirm RG/name; use --ids; run built‑in discovery tools if still missing |
| Auth / Permission | Explain; user validates role; halt write plan |
| Invalid Parameter | Re‑read help; correct flag; show minimal diff |
| Timeout / Transient | Retry once; if repeat → escalate |
| Unknown | Provide concise summary; suggest doc search (SearchDocuments) |

Stop after 2 identical failures; escalate with concise summary + required user input.

## Privacy & Sensitive Data

Mask: secrets, tokens, keys, connection strings, password fields. Limit output to required properties; prefer focused `--query` projections.

## Examples

### Scaling a Web App (WRITE)

Read current state:

```shell
az webapp show --ids <webAppResourceId> --query "{instances:siteConfig.numberOfWorkers,plan:serverFarmId}" --subscription <subId>
```

Read plan limits:

```shell
az appservice plan show --ids <planResourceId> --query "{tier:sku.tier,max:maximumNumberOfWorkers}" --subscription <subId>
```

Draft approval package (target instances 3). Execute only after approval:

```shell
az webapp scale --ids <webAppResourceId> --instance-count 3 --subscription <subId> --no-wait
```

Verification:

```shell
az webapp show --ids <webAppResourceId> --query "siteConfig.numberOfWorkers" --subscription <subId>
```

### Tightening NSG Rule

Baseline:

```shell
az network nsg rule show --ids <nsgRuleResourceId> --query "{name:name,source:sourceAddressPrefixes,destPort:destinationPortRange}" --subscription <subId>
```

Approval package (restrict RDP to 203.0.113.0):

```shell
az network nsg rule update --ids <nsgRuleResourceId> --source-address-prefixes 203.0.113.0 --subscription <subId> --no-wait
```

Verification:

```shell
az network nsg rule show --ids <nsgRuleResourceId> --query "sourceAddressPrefixes" --subscription <subId>
```

## Reporting Format (WRITE Completion)

```text
[OK] <Action> on <resourceId(s)> at <UTC timestamp>
Before: <key=value, ...>
After:  <key=value, ...>
Verification: <result>
Next: <monitoring or closure>
```

## Cross References

- `metrics_and_chart_visualization` – extended metric/time‑series analysis prior to or after scaling/performance adjustments.

## Out‑of‑Scope

In‑cluster Kubernetes (kubectl), az aks command invoke, raw pod/container inspection—redirect to AKS or relevant skill if needed.

## Conciseness Reminder

Answer first (1–2 sentences) → focused data (queries, diffs) → next step only if action pending.
