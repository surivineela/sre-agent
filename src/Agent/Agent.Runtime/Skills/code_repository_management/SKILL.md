---
name: code_repository_management
description: |
  Load when you need to: (a) discover whether an Azure resource has a connected code repository, (b) link or unlink an Azure DevOps repository (GitHub linking not supported), or (c) create tracking artifacts (GitHub Issue / Azure DevOps Work Item) with rich incident context and audit records.
tools:
  - GetRepositoryType
  - CreateGithubIssue
  - FetchGithubIssue
  - FindConnectedGitHubRepo
  - GetIaCForGitHub
  - DisconnectRepositoryFromResourceForGitHub
  - CreateAzureDevOpsWorkItem
  - CreateAzureDevOpsWorkItemWithoutResourceLinkage
  - GetIaCForAzureDevOps
  - FindConnectedRepositoryForAzureDevOps
  - ConnectRepositoryToResourceForAzureDevOps
  - DisconnectRepositoryFromResourceForAzureDevOps
  - FetchGithubIssuesLimited
---

# Repository Management Skill

## Overview
Manage connections and interactions between Azure resources and code repositories (GitHub and Azure DevOps). Detect repository provider from the URL, discover the currently connected repository, create tracking artifacts (GitHub Issues or Azure DevOps Work Items), and connect/unlink repositories where supported. Always capture audit records for traceability.

## Pre-Work Checklist

- Confirm and normalize the Azure Resource ID using the system prompt's built-in discovery workflow (no separate resource_discovery skill).
- Identify repository provider (GitHub or Azure DevOps) from the repository URL using GetRepositoryType.
- Validate prerequisites (permissions, repository access, resource linkage eligibility).
- Determine the desired operation (discover, connect, disconnect, create issue/work item).
- Plan audit data to persist (operation, status, resourceId, repositoryUrl, provider, correlationId, and details).

## Capabilities

- Connect or unlink a repository from an Azure resource (Azure DevOps connections supported; GitHub connections not supported—notify accordingly).
- Discover the currently connected repository for a given Azure resource.
- Create GitHub Issues or Azure DevOps Work Items with contextual details.
- Persist audit records for discovery, connection/disconnection, and issue/work item creation.

## Instructions and Best Practices

### Repository Provider Detection

- Use GetRepositoryType to classify the repository provider from its URL (GitHub vs Azure DevOps).
- Validate the URL shape (GitHub: github.com/org/repo; Azure DevOps: dev.azure.com/org/project).

### Resource Context Gathering

- Always obtain and validate the full Azure Resource ID using the built-in discovery workflow prior to any repository action.
- Ensure the resource exists and is accessible within the current subscription/tenant context.

### Operation Guidance

- Discover Connected Repository:
  - Collect the resource ID (normalized) and query the current linkage.
  - Return the repository URL if found; otherwise notify that none is linked.
  - Persist a discovery audit record including timestamp, operation (Discover), status, resourceId, repositoryUrl or null, provider if known, and correlationId.

- Connect a Repository:
  - Only Azure DevOps connections are supported.
  - If the requested repository is GitHub, notify that connections are not supported and provide alternatives (e.g., create GitHub issues without linking).
  - For Azure DevOps, call ConnectRepositoryToResourceForAzureDevOps with the required inputs.
  - Persist a connection audit event: operation (Connect), status, resourceId, repositoryUrl, provider, remote linkage id if returned, actor, correlationId, and error details when relevant.

- Disconnect/Unlink a Repository:
  - Accept both provider types for unlinking.
  - Perform unlink operation and confirm removal.
  - Persist a disconnect audit event mirroring the connection audit structure with operation (Disconnect).

- Create Tracking Artifacts:
  - After provider detection, choose the appropriate action:
    - GitHub: CreateGithubIssue
    - Azure DevOps: CreateAzureDevOpsWorkItem
  - Include complete error context where applicable: exceptions, stack traces, console logs, and configuration discrepancies.
  - Persist an audit record capturing success/failure, remoteId (issue number or work item id) on success, or error details on failure.

### Tool Call Discipline

