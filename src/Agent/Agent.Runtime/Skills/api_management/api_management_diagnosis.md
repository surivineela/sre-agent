# Azure API Management Diagnosis

## Overview

Supplementary diagnosis reference within the APIM skill. Provides structured evidence collection (logs, metrics, policy precedence, networking posture, backend linkage) to explain failures, latency spikes, and availability anomalies. Correlate platform signals with user‑reported symptoms—healthy platform metrics do not always mean healthy user experience.

## Inputs (Assumed from Core Skill)

- Confirmed APIM instance name (already enumerated/selected).
- Symptom summary or proactive discovery trigger.

## Capabilities

- Validate target instance and networking configuration.
- Query error logs, activity logs, and failure rates by API/operation.
- Inspect policies in correct precedence (global, API-level, operation-level).
- List and inspect APIs, operations, and their configuration.
- Retrieve recent failed requests for correlation.
- Discover backends and assess their health via ARM resource properties.
- Produce structured findings and actionable recommendations.

## Diagnostic Workflow

### 1) Instance Context Snapshot

- Retrieve instance metadata & config (tier, region, networking mode, private endpoints).
- Pull networking posture (VNet integration summary, notable NSG changes, DNS/private zone status).

Capture:

- Identifiers (name, RG, subscription, region, tier)
- Networking mode (public/VNet), private endpoints, DNS requirements
- NSG rules potentially blocking gateway/management/backend flows

### 2) Errors & Failure Rates

- Enumerate error events (status codes, time windows).
- Surface recent configuration changes overlapping symptom onset.
- Rank failing APIs/operations by failure rate, latency (P95 if available).

Focus:

- Error families (4xx vs 5xx) and spike timing
- Throughput anomalies aligning with rate limits/throttling/policy logic

### 3) Policy & Configuration Precedence

Inspect policies in strict order. Do not assume lower-level policies apply before eliminating higher-level impacts.

1. Global: cross-cutting auth, rate limits, header rewrites.
2. API: routing, transformations, auth specifics.
3. Operation: fine-grained request/response changes.

Correlate:

- Altered headers / missing body content
- Policy‑introduced timeouts/retries
- Misrouted backend or missing forward‑request block

### 4) Failed Request Correlation

- Retrieve representative failed requests (headers/payload size, response codes).
- Map to implicated policy scope, backend status, and network posture.

Check:

- Size limits
- Auth flows (JWT, OAuth, subscription keys)
- CORS/header propagation
- Rate limit / quota trigger patterns

### 5) Backend Health

Identify backend entities, capture latency/availability, verify routing block presence (forward‑request). If backend type is Function or Container App and root cause appears backend‑side, return to core skill to open corresponding backend skill.

Correlate:

- Policy vs backend origin of 4xx/5xx
- Backend scaling/outage window overlap
- DNS/private link misconfiguration

### 6) Findings Synthesis

Summarize:

- Failure rates (top endpoints)
- Activity changes correlated with onset
- Policy issues by scope
- Networking risks
- Backend health deltas

Recommend minimal corrective action; if change required → open `api_management_remediation.md`.

### When to Open Other Supplementary Files

| Trigger | Open File | Rationale |
|---------|-----------|-----------|
| Validated minimal corrective action (scaling, NSG/DNS adjustment, policy fix) with concrete parameters | `api_management_remediation.md` | Execute safe, approved change workflow |
| Backend is Function App and failed requests / latency map to function cold starts or runtime errors | `function_app` | Deeper function runtime diagnostics & scaling |
| Backend is Container App and failures tie to revision health, resource saturation, or egress issues | `container_apps` | Container runtime, scaling, networking deep dive |
| Need long-range trend / anomaly visualization beyond immediate snapshot | `metrics_and_chart_visualization` | Extended time series exploration & correlation |

Open only one new supplementary file at a time; return here if additional domains emerge.

## Reporting Guidance

- Status updates: concise reason + outcome.
- Tables: failures, policies, network, backend.
- Narrative: highlight standout signals + next recommended action.

## Internal Tooling Reference (Do Not Surface Names to User)

- Instance and network:
  - GetAPIManagementInfo
  - GetVNetConfigurationForApiManagement
  - CheckForVirtualNetworkIssues
  - GetNSGRulesForApiManagement
  - GetNSGActivityLogs
- Logs and metrics:
  - GetAPIMErrorLogs
  - GetAPIMActivityLogs
  - GetAPIMFailureRateByApiOperation
- APIs and policies:
  - GetAPIMApis
  - GetAPIDetailsByName
  - GetAPIOperationsByApi
  - GetAPIOperationDetailedInfo
  - GetGlobalApimPolicy
  - GetPoliciesByApi
  - GetPoliciesByOperation
- Failed requests and backends:
  - GetAPIMRecentFailedRequests
  - GetApplicationComponentsSummary
  - GetResourceDetailedProperties

## Best Practices

- Confirm the APIM target instance before deep dives.
- Ask for clarification when symptoms are vague or unspecified.
- Follow policy precedence (global → API → operation); avoid skipping levels.
- Correlate time windows across logs, metrics, and activity changes.
- Validate network posture early for private APIM or VNET-integrated deployments.
- Treat “APIM metrics healthy” as a signal, not proof of good user experience.
- Maintain a clear separation between diagnosis and changes; request explicit approval before any modifications.

## Cross References

- `api_management_remediation.md` – safe change execution playbooks after diagnosis.
- `function_app` – deeper runtime/backlog analysis for Function backends.
- `container_apps` – container runtime/resource diagnostics for Container App backends.
- `metrics_and_chart_visualization` – advanced trend/anomaly visualization.

## Example

Unknown multi‑API 5xx spike (abbreviated):

1. Snapshot instance + network posture.
2. Failure rate ranking (via `GetAPIMFailureRateByApiOperation`) → two APIs high 5xx.
3. Activity change (from `GetAPIMActivityLogs`): global policy edit 20m prior.
4. Policy precedence review (`GetGlobalApimPolicy`, then `GetPoliciesByApi`) → header rewrite breaking backend auth.
5. Recommend correction (open remediation file) with diff summary (policy XML before/after).
