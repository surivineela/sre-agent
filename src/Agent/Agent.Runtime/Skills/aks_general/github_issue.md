# GitHub Issue Management

## Overview
Create and maintain high-quality GitHub issues for technical incidents and operational problems. Capture full error context (exceptions, stack traces, console logs), verify repository access, select the appropriate repository, check for duplicates, apply labels, link related issues, and notify stakeholders. Provide issue URLs and status updates. Use this capability for outages, application errors, infrastructure problems, deployment failures, security incidents, and any situation that requires formal tracking and collaboration.

## Capabilities
- Create detailed GitHub issues with complete diagnostic context.
- Select the correct repository and verify access; handle both success and failure scenarios.
- Search for and manage duplicates; update existing issues when appropriate.
- Apply labels, assign owners, add milestones, and notify stakeholders.
- Link related issues and pull requests for end-to-end traceability.
- Link or unlink Azure resources to/from GitHub repositories when needed for traceability.
- Record status updates and return canonical issue URLs and identifiers.

## When to Use
- Always open an issue for any incident investigation or remediation effort that impacts reliability, performance, security, deployment, or infrastructure state.
- Use for:
  - System or service outages
  - Application errors and exceptions
  - Infrastructure configuration or runtime problems
  - Deployment and CI/CD failures
  - Security incidents and vulnerabilities
  - Requests for change where formal tracking is required

## Workflow

1. Define Scope and Target Repository
   - Identify the system/component affected, severity, and business impact.
   - Determine the most relevant repository based on ownership of the failing component, deployment manifests, IaC source, or service code.

2. Verify Access and Gather Context
   - Confirm repository access; if access fails, notify the user of the failure and continue capturing incident details locally.
   - Collect error details from available context: exceptions, full stack traces, console logs, command outputs, timestamps, affected regions/namespaces, and user impact.

3. Check for Duplicates
   - Search existing issues by keywords (error messages, services, components) and labels.
   - If a duplicate exists, update the existing issue with the new evidence and timeline instead of creating a new one.

4. Identify Infrastructure as Code (IaC) and Propagation State
   - Determine the IaC mechanism used in the repository. State explicitly how it was determined:
     - GitHub Embeddings-based semantic search for IaC indicators (e.g., Terraform, Bicep, ARM, Helm, Kubernetes manifests).
     - Grepping the repository for file patterns: *.tf, *.bicep, *.json (ARM), Chart.yaml, templates/, kustomization.yaml, *.yaml for Kubernetes, etc.
   - For Azure API Management (APIM) resources managed via Bicep:
     - Retrieve relevant Bicep files and evaluate parameters/variables and modules for cpu, memory, and instance counts.
     - Compare intended values in Bicep with observed runtime state (from validated evidence) to identify drift.
     - Summarize any mismatches to include in the issue.

5. Draft the Issue
   - Use a clear, action-oriented title with service/component and symptom.
   - Include structured sections (see Template below) with complete, unredacted error context where appropriate.
   - Correlate errors with source code where possible (see “Correlate Source Code” section).
   - Add labels (incident, sev, component, area, security, deployment, infrastructure), assign owners, and set milestones if known.
   - Link related incidents, PRs, and change records.

6. Correlate Source Code
   - Analyze the error text and stack frames to locate relevant code paths and configuration.
   - Provide a brief analysis of likely root causes, known hotspots, and candidate changes.
   - Use the source_code_analysis skill for semantic exploration and to extract relevant snippets and references.

7. Submit and Return URL
   - Create the issue and capture the resulting URL and identifier.
   - If issue creation fails or repository access is insufficient:
     - Inform the user clearly about the failure and any returned error messages.
     - Proceed with the workflow by presenting the drafted issue content so it can be filed manually.

8. Maintain and Update
   - Add status updates as new evidence emerges.
   - Link remediation steps and change records. For write-only infrastructure changes, record details using [change_propagation.md](change_propagation.md) to maintain auditability and GitOps alignment.

## Instructions and Best Practices
- Be exhaustive with error context: include full stack traces, raw console logs, and command outputs when safe and appropriate.
- Avoid paraphrasing critical error messages; include exact text to support searchability and diagnosis.
- Prefer specific, stable labels for service, area, and severity to drive triage workflows.
- Use clear reproduction steps and environment details (region, cluster, namespace, commit SHA, deployment version).
- Explicitly document the mechanism used to identify IaC (Embeddings vs grepping) and summarize findings.
- Highlight configuration drift between IaC (e.g., Bicep) and runtime state with precise field/value comparisons.
- Keep updates incremental and timestamped; maintain a concise timeline of events.
- Respect security and privacy guidelines when including logs or credentials—never include secrets.
- If repository selection is uncertain, document the rationale and propose alternatives.

## Issue Template (Copy and Adapt)
Title: [Service/Component] <Symptom/Failure> in <Environment/Region> since <Timestamp>

