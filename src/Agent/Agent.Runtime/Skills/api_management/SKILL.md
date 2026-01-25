---
name: api_management
description: This skill enables comprehensive orchestration and delegation of diagnostics and remediation tasks for Azure API Management services, ensuring the appropriate specialists are engaged while gathering context, supporting troubleshooting, and presenting actionable insights to users.
tools:
  - ListAPIManagement
  - SearchResource
  - GetAPIMErrorLogs
  - GetAPIMFailureRateByApiOperation
  - GetAPIMRecentFailedRequests
  - GetAPIMApis
  - GetAPIDetailsByName
  - GetAPIOperationsByApi
  - GetAPIOperationDetailedInfo
  - GetAPIMActivityLogs
  - GetAPIManagementInfo
  - GetVNetConfigurationForApiManagement
  - GetNSGRulesForApiManagement
  - GetNSGActivityLogs
  - CheckForVirtualNetworkIssues
  - GetPoliciesByApi
  - GetPoliciesByOperation
  - GetGlobalApimPolicy
  - GetResourceDetailedProperties
  - GetApplicationComponentsSummary
  - APIMModifyNSGRule
  - APIMRemoveNSGRule
  - ScaleAPIMInstance
---

# Azure API Management (APIM) Operations Skill

All markdown files in this folder collectively form a single APIM skill. This core file is the starting point; diagnosis and remediation are supplementary references you open progressively only when their triggers appear. Backend investigation (Function Apps, Container Apps) is triggered here—not inside the supplementary files.



## Purpose

Coordinate end‑to‑end APIM operational, diagnostic, and change workflows: enumerate instances, clarify symptoms, choose investigative (diagnosis) or corrective (remediation) paths, and verify outcomes. Never require the user to supply raw resource names—enumerate then confirm.


## Core Principles (inherit system prompt)

1. Safety: Explicit confirmation before any APIM write (scale, policy change, networking adjustment). No guessing parameters.
2. Accuracy: Prefer symptom clarification + evidence correlation over premature changes.
3. Conciseness: Direct answer first; only essential supporting tables.
4. Tool name usage: Skill files may reference tool names (e.g., `GetAPIMErrorLogs`) for internal clarity; user‑facing responses still describe actions (“I’ll check recent error rates”) without listing tool names.
5. Progressive discovery: Open supplementary files only when trigger conditions are satisfied; do not describe this as handoffs.


## Progressive Supplementary Discovery Map


| Situation / Need | Open Supplementary File | Trigger Summary |
|------------------|-------------------------|-----------------|
| Failures, elevated error rates, latency spikes, intermittent availability; unclear root cause | api_management_diagnosis.md | Request errors (4xx/5xx), performance degradation, missing clarity on cause |
| Confirmed root cause requiring change (scaling, policy correction, networking fix) OR user requests known change | api_management_remediation.md | Clear corrective action identified, parameters can be validated, user intends modification |
| Backend is Azure Function App and failure signals originate there | function_app skill | Backend 5xx / timeout traced to Function runtime |
| Backend is Azure Container Apps and container/resource signals implicated | container_apps skill | Container scaling/resource errors aligned with APIM failures |


### Detailed Triggers (Reference)


| File | Trigger Details |
|------|-----------------|
| api_management_diagnosis.md | Intermittent 5xx / latency spikes; user uncertain; need policy precedence analysis; suspected networking impact; lack of clear failing component |
| api_management_remediation.md | Validated issue + minimal safe change (scale units/tier, adjust NSG/DNS, correct policy ordering, backend URL/cert fix); explicit approval required |
| function_app | Backend identified as Function App with runtime errors, cold start latency, scaling misalignment, auth failures |
| container_apps | Backend identified as Container App with resource saturation, revision failures, unhealthy replicas, networking egress issues |


## Standard Operational Pattern

Plan → Enumerate → Select → Collect → Analyze → (Act) → Verify → Report.

| Phase | Purpose | Minimal Output |
|-------|---------|----------------|
| Plan | Clarify user goal + symptom summary | Goal + key symptoms |
| Enumerate | List APIM instances (name, RG, tier, region) | Table (≤5 rows) / summary |
| Select | Confirm exact instance (avoid ambiguity) | Confirmed resource line |
| Collect | Instance config, networking posture, policy scope, errors/metrics | Focused tables / bullets |
| Analyze | Correlate signals; pick diagnosis vs remediation path | Chosen path + rationale |
| Act (optional) | Safe, approved change | Change plan (≤3 lines) |
| Verify | Post‑state vs expected | Delta summary |
| Report | Direct answer + evidence + next step | 1–2 sentences + table |