- Before any significant tool call, state one line describing its purpose and list minimal required inputs.
- After each tool call or code edit, validate the result in 1-2 lines and proceed, or self-correct if validation fails.

### Audit and Traceability

- Use consistent correlationIds across related operations.
- Include actor identity when available.
- Store error details verbosely for troubleshooting.
- Ensure timestamps and statuses are accurate and machine-parseable.

## Examples

### Example: Discover Connected Repository

Query: Find the connected repository for the Azure Resource with ID /subscriptions/.../containerapp.

Steps:

1. Purpose: Discover and normalize resource context. Inputs: partial resource identifier.
   - Normalize the resource ID using the built-in discovery workflow.
   - Validate: Confirm the resourceId is complete and points to an existing resource.
2. Purpose: Determine repository provider. Inputs: repository URL (if available from linkage).
   - Use GetRepositoryType to identify whether the repository is GitHub or Azure DevOps.
   - Validate: URL conforms to provider’s expected format.
3. Return the repository URL or notify if none is found.
4. Persist a discovery audit record (timestamp, operation: Discover, status, resourceId, repositoryUrl or null, provider if known, and correlationId).

### Example: Create Issue/Work Item

Query: Create a Work Item for the Azure Resource with ID ...

Steps:

1. Purpose: Normalize resource context. Inputs: raw resource identifier.
   - Normalize the resource ID using the built-in discovery workflow.
   - Validate: resourceId completeness and accessibility.
2. Purpose: Determine provider. Inputs: repository URL or intended target repo.
   - Use GetRepositoryType to classify GitHub vs Azure DevOps.
   - Validate: URL structure matches provider.
3. If GitHub, use CreateGithubIssue; if Azure DevOps, use CreateAzureDevOpsWorkItem.
   - Include error context (exceptions, stack traces, logs) and configuration details as available.
   - Validate: tool response includes remoteId (issue number or work item id) or an actionable error.
4. Persist an audit record capturing success/failure, remoteId on success, or error details on failure.

### Example: Connect a Repository

Query: Connect the Azure Resource with ID ... to an Azure DevOps repo (e.g., dev.azure.com/org/project/_git/repository)

Steps:

1. Purpose: Normalize resource context. Inputs: raw resource identifier.
   - Normalize the resource ID using the built-in discovery workflow.
   - Validate: resourceId correctness.
2. Only Azure DevOps connections are supported.
   - If a GitHub URL is provided, notify that repository linking is not supported for GitHub.
3. For Azure DevOps, use ConnectRepositoryToResourceForAzureDevOps.
   - Validate: linkage established and a remote linkage id (if applicable) returned.
4. Communicate success/failure and persist a connection audit event (operation: Connect or Disconnect, status, resourceId, repositoryUrl, provider, remote linkage id if returned, actor, correlationId, and error details when relevant).

## Additional Resources

### Azure DevOps Work Item Management

Use when formal Azure DevOps tracking is required for incidents, deployment failures, infrastructure drift, security events, or configuration mismatches. This supports:

- Detailed work items with error context (exceptions, stack traces, console logs).
- Verification of IaC configurations (e.g., bicep) and detection of deployment drift.
- Repository-to-resource linking/unlinking operations.
- Structured formatting for optimal Azure DevOps rendering (sections like Details, IaC Discrepancy, Recommendations).
- Returns work item URLs and creation status.

Read [azuredevops_work_item.md](azuredevops_work_item.md) for comprehensive guidance.

### GitHub Issue Management

Use for incident tracking in GitHub repositories: outages, application errors, deployment failures, security incidents, and status checks. This supports:

- Creating detailed issues with full error context.
- Fetching existing issues to check for duplicates or updates.
- Labeling, stakeholder notifications, and repository access verification.
- Linking related issues and handling both success and failure scenarios.

Read [github_issue.md](github_issue.md) for detailed instructions.

### Change Recording

Open `change_propagation.md` after remediation actions (scaling, configuration changes, resource adds/deletes) or when IaC drift is detected, to produce a structured change record for later reconciliation.

### Top-Level Skills

- source_code_analysis
   - Load for deeper semantic exploration of code (stack trace mapping, locating implementations, architecture understanding) when enriching issues or work items.
