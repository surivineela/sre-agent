# Change Propagation

## Overview
Record and format all infrastructure and application modifications identified or executed during remediation so they can be synchronized back to source repositories. Capture the exact change, its rationale, context, and the corresponding Infrastructure as Code (IaC) or configuration file locations that must be updated. Provide before/after states and concrete repository update instructions. Do not apply changes; only document and format them for human review and follow-up.

Use this process whenever remediation actions include:
- Scaling (replicas, SKU/size, CPU/memory)
- Configuration updates (timeouts, connection strings, feature flags)
- Resource additions/removals/renames
- Network/security policy adjustments (NSG, firewall, ingress)
- Deployment manifest updates (Helm values, Kubernetes YAML, ARM/Bicep/Terraform)

Maintaining accurate change propagation preserves GitOps consistency and audit trails.

## Capabilities
- Track infrastructure/application changes with full context (what, where, why, when).
- Identify the IaC/config source of truth: Terraform, Bicep/ARM, Helm charts/values, plain Kubernetes manifests, or app config files.
- Map runtime or console-driven changes to the precise repository path(s) requiring updates.
- Produce structured change records with before/after states and line-level update instructions.
- Group related changes into a batch with linkage to incident tickets and remediation notes.

## Required Inputs
- Change trigger/context: incident ID, remediation step, hypothesis, validation evidence.
- Target environment(s): prod/staging/dev, region(s), namespace/resource group.
- Resource identity: service name, resource name, type, and unique identifiers as applicable.
- Runtime evidence: commands executed, control-plane events, metrics/logs supporting the change.
- Repository discovery hints: prior issues/PRs, ownership docs, known repo paths, IaC conventions.

## Workflow
1. Capture change events
   - Monitor remediation actions from the conversation and execution logs.
   - Log each action as a change candidate with timestamp, actor, and rationale.

2. Normalize and scope
   - Determine resource type and scope (e.g., Azure App Service Plan, Kubernetes Deployment).
   - Confirm environment, region, and ownership boundaries to locate the correct repository.

3. Identify source of truth
   - Determine IaC/config mechanism:
     - Terraform (.tf)
     - Bicep/ARM (.bicep/.json)
     - Helm (Chart.yaml, values.yaml, templates)
     - Kubernetes YAML (deployments, configmaps, secrets, ingress)
     - Application config files (appsettings, env files)
   - Record the discovery method (semantic search vs. grep/pattern search) and confidence.

4. Locate file(s) and current state
   - Find the relevant file(s) and section(s). Capture the current (“before”) values with line numbers or anchors if feasible.
   - If unknown, record “before” as unknown and include a task to fetch it.

5. Define the desired “after” state
   - Specify the concrete target value(s) as required by remediation.
   - Include constraints or validation criteria (e.g., minReplicas >= 2).

6. Generate repository update instructions
   - Provide exact file path(s), section keys, and line-level guidance if stable.
   - Include a minimal diff or patch-like instruction set when helpful.
   - Note related files that may also require updates (variables, outputs, documentation).

7. Produce the structured change record
   - Use the schema in “Structured Change Record Format.”
   - Batch related changes and link to the incident and validation plan.

8. Link to tracking and follow-up
   - Reference the incident tracking entry and any mitigation PRs/issues.
   - If a GitHub issue is required or already exists, link it. For issue instructions, read [github_issue.md](github_issue.md).

## Structured Change Record Format
Use a consistent, machine- and human-readable structure. Prefer YAML; JSON is acceptable. One record per change, with optional batching.

Example YAML schema:
- change_id: stable unique identifier (ULID/UUID/timestamp-based)
- timestamp_utc: ISO 8601
- actor: “automation” or user identifier
- rationale: brief explanation tied to incident or performance metrics
- environment: prod|staging|dev
- scope:
  - cloud: azure|aws|gcp|k8s|onprem
  - region: e.g., eastus
  - namespace_or_rg: k8s namespace or Azure resource group
  - service: logical service name
- resource:
  - kind: e.g., KubernetesDeployment | AzureAppServicePlan | APIM | StorageAccount
  - name: resource name
  - identifiers: optional IDs/ARNs
- iac:
  - type: terraform|bicep|arm|helm|k8s|appconfig
  - discovery_method: embeddings|grep|manual
  - repo: org/repo
  - path: path/to/file
  - additional_paths: [optional related files]
- change:
  - field: e.g., replicaCount | sku_name | resources.limits.cpu
  - before: previous value or unknown
  - after: new desired value
  - diff: optional minimal diff/patch
- validation:
  - checks: list of metrics/logs/commands to confirm success
  - rollback_ready: true|false
  - rollback_instructions: brief revert plan if applicable
- related:
  - incident_ids: [list]
  - issue_urls: [list]
  - pr_urls: [list]
- notes:
  - risks: brief risk analysis
  - followups: tasks/todos

## IaC and File Mapping Guidance
- Terraform
  - Search for modules/resources by type and name (e.g., azurerm_app_service_plan).
  - Update variables.tf or environment-specific tfvars as appropriate.
  - Maintain consistency across workspaces; note drift if runtime differs from tf state.
- Bicep/ARM
  - Prefer editing Bicep sources over compiled ARM templates.
  - Update parameter files if environment-specific overrides exist.
  - For new resources, include required properties (location, sku, tags) and parameterization strategy.
