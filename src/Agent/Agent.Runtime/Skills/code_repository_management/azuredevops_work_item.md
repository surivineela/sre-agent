# Azure DevOps Work Item

## Overview
Create and manage Azure DevOps (AzDo) Work Items that capture technical incidents, operational issues, and infrastructure changes with complete, actionable context. Include exceptions, full stack traces, console logs, and Infrastructure as Code (IaC) discrepancies. Automatically discover connected repositories, verify IaC configurations (bicep), detect deployment drift between Azure resources and declared configurations, and format Work Item descriptions using clean HTML for optimal AzDo rendering. Support repository-to-resource linking/unlinking. Return Work Item URLs and creation status. Use for system outages, application errors, infrastructure drift detection, deployment failures, security incidents, configuration mismatches, and resource connectivity issues.

## Capabilities
- Create AzDo Work Items with rich incident context:
  - Exceptions, full stack traces, console logs, environmental details, and reproduction steps (when available).
- Enrich Work Items with IaC insights:
  - Identify IaC files in the repository (focus on bicep) and detect drift in cpu, memory, and instance counts.
- Discover and validate repository context:
  - Find the repository connected to a given Azure resource.
- Repository connections:
  - Connect or disconnect an Azure resource to/from an AzDo repository.
- Produce HTML-formatted descriptions with clear sections and emojis for readability.
- Return Work Item URLs and capture creation status.
- Maintain auditability with correlation IDs, timestamps, outcomes, and error details.

## Prerequisites and Inputs
- Azure Resource ID (partial or full). If partial, first normalize and validate.
- Target AzDo repository URL (optional if a resource has a connected repo).
- Incident artifacts when available: exception text, stack trace, logs, configuration snippets, and observed vs expected behavior.
- Correlation ID for audit continuity.

## Workflow and Best Practices

### 1) Resource and Repository Context
- Normalize and validate the Azure Resource ID first. If not available or incomplete, obtain and validate it.
  - Use the system prompt's built-in discovery workflow (resource_discovery skill removed) to obtain the full resource ID, verify existence, and confirm access context.
- Determine the target AzDo repository:
  - If the repository is not provided, attempt to find the repository connected to the resource (FindConnectedRepository).
  - If no connected repository is found, proceed with Work Item creation using the provided repository (if any). If none is available, return a clear notification indicating that a repository is required or must be discovered.

### 2) Create the Work Item First
- For any incident or operational investigation, create an AzDo Work Item promptly to establish an audit trail and collaboration anchor.
- Include all known incident details:
  - Exceptions, full stack trace, console logs.
  - Resource ID and operation context.
  - Timestamps and correlation ID.
- Validate the tool response:
  - Confirm that a Work Item ID/URL is returned or capture and report error details.
- Persist an audit record with operation (CreateWorkItem), status, resourceId, repositoryUrl, workItemId/URL, and correlationId.

### 3) Optional IaC Enrichment (Do Not Block on Failure)
- Attempt to identify and retrieve IaC files relevant to the resource (GetIaCForAzureDevOps).
  - Document that files were identified via repository search/grep.
- If IaC files are found:
  - Analyze bicep files for cpu, memory, and instance count values.
  - Compare against the current Azure resource state to detect drift or mismatches.
- If IaC calls fail or no IaC files are found:
  - Continue without IaC enrichment. Do not retry IaC calls in subsequent attempts to avoid repeated failures.
- Add any discovered IaC discrepancies to the Work Item by updating its description (using the HTML format in the next section).
- Persist an audit record for IaC enrichment attempts (success/failure, details).

### 4) Repository Linking/Unlinking (When Requested)
- Connect an Azure resource to an AzDo repository:
  - Use ConnectRepositoryToResourceForAzureDevOps with the resourceId and repository URL.
  - Validate that linkage is established and record any linkage identifier returned.
  - Persist a connection audit event (operation: Connect, status, resourceId, repositoryUrl, provider, remote linkage id if available, actor, correlationId, errors if any).
- Disconnect/unlink a resource from an AzDo repository:
  - Perform the unlink and confirm removal.
  - Persist a disconnect audit event mirroring the connection event (operation: Disconnect).

### 5) Results and Traceability
- Return the Work Item link and status.
- Use consistent correlation IDs across related discovery, connection, IaC, and work item creation steps.
- Store error details verbosely (exceptions, stack traces, logs) for troubleshooting.
- Ensure timestamps and statuses are machine-parseable.

