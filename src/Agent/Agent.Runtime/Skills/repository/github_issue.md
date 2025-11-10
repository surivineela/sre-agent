# GitHub Issue Management

## Overview
Create and manage GitHub issues to track technical incidents, application errors, infrastructure problems, deployment failures, and security events. Capture detailed error context (exceptions, full stack traces, console logs), verify repository access, avoid duplicates, apply labeling, notify stakeholders, and return the created issue URL and identifier. Include correlated source code analysis and any observed configuration or infrastructure drift to support rapid diagnosis and remediation. Persist audit data for traceability.

Use for:
- System outages and service degradation
- Application errors and exceptions
- Deployment failures and configuration mismatches
- Infrastructure problems and suspected drift
- Security incidents and policy violations

Note: Repository linking between Azure resources and GitHub is not supported. When linking is requested, proceed with issue creation without establishing a repository link and communicate this limitation.

## Capabilities
- Identify the appropriate GitHub repository for the issue and verify access.
- Check for existing issues to prevent duplicates or to update ongoing threads.
- Create detailed GitHub issues with structured sections and rich error context.
- Apply labels, assign owners, and mention stakeholders for visibility.
- Link related issues and reference commits, PRs, or runs.
- Return the issue URL, status, and tracking number.
- Persist audit records (operation, status, repository, issue number/URL, correlationId, actor, and error details).
- Document suspected IaC drift for Azure workloads (e.g., APIM, container apps) by inspecting repository IaC definitions and highlighting discrepancies.

## Prerequisites
- Determine the target repository and confirm access.
- Collect incident context: error messages, exceptions, stack traces, logs, timestamps, impacted services, and any relevant Azure resource IDs.
- Generate or propagate a correlationId for end-to-end tracing across actions.
- Ensure provider detection has classified the repository as GitHub.

## Instructions and Best Practices

### Repository Selection and Access
- Identify the correct repository based on the service/component involved. Prefer the code repository where the failing component is maintained.
- If multiple repos are plausible, choose the one with the most relevant ownership, recent activity, or IaC for the affected resources.
- Verify repository access before attempting creation. If access fails or is not granted:
  - Notify the user of the access issue and the required permissions.
  - Do not block the overall workflow; continue with other diagnostic steps as applicable.
  - Record the failure in the audit with actionable details.

### Duplicate Detection
- Search open issues with:
  - Key error signatures (exception types, error codes, messages).
  - Component/service identifiers.
  - Recent time window around the incident.
- If a match exists:
  - Comment with new context (timestamps, environment, logs), link evidence, and update labels/assignees as needed.
  - Record audit with operation “UpdateExistingIssue” and the issue number.
- If no match exists, proceed to create a new issue.

### Issue Composition
Create a structured issue using the following sections. Include as many as are relevant:

- Title
  - Use a concise format: [Impact] Component – Symptom (Key Error/Code) – Environment
- Summary
  - One-paragraph description of what happened, where, when, and current status.
- Impact
  - Affected users/services, scope, severity, regions/environments, and timelines.
- Evidence
  - Exceptions with full stack traces, console logs, telemetry snippets, and error screenshots or links.
  - Include error instance IDs, request IDs, and correlationId.
- Correlated Source Code
  - Summarize the suspected code areas, files, or methods that map to the error.
  - Provide links to lines or search results.
  - When deeper analysis is needed, load the source_code_analysis skill to perform semantic searches and correlation across the codebase. Summarize findings and include permalinks to files/lines.
- IaC and Configuration Drift
  - Determine the likely IaC used for the repository (e.g., Bicep, Terraform, ARM).
  - State the mechanism used to determine the IaC:
    - “Determined via GitHub Embeddings API” or
    - “Determined via grep-based search for IaC signatures”
  - For Azure resources such as API Management (APIM) or container apps, compare declared settings (e.g., CPU, memory, instance counts) in IaC against observed runtime/resource values. Note discrepancies clearly.
- Timeline
  - Discovery time, first failure, mitigations attempted, current state.
- Environment and Config
  - Service version, deployment hashes, feature flags, configuration deltas, dependency versions.
- Related Work
  - Link to related issues, incidents, pull requests, CI/CD runs, dashboards, or alerts.