## Instance Enumeration & Selection

1. Enumerate all accessible APIM instances (names, resource groups, tiers, regions). If >5, summarize count + top 5 (e.g., production tiers first) and request user selection.
2. Confirm selected instance explicitly; echo key identifiers.
3. If user gives vague “APIM issue” → ask for symptom dimensions: error type, latency, timeframe, impacted APIs.


## Evidence Categories

- Instance metadata: tier, region, networking mode (public/VNet), private endpoints.
- Policy scope: global vs API vs operation (do not assume precedence—verify).
- Error logs & failure rates: status code distributions, top failing operations.
- Activity changes: recent configuration or policy updates aligning with symptom onset.
- Backend linkage: referenced backend entities + basic health markers.
- Networking posture: NSG rules, DNS/private zone status, VNet integration checks.


## Path Decision (Diagnosis vs Remediation)

- Choose diagnosis when signals are ambiguous, multi‑layered, or user lacks clear cause.
- Choose remediation only after a root cause or safe corrective hypothesis is validated and parameters are concrete.
- If corrective action emerges during diagnosis, switch path and open remediation file.


## Validation & Confirmation Pattern

For any write:

1. State change (scope + element).
2. Impact (latency, availability, cost, risk) + rollback option.
3. Ask for explicit yes/no.
4. Execute single change → verify → summarize.

Template:

```text
Planned Change: <concise>
Impact: <effect>
Rollback: <how>
Proceed? (yes/no)
```

Verification snippet:
```text
Before: <key=value>
After: <key=value>
Result: <match | mismatch + next step>
```


## Supplementary File Summaries

- `api_management_diagnosis.md`: Deep evidence collection & policy/network/backends correlation workflow.
- `api_management_remediation.md`: Safe change execution playbooks (scaling, networking, policy corrections, backend alignment).


## Communication Pattern

Direct answer first (“The gateway latency spike aligns with a recent global policy change—diagnosis required”). Follow with minimal supporting data (tables for errors, policy diff summary). No tool names.


## Quick Examples


Latency spike with uncertain cause:


1. Enumerate instances; confirm production APIM.
2. Collect failure rates + activity logs (policy update 15 min prior).
3. Open diagnosis file; inspect global policy; identify misordered header enrichment before validate-jwt.
4. Recommend policy correction (remediation trigger).

Known scaling request:


1. Enumerate + confirm instance.
2. Collect current units/tier + throughput symptoms.
3. Present scale plan (increase units) with impact + rollback.
4. Execute after approval; verify latency normalization.


## Out of Scope

- Creating new APIM instances or full migration projects.
- Destructive deletions (must be explicitly authorized; default deny here).
- Non-APIM backend deep dives (handled in their respective backend skills once triggered).


## Completion Checklist (Internal)

Answer first line; instance confirmed; path rationale clear; no user-facing tool names; change (if any) approved & verified; evidence concise.

## Instructions and Best Practices

- Always enumerate and describe APIM instances before asking the user to choose a target. Avoid assuming the resource.
- Confirm selection and repeat back the resource details to prevent misconfiguration.
- Favor symptom-first questioning: “What failures are you seeing?”, “Which APIs or operations are affected?”, “Since when?”, “Any recent changes?”
- Keep the user informed with succinct summaries and clear next steps.
- Maintain a separation of concerns: Investigation steps in the diagnosis module; changes in the remediation module with explicit approval.
- After any remediation, perform verification steps (health checks, sample requests, key metrics) and report outcomes.


## Examples


### Example 1: Vague issue

- User: “I’m having issues with my APIM.”
- Steps:

  1) Enumerate APIM instances; ask user to pick target.
  2) Confirm selection; collect instance config + recent error/failure signals.
  3) Clarify symptoms (errors, latency, time window, affected APIs).
  4) Choose diagnosis path; open `api_management_diagnosis.md`.

### Example 2: Known fix request

- User: “Scale my production APIM; it’s under heavy load.”
- Steps:

  1) Enumerate instances; confirm production target.
  2) Collect current tier/units + throughput metrics.
  3) Present scale plan (units/tier change) + impact + rollback; request approval.
  4) Open `api_management_remediation.md`; execute change after approval.
  5) Verify latency and error rates normalize.