## Work Item Description Formatting (HTML Only)
- Use HTML (not markdown) for the Work Item description.
- Structure the description into clear sections with emojis for scannability:
  - Title: Begin with an emoji and concise summary.
  - Sections (examples):
    - 📝 Details
    - 🔍 IaC Discrepancy
    - 💡 Recommendation
- HTML conventions:
  - Use <h2>, <h3> for section headings.
  - Use <ul> and <li> for lists.
  - Use <pre> or <code> to format stack traces, logs, and code snippets.
  - Use <b> or <strong> to highlight resource IDs, timestamps, action items.
  - Ensure well-formed HTML that renders cleanly in AzDo Work Items.
- Do not use markdown syntax such as **bold**, backticks, or fenced code blocks.

### Example HTML Skeleton
- Title example: "🚨 Incident: API Failure in albumapicsharp-2"

- Description example body:
  <h2>📝 Details</h2>
  <ul>
    <li><b>Resource ID:</b> /subscriptions/.../resourceGroups/.../providers/Microsoft.App/containerApps/albumapicsharp-2</li>
    <li><b>Operation:</b> Deployment</li>
    <li><b>Timestamp:</b> 2025-01-15T18:42:07Z</li>
    <li><b>Correlation ID:</b> 123e4567-e89b-12d3-a456-426614174000</li>
  </ul>
  <h3>Exception</h3>
  <pre>System.InvalidOperationException: Failed to connect to database
   at MyApi.Controllers.AlbumsController.Get() in /src/Controllers/AlbumsController.cs:line 42
   ... (stack trace) ...
  </pre>
  <h3>Logs</h3>
  <pre>[18:41:59 INF] Starting deployment...
[18:42:01 ERR] Connection timeout ...
  </pre>

  <h2>🔍 IaC Discrepancy</h2>
  <ul>
    <li><b>cpu</b>: bicep=0.5; actual=1.0</li>
    <li><b>memory</b>: bicep=1Gi; actual=2Gi</li>
    <li><b>instances</b>: bicep=2; actual=4</li>
  </ul>
  <pre>// modules/containerapp.bicep (excerpt)
containerapp {
  template {
    // ...
    cpu: 0.5
    memory: '1Gi'
    replicas: 2
  }
}
  </pre>

  <h2>💡 Recommendation</h2>
  <ul>
    <li>Align bicep with the current scaled configuration or scale resource back to declared values.</li>
    <li>Record changes for GitOps and open a follow-up PR to synchronize IaC.</li>
  </ul>

## Tool Call Discipline
- Before each tool call, state one sentence describing its purpose and list minimal inputs.
- After each tool call, validate the result in 1–2 lines; proceed or self-correct as needed.
- Do not repeatedly attempt the same IaC retrieval on failure; skip IaC in subsequent steps.

## Examples

### Create an AzDo Work Item for an Incident

1. Normalize the resource context (built-in discovery workflow); confirm existence and access.
2. Determine or discover the AzDo repository (find connected repository if not supplied).
3. Create the Work Item (CreateAzureDevOpsWorkItem) with emoji title + HTML description (Details, Exception, Logs, IaC Discrepancy if any, Recommendation).
4. Optionally enrich with IaC (single attempt); add drift findings if discovered.
5. Return the Work Item URL and persist audit details.

### Connect an Azure Resource to an AzDo Repository

1. Normalize resource ID (built-in discovery workflow).
2. Invoke ConnectRepositoryToResourceForAzureDevOps with resourceId and repository URL.
3. Validate linkage and persist a connection audit record (operation, status, resourceId, repositoryUrl, provider, linkage id if available, actor, correlationId).

### Disconnect an Azure Resource from an AzDo Repository

1. Normalize resource ID (built-in discovery workflow) and confirm current linkage.
2. Perform unlink; validate removal.
3. Persist a disconnect audit record mirroring the connection audit fields.

## Additional References

### Change Recording for Infrastructure Modifications

- When remediation or scaling actions are identified, record write-only infrastructure changes for GitOps consistency and audit trails.
- Read [change_propagation.md](change_propagation.md) for:
  - Capturing all modifications (scaling, configuration updates, resource changes).
  - Identifying IaC type (Terraform/Bicep/ARM/Helm/K8s).
  - Producing structured change records suitable for repository synchronization.
- Use after detecting drift or recommending changes to ensure a complete audit chain.

### Resource Context

Use the system prompt's discovery workflow to normalize/validate resource IDs and gather basic context; no separate discovery skill is required.
