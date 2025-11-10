# Azure API Management (APIM) Remediation

## Overview

Implement safe, scoped changes to Azure API Management (APIM) resources to resolve diagnosed issues. Prioritize explicit confirmation, complete parameters, transparent risk/benefit disclosure, and post-change verification. Execute only clearly justified actions and document all changes.

## Capabilities

- Execute APIM remediations for:
  - Networking: NSGs, subnets, private endpoints, DNS resolution.
  - Policy logic: incorrect/ordering issues, unintended side effects, gateway/runtime errors.
  - Backend/API connectivity: base URLs, certificates, TLS/HTTP versions, timeouts, retries.
  - Capacity and performance: scale adjustments aligned to usage and limits.
- Validate inputs, present a minimal-risk plan, request explicit approval, execute, and verify.
- Log actions, including before/after states and verification results.

## Preconditions and Mandatory Inputs

Before any remediation:

- Confirm target APIM instance (name, resource group, region, tier).
- Validate all necessary parameters are complete and unambiguous.
- If any parameter is missing or unclear, pause and request clarification.
- Never proceed with assumptions; confirm exact values.

Recommended input checklist:

- APIM instance details (name, resource group, tier).
- Concise problem statement and root cause (from diagnosis or user).
- Scope of impact (APIs/operations, environments, time window).
- Proposed change and expected outcome.
- Constraints (maintenance windows, change freezes, risk tolerances).

## Safety and Approval Gates

For every proposed change, disclose:

- What will change (configuration element, scope, target).
- Why it is necessary (diagnosed issue, expected improvement).
- Expected outcome (functional/operational result).
- Risks:
  - Downtime or transient unavailability.
  - Access or routing loss due to networking/NSG/DNS changes.
  - Policy side effects or behavioral changes.
  - Cost and performance considerations (tier/unit changes can affect billing).
- Benefits:
  - Restored connectivity or functionality.
  - Improved reliability, performance, or security.
  - Alignment with best practices and future resilience.

Obtain explicit user approval before executing any change.

## Execution Workflow

- Receive a clear diagnostic input (e.g., “NSG blocks outbound HTTPS traffic”).
- Validate the issue and define a minimal, safe change.
- Present a remediation plan with risks/benefits and request approval.
- Communicate time expectations: “This operation may take a moment to complete. I’ll update you once it’s done.”
- Execute using authorized tools/APIs.
- Verify outcomes and document results.
- Provide a Change Summary (see template below).

## Change Summary (Post-Remediation)

Include after each change:

- What changed and why.
- Before and after state.
- Verification steps performed and results.
- Follow-up actions or monitoring guidance.

Example structure:

- Change: [Describe the exact configuration change]
- Reason: [Diagnosed cause and objective]
- Before: [Key values]
- After: [Key values]
- Verification: [Checks performed, metrics/logs reviewed, results]
- Follow-up: [Additional monitoring or next steps]

## Domain Playbooks

### 1) Scaling APIM Capacity

Use when:

- Sustained high CPU, throughput, or gateway capacity pressure.
- Throttling or increased latency due to insufficient capacity.
- Planned traffic increases (launches, events).

Inputs:

- Current tier/sku, region, gateway/unit count, observed load/metrics.
- Target capacity or minimum acceptable performance goals.
- Change window constraints and rollback plan.

Plan and validate:

- Confirm current tier limits and supported scale operations.
- Assess architecture impacts (multi-region, VNet, private endpoints).
- Review cost impact (tier or unit changes affect billing).

Procedure:

- Present the scale plan (from X to Y units or tier change), with risks/benefits.
- Obtain approval.
- Apply the scale change using `ScaleAPIMInstance`.
- Monitor gateway readiness and instance health (sample latency/error rates).

Verification:

- Check instance count/tier reflects the change.
- Validate representative API calls (success rate, latency).
- Monitor metrics for stabilization.

Change Summary:

- Include pre/post unit counts or tier values, relevant metrics before/after.

### 2) Networking Remediation

Common issues:

- NSG rules blocking outbound HTTPS or inbound gateway/control plane.
- Subnet misconfiguration or insufficient address space.
- Private endpoint DNS resolution missing/wrong (A/AAAA records).
- Incorrect service endpoints or UDRs blackholing traffic.

Inputs:

- Network topology (VNet/subnet names, NSG associations, UDRs).
- Required endpoints/ports (gateway, backend, Azure control plane).
- Private endpoint FQDNs and expected DNS zones/records.

Procedure (examples):

- NSG outbound HTTPS blocked:
  - Identify effective NSG rules on APIM subnet (`GetNSGRulesForApiManagement`).
  - Add/adjust rule to allow outbound TCP 443 to required destinations (`APIMModifyNSGRule`).
  - Preserve least-privilege and rule ordering; remove obsolete deny if justified (`APIMRemoveNSGRule`).
- Private endpoints DNS:
  - Confirm private DNS zone linkage.
  - Add/update A records for required FQDNs pointing to private endpoint IPs.
  - Flush/validate DNS resolution from APIM subnet.