- Helm
  - Prefer values.yaml for environment-specific changes; avoid hardcoding in templates.
  - If templates require logic changes, document templating updates with Helm version compatibility.
- Kubernetes manifests
  - For Deployments, adjust spec.replicas and spec.template.spec.resources.
  - For ConfigMaps/Secrets, record key-level changes; never include secret values—reference secret names or placeholders.
- Application config
  - Identify environment-specific config sources (.env, appsettings.{env}.json).
  - Document key paths and default vs. override precedence.

## Output Conventions
- Always include before and after values; if before is unknown, state unknown and add a retrieval task.
- Use explicit file paths from repository root.
- Provide line numbers when stable; otherwise, include a robust anchor (YAML path, JSON path, or HCL block identifier).
- Redact secrets; use placeholders and note secret management system (e.g., Key Vault).
- Keep instructions minimal and actionable; prefer a single change per record unless tightly coupled.

## Examples

### Kubernetes Helm Chart Update
- Context: Increased replicas due to high CPU.
- Mapping:
  - iac.type: helm
  - path: helm/microservice-api/values.yaml
- Change:
  - field: replicaCount
  - before: 3
  - after: 8
  - repository_update: Update helm/microservice-api/values.yaml line 12: change replicaCount from 3 to 8
- Validation: CPU stabilizes < 70% over 15 minutes; no pod restarts.
- Rollback: restore replicaCount to 3 if error rates increase.

Example record:
change_id: ulid_01HZY4W6T0F9Q3
timestamp_utc: 2025-10-24T09:35:00Z
actor: automation
rationale: Mitigate elevated CPU and 5xx errors during incident INC-2025-1024
environment: prod
scope:
  cloud: k8s
  region: eastus
  namespace_or_rg: payments
  service: payments-api
resource:
  kind: KubernetesDeployment
  name: payments-api
iac:
  type: helm
  discovery_method: embeddings
  repo: org/app-infra
  path: helm/microservice-api/values.yaml
change:
  field: replicaCount
  before: 3
  after: 8
validation:
  checks:
    - Deployment readyReplicas == 8
    - Error rate < 5% for 15m
  rollback_ready: true
  rollback_instructions: Revert replicaCount to 3
related:
  incident_ids: [INC-2025-1024]
  issue_urls: []
  pr_urls: []
notes:
  risks: Increased cost; ensure HPA thresholds align
  followups:
    - Review HPA settings for autoscaling

### Terraform Azure Resource Scaling
- Context: Scale App Service Plan for performance.
- Mapping:
  - iac.type: terraform
  - path: terraform/azure/app-service.tf
- Change:
  - field: sku_name
  - before: S1
  - after: P1v2
  - repository_update: Update terraform/azure/app-service.tf line 25: change sku_name from "S1" to "P1v2"
- Validation: Latency p95 < 500ms; CPU < 70%.
- Rollback: revert sku_name to S1 if costs outweigh benefits.

### ARM/Bicep Resource Addition
- Context: New storage account for backups.
- Mapping:
  - iac.type: bicep
  - path: bicep/storage/backup-storage.bicep
- Change:
  - field: resource.add
  - before: none
  - after: storage account with Standard_LRS
  - repository_update: Add new resource block to bicep/storage/backup-storage.bicep with Standard_LRS; parameterize name and tags.
- Validation: Backup job succeeds; secure access enabled.
- Rollback: remove resource definition and dependent references.

### ConfigMap Update
- Context: Switch database endpoint to secondary.
- Mapping:
  - iac.type: k8s
  - path: k8s/configmaps/app-config.yaml
- Change:
  - field: data.database_url
  - before: prod-db-primary.example.com
  - after: prod-db-secondary.example.com
  - repository_update: Update k8s/configmaps/app-config.yaml line 8: change database_url endpoint
- Validation: App connects to secondary; error rate decreases.
- Rollback: revert to primary endpoint when safe.

## Instructions and Best Practices
- Record every remediation change, even if temporary, and mark intended reversion.
- Prefer updating the true source of truth; note if runtime hotfix diverges from IaC.
- Use environment-appropriate files (values.prod.yaml, prod.tfvars) when they exist.
- Avoid speculative values; if uncertain, flag as TODO with required verification steps.
- Preserve idempotency and ordering: document dependencies and prerequisite changes.
- Note potential side effects (cost, performance, quotas) and required approvals.
- Keep change records small and testable; batch only tightly coupled modifications.
- Link change records to issues/PRs and incidents for traceability.

## Failure and Constraint Handling
- Repository or file not found
  - Record suspected repository and paths; include discovery steps taken and confidence level.
  - Provide fallback instructions (search terms, owners to contact).
- Insufficient permissions
  - Document limitation; include full change record so a maintainer can proceed.
- Unknown “before” state
  - Mark before as unknown; add task to retrieve current file content or tf state.
- Secrets and sensitive data
  - Redact values; reference key names or secret store entries instead.

## Validation and Follow-Up
- Define measurable success checks for each change (metrics, logs, resource states).
- Include rollback instructions for each change.
- Create or update a tracking issue with the change records and next steps. For issue creation and status tracking, read [github_issue.md](github_issue.md).

## Related Resources
- For incident tracking and stakeholder notifications associated with these changes, refer to [github_issue.md](github_issue.md).