- Proposed Next Actions
  - Immediate mitigations, debugging steps, owners, and ETA for follow-ups.

Apply labels such as area/service, severity, environment (prod/stage), incident/bug, and add assignees and mentions for owners and on-call rotations.

### APIM and IaC Drift Checks
- Retrieve bicep (or other IaC) files relevant to APIM or the impacted resource.
- Compare CPU, memory, and instance settings declared in IaC against observed values in Azure.
- Document any mismatches under “IaC and Configuration Drift” with concrete before/after values and the detection mechanism used.

### Resource Linking
- Repository-to-resource linking is not supported for GitHub.
- If asked to link or unlink:
  - Explain the limitation and proceed with issue creation and documentation.
  - If linking is required for process reasons, suggest tracking linkage context within the issue (resourceId and environment) and consider Azure DevOps linking as an alternative where appropriate.

### Notifications and Routing
- Mention team aliases or owners.
- Apply routing labels for triage boards.
- If the incident spans multiple repos, create primary issue in the owning repo and open linked issues or tasks in dependent repos as needed.

### Failure Handling
- If issue creation fails (e.g., permissions, API errors, rate limits):
  - Notify the user with the explicit error and recommended resolution steps.
  - Record a detailed audit (status: Failed) including exception message, status code, request ID if available, and correlationId.
  - Continue with other non-blocking steps where feasible (e.g., provide a draft issue body in the conversation to allow manual creation).

### Success Handling
- On success, return the issue URL and number.
- Record audit (status: Succeeded) with repository, issue number, labels applied, and correlationId.

### Audit and Traceability
- Use a consistent correlationId across discovery, duplicate checks, creation, and updates.
- Capture actor identity where available.
- Store verbose error details to aid troubleshooting.
- Ensure timestamps are accurate and machine-parseable.

## Examples

### Example: Create a New Incident Issue
1. Determine target repo and verify access.
2. Search for duplicates using key error text “Request timeout after 30 seconds” and component name.
3. No duplicates found. Compose issue with sections:
   - Summary: Timeouts observed on Checkout API in prod since 12:05 UTC.
   - Impact: ~12% requests failing, region: East US.
   - Evidence: Stack trace snippet and request IDs, correlationId=ab12-...
   - Correlated Source Code: Link to HttpClientFactory configuration; note default timeout is 10s.
   - IaC and Configuration Drift: Determined via grep-based search; Bicep shows minReplicas=1 while observed instances=0 at incident onset.
   - Timeline and Proposed Next Actions.
4. Apply labels: severity/P1, area/checkout, env/prod; assign @oncall.
5. Create the issue, return URL and number, and persist audit.

### Example: Update Existing Issue
1. Find existing issue “APIM 503 errors – Backend timeout – prod”.
2. Add comment with new error IDs, timeframe extension, and logs.
3. Update labels (add “investigating”), assign API team.
4. Persist audit with operation “UpdateExistingIssue”.

## Integration with Other Skills and Files

### Source Code Analysis (top-level skill: source_code_analysis)
- Load when deeper semantic analysis is required to map exceptions and symptoms to code.
- Use to:
  - Find method implementations and usage sites for erroring components.
  - Correlate stack traces to files and lines.
  - Identify configuration sources (timeouts, connection strings, retries).
- Summarize findings under “Correlated Source Code” with links.

### Recording Infrastructure Changes for GitOps Consistency
- When remediation plans imply infrastructure changes (scaling, config updates, resource modifications), record them using [change_propagation.md](change_propagation.md).
- Use to:
  - Identify the IaC type (Terraform/Bicep/ARM/Helm/K8s).
  - Structure change records for later implementation and repository sync.
- Do not implement changes here; only document and format them for audit and follow-up.

## Minimal Issue Template (copy into the issue body)
- Summary
- Impact
- Evidence
  - Exceptions
  - Stack traces
  - Console logs
  - CorrelationId
- Correlated Source Code
- IaC and Configuration Drift (include detection mechanism: Embeddings vs grep)
- Timeline
- Environment and Config
- Related Work
- Proposed Next Actions

## Notes
- Prefer permalinks to specific lines/commits to avoid drift.
- Keep sensitive data out of public issues; sanitize logs and secrets.
- If the repository is private, ensure viewers have access or mirror sanitized details in a secure channel.