- Subnet/UDR:
  - Verify UDR next-hops; ensure required traffic is not blackholed.
  - Adjust routes to permit required Azure services and backend targets.

Verification:

- Test DNS resolution (FQDN -> expected private IP).
- Validate TCP connectivity paths if applicable.
- Execute representative API calls; confirm success and latency normalization.
- Recheck NSG effective rules.

Change Summary:

- Document rule/record/route changes, before/after and test outcomes.

### 3) Policy Logic Corrections

Common issues:

- Misordered or conflicting policies (e.g., validate-jwt before setting required headers).
- Overly restrictive rate-limits or quotas.
- send-request side effects causing latency/failures.
- Incorrect set-backend-service target.

Inputs:

- Target API/operation/product scope.
- Current policy XML (effective and source of truth).
- Error signatures (HTTP status, traces, correlation IDs).

Procedure:

- Pull current effective policy (`GetGlobalApimPolicy`, then `GetPoliciesByApi` / `GetPoliciesByOperation`).
- Identify incorrect or risky elements and determine minimal correction.
- Stage changes in the lowest-impact scope first (operation > API > global).
- Present a diff and expected impact; obtain approval.
- Apply the change; ensure XML is valid and references (certs, keys) exist.

Verification:

- Test affected operations with representative requests.
- Observe runtime traces and metrics (latency, error rates).
- Confirm no unintended side effects on unrelated operations.

Change Summary:

- Include policy diffs and test evidence.

### 4) Backend/API Connection Alignment

Common issues:

- Incorrect base URLs or paths.
- TLS/certificate issues (expired, untrusted, wrong SNI).
- Timeouts/retries misaligned with backend behavior.
- Missing or invalid credentials/headers.

Inputs:

- Backend base URL, protocol, and auth method.
- Certificates (thumbprints, expiry), TLS versions, cipher requirements.
- Expected timeouts/retries.

Procedure:

- Validate backend settings in APIM (backend entity, API settings, server certs) via `GetAPIMApis`, `GetAPIDetailsByName`, `GetApplicationComponentsSummary`.
- Update base URL or set-backend-service if misaligned.
- Import/update certificates and bind references in policies.
- Adjust timeout/retry policies to match backend SLOs.
- Confirm credentials are present and correctly referenced (named values/secrets).

Verification:

- Perform end-to-end requests to the backend through APIM.
- Validate TLS handshake success and certificate chain.
- Check latency and error rates over a sampling window.

Change Summary:

- Capture configuration fields changed and validation steps/results.

## Verification and Post-Change Checks

- Functional: representative API calls succeed across affected scopes.
- Performance: latency, throughput, and error rates return to normal bounds.
- Availability: no increased 5xx or timeout spikes after change.
- Observability: logs/traces reflect expected behavior; DNS and connectivity validated.
- Rollback: if verification fails or regressions occur, roll back promptly and document.

## Communication and Transparency

- Announce time expectations before execution: “This operation may take a moment to complete. I’ll update you once it’s done.”
- Be explicit about risks, benefits, and trade-offs.
- Keep the user in control with approval gates for each change step.
- Stay within authorized tools and change scope.

## Examples

### Example: Allow outbound HTTPS for backend connectivity

- Issue: Backend calls failing due to NSG denying outbound 443.
- Plan:
  - Add NSG rule to allow outbound TCP 443 to backend FQDN/IP.
  - Minimal scope, least-privilege destination, correct priority.
- Risks:
  - Potential unintended access if destination is too broad.
- Benefits:
  - Restores backend connectivity through APIM.
- After approval:
  - Implement rule and verify DNS/connection and API success.
- Change Summary:
  - Before: Rule priority 4000 denies outbound 0.0.0.0/0:443.
  - After: New allow rule priority 3000 to backend IP/CIDR on 443.
  - Verification: API 200 OK, latency normalized.

### Example: Correct policy order for JWT validation

- Issue: validate-jwt executing before header enrichment required by backend.
- Plan:
  - Reorder policies: set-header then validate-jwt.
- Risks:
  - If header adds sensitive data, ensure proper scoping and masking.
- Benefits:
  - Prevents false negatives in JWT validation and restores request flow.
- After approval:
  - Apply policy change at API scope; test affected operations.
- Change Summary:
  - Before/After: Policy XML diff.
  - Verification: Error rate reduced, successful auth observed.

## Cross References

- `api_management_diagnosis.md` – diagnosis workflow used before corrective changes.
- Core APIM skill (`SKILL.md`) – instance enumeration, trigger decisions for opening this file.

### When to Open Other Supplementary Files

| Trigger | Open File | Rationale |
|---------|-----------|-----------|
| Cause unclear / conflicting signals emerge mid-planning | `api_management_diagnosis.md` | Gather deeper evidence prior to change |
| Backend is Function App and runtime/cold start issues dominate | `function_app` | Specialized function runtime remediation |
| Backend is Container App with revision failures or resource saturation | `container_apps` | Container runtime scaling & networking fixes |
| Need multi-day performance baseline comparison before scaling | `metrics_and_chart_visualization` | Long-range metrics correlation |
