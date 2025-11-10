# GitHub Issue Management

## Overview
Create and maintain high-quality GitHub issues to formally track technical incidents and operational problems. Capture full error context (exceptions, stack traces, console logs), verify repository access, avoid duplicates, apply labels, notify stakeholders, and link related issues. Provide URLs and tracking references for ongoing status checks. Use this process for system outages, application errors, infrastructure problems, deployment failures, security incidents, or any scenario requiring formal tracking and collaboration.

## Capabilities
- Create detailed issues in the appropriate repository with complete incident context.
- Search for and update existing issues to avoid duplicates.
- Select the correct repository based on service ownership and runtime signals.
- Apply labels, milestones, assignees, and project links according to conventions.
- Link related issues and PRs; include cross-repo references when relevant.
- Verify repository access and permissions; document any authorization constraints.
- Connect or disconnect an Azure resource to a GitHub repository for traceability.
- Return issue URLs, identifiers, and status updates.

## Required Inputs
- Incident summary: what happened, when, impact, current status.
- Evidence: errors, exceptions, full stack traces, console/service logs, metrics.
- Target service/app and environment; suspected repository or organization.
- Stakeholders: on-call owner(s), team aliases, communication channels.
- Labeling scheme or project details (if known).
- Any related issue/PR links or tracking IDs (alerts, incident IDs).

## Execution Workflow
1. Plan and scope
   - Clarify incident type, affected service(s), impact, and time window.
   - Identify the most likely repository and fallback candidates.
   - Define success criteria (issue created or updated with full context; stakeholders notified).

2. Repository selection and access
   - Locate the repository using service ownership metadata, org naming, and prior issues/PRs.
   - Validate access (read/write/issue creation). If restricted, record the limitation and proceed with guidance.

3. Duplicate check
   - Search issues (open and recent closed) by service, error signature, incident time, and key terms.
   - If a match exists, update that issue with new evidence and timeline. Otherwise proceed to new issue creation.

4. Evidence gathering
   - Aggregate exceptions, full stack traces, error snippets, and console logs.
   - Summarize recent relevant metrics (error rates, latency, saturation) where available.
   - Provide context: first occurrence, frequency, blast radius, rollback/mitigation attempts.

5. IaC identification and configuration deltas
   - Determine IaC mechanism used by the repository. Explicitly record the discovery mechanism:
     - GitHub embeddings-based semantic search, or
     - Grep-based search for known IaC patterns and file paths.
   - Prioritize Bicep files when present. Check for configuration values relevant to the incident (CPU, memory, instance counts/replicas).
   - Compare IaC-defined values with observed/runtime configuration. Note any discrepancies and include them in the issue.

6. APIM-specific checks (when applicable)
   - For Azure API Management-related services, inspect Bicep (or IaC) files for APIM configuration.
   - Identify changes or drift in CPU, memory, and instance settings that may not be reflected at runtime.
   - Document suspected causes and required follow-up actions.

7. Correlate errors to source code
   - Correlate stack traces and error messages to the codebase. Identify modules, methods, or configuration implicated.
   - Summarize findings in a “Correlated Source Code” section (function/file, relevant lines, suspected cause).

8. Compose and create or update the issue
   - Use the Issue Template structure below.
   - Apply labels (e.g., incident, sev level, service name), assign owners, add milestones/projects as per conventions.
   - Submit and capture the issue URL and ID.

9. Stakeholder notification
   - Mention relevant teams/aliases in the issue.
   - Provide the issue URL for status tracking.

10. Post-action validation
   - Confirm issue creation/update success by returning the URL and ID.
   - If creation or access fails, notify the user, record the failure reason, and provide next steps or a fallback plan.

## Issue Template
Title
- [Service] Short incident description | Impact | Date/Time (UTC)

Body
- Summary
  - What happened, when it started, current status
  - Impacted users/scope; severity level
- Timeline (UTC)
  - t0: detection/event
  - t1: first response
  - t2: mitigation attempts
  - tN: current state
- Evidence
  - Exceptions and full stack traces (fenced code blocks)
  - Console/service logs (snippets with timestamps)
  - Metrics (error rate, latency, CPU/memory; include time ranges)
- Correlated Source Code
  - Files and functions implicated; line ranges; configuration keys
  - Hypothesized root cause with brief rationale
- IaC Findings
  - IaC mechanism discovery: “Embeddings search” or “Grep-based search”
  - Files: paths to Bicep/Terraform/ARM/Helm manifests
  - Config deltas: CPU/memory/replica counts; note drift vs runtime
- APIM (if applicable)
  - Bicep/ARM settings relevant to APIM; suspected drift or misconfiguration
- Related Items
  - Linked issues/PRs; alerts/incident IDs; dashboards
- Actions and Next Steps
  - Immediate mitigations, rollback plan, validation checks
  - Owners, ETA, and dependencies
- Labels/Meta
  - Labels applied; assignees; project/milestone
- Tracking
  - Issue URL, number; external ticket references if any

## Best Practices
- Always create or update an issue for incident-class events, even during ongoing investigations.
- Prefer updating an existing matching issue to prevent fragmentation.
- Include full stack traces and the smallest viable log snippets with timestamps and correlation IDs.
- Clearly state how IaC was identified (embeddings vs grep), and show exact file paths and configuration keys.
- Call out configuration drift between IaC and runtime; propose verification steps to reconcile.
- Keep the title concise and searchable; include service name and impact.
- Use consistent labels and severities; align with incident management conventions.
- Mention responsible teams to ensure timely response and ownership.
- Maintain a factual timeline; avoid speculative statements unless clearly labeled as hypothesis.

## Handling Failures and Constraints
- Repository not found or insufficient permissions: document the limitation, provide the expected target repo and required permission, and share the full issue body for a human to post.
- API errors during issue creation: return the error, retain the composed issue content, and suggest retry or alternative repository.
- Missing evidence: proceed with available information, explicitly list unknowns, and add a task to collect missing logs/metrics.
- Confidential information: redact secrets, tokens, customer PII, or credentials before posting.

## Connect/Disconnect Azure Resource Traceability
- Connect a relevant Azure resource to the repository when traceability is needed (e.g., automated syncs or environment mapping).
- Disconnect links that are stale or incorrect, and record the rationale and new linkage if applicable.
- Document any changes in the issue timeline and ensure stakeholders are notified.

## Examples

Example: New Incident Issue
- Title: [Payments-API] Elevated 5xx after deployment | 20% error rate | 2025-10-24
- Key contents:
  - Evidence: stack traces from checkout handler; logs showing “Request timeout after 30 seconds”
  - Correlated Source Code: HTTP client timeout set to 30s in HttpClientFactory config; DB retry policy disabled
  - IaC Findings: Discovered via embeddings; Bicep shows minReplicas=1 but runtime shows 0 during scale-down; CPU=250m, Memory=512Mi
  - Actions: Roll back to previous revision; raise PR to set timeout to 10s with retries; align minReplicas=2 in Bicep

Example: Update Existing Issue
- Locate existing incident with matching signature.
- Add new logs and a timeline update.
- Link the mitigation PR and note post-deploy validation steps.

## Related Resources
- Change propagation records and GitOps alignment: read [change_propagation.md](change_propagation.md) to document any write-only infrastructure changes discovered or recommended during remediation.
- Deep source code correlation and semantic exploration: use the top-level skill source_code_analysis for locating implementations, understanding architecture, and mapping errors to specific code regions.