Summary
- Impact: <users/SLAs/transactions> affected
- Scope: <service/component/namespace/cluster>
- First observed: <timestamp> | Current status: <investigating/mitigating/resolved>
- Severity: <SEV-1/2/3> | Priority: <P0/P1/P2>

Environment
- Subscription/Project: <id/name>
- Region: <region>
- AKS Cluster/Namespace (if applicable): <cluster>/<namespace>
- Version/Commit: <tag/commit SHA/build id>
- Related deployment: <pipeline run/PR link>

Observed Errors and Evidence
- Exception(s): <full exception text>
- Stack Trace(s): 
  ```
  <full stack trace(s)>
  ```
- Logs/Console Output:
  ```
  <relevant raw logs>
  ```
- Metrics/Symptoms: <CPU/memory/errors/restarts/latency> with timestamps

Reproduction Steps
1. <step>
2. <step>
3. <expected vs actual results>

IaC and Configuration State
- Detection method: <GitHub Embeddings API | grep patterns>
- IaC type(s): <Terraform/Bicep/ARM/Helm/Kubernetes>
- APIM Bicep review (if applicable): <file paths, parameters> — Observed runtime vs IaC:
  - cpu: <IaC value> vs <observed>
  - memory: <IaC value> vs <observed>
  - instances: <IaC value> vs <observed>
- Potential drift or mismatches: <summary>

Correlated Source Code
- Key files and functions: <paths, methods>
- Relevant snippets and lines: <links/blocks>
- Hypotheses: <likely cause/config hotspot>
- References: <docs/PRs/issues>

Mitigation and Next Steps
- Immediate actions taken: <steps, timestamps>
- Proposed actions: <investigation, remediation, validation>
- Owners: <team/usernames>
- Tracking links: <related issues/PRs/change records>

Timeline
- <timestamp> — <event/observation/action>
- <timestamp> — <event/observation/action>

Labels and Metadata
- Labels: <incident, sev-X, area-*, component-*, security, deployment, infra>
- Assignees/Reviewers: <usernames>
- Milestone: <sprint/release>

## Handling Failures and Access Issues
- If repository fetching, authorization, or issue creation fails:
  - Present the exact error returned.
  - Notify the user of the failure and provide the fully prepared issue body for manual filing.
  - Continue with investigation tasks and status reporting without blocking on automation.

## Correlating Errors with Source Code
- Use error messages, stack traces, and file/line hints to locate implementations and configuration.
- For common scenarios:
  - NullReferenceException at line N in File.cs: locate the method, analyze nullability and guards.
  - API timeouts: inspect HTTP client configuration, retry and timeout settings.
  - Database connection failures: review connection strings, credential sources, retry/backoff logic.
  - Container start failures: analyze Dockerfile, entrypoint, health checks, and deployment manifests.
  - 401/403 errors: verify authentication middleware, token validation, and identity configuration.
- For deep exploration and semantic matches across large codebases, use the source_code_analysis skill.

## Linking Azure Resources and Repositories
- When needed for traceability, link or unlink Azure resources to the repository:
  - Document the resource ID, repository name, branch, and scope of linkage.
  - Ensure appropriate permissions and audit logs.
  - Reference links in the issue for end-to-end traceability.

## Related Resources
- Change Propagation and Recording: Use [change_propagation.md](change_propagation.md) to record all write-only infrastructure modifications identified or performed during remediation for GitOps consistency and audit trails.
- Source Code Analysis: Use the source_code_analysis skill to semantically explore code, locate implementations, and correlate incidents with source lines and configurations.

## Examples

Example 1: Deployment Failure (Container CrashLoopBackOff)
- Title: payments-api CrashLoopBackOff after v1.12.3 rollout in prod-eu2
- Key Evidence: Pod restarts > 20, stack trace pointing to startup config parsing, container logs show missing env var.
- IaC: Detected via grep; Helm chart values show missing env mapping; drift confirmed.
- Actions: Rolled back deployment; recorded change details; linked PR fixing values.yaml; notified on-call and SRE.
- Outcome: Issue URL returned; labels incident, sev-2, component-payments, deployment.

Example 2: Security Incident (Unauthorized Access)
- Title: 401 Unauthorized on user profile API in staging after token validation changes
- Evidence: Authentication middleware logs, exact error messages, failing route, related commit SHA.
- IaC: Detected via embeddings; Bicep for APIM shows policy mismatch with runtime.
- Actions: Added detailed reproduction; linked source code analysis findings; assigned to identity team.

Example 3: Infrastructure Drift (APIM Instances)
- Title: APIM instances mismatch between Bicep (2) and runtime (1) in prod
- Evidence: Bicep parameters indicate instances=2; observed runtime shows instances=1.
- Actions: Documented drift; attached plan to reconcile; recorded via [change_propagation.md](change_propagation.md); notified service owners.
