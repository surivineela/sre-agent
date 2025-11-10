# Change Propagation

## Overview
Record write-only infrastructure and application changes made during remediation so they can be synchronized back to source repositories. Capture and format all modifications (scaling, configuration updates, resource additions/deletions) as structured change records suitable for GitOps workflows and audit trails. Identify the Infrastructure as Code (IaC) technology (Terraform, Bicep, ARM, Helm, Kubernetes manifests) and provide precise repository update guidance. Do not apply changes; only record and format them for human or automated follow-up.

Use when:
- Remediation actions altered runtime state (e.g., scaled a service, changed configuration, created a resource).
- Drift from declared IaC was detected and needs reconciliation.
- A durable audit trail and repository sync plan is required.

## Capabilities
- Track all remediation actions and resulting state deltas.
- Identify source repositories and specific files requiring updates (Terraform .tf, Bicep .bicep, ARM templates, Helm values.yaml, Kubernetes YAML).
- Generate structured change records with before/after states and recommended code edits.
- Provide file-level guidance including paths, keys/attributes, suggested diffs, and (when available) line numbers or JSONPath/YAMLPath selectors.
- Maintain traceability with correlation IDs, timestamps, actors, environments, and related work items.

## Workflow
1. Collect context
   - Inputs: correlationId, timestamps, environment (prod/staging/dev), resource identifiers, remediation intent, observed change outcome.
   - Gather evidence: current resource state, prior desired state (from IaC if available), logs, commands that were executed.

2. Identify IaC ownership
   - Determine the IaC technology per resource:
     - Terraform: .tf modules, variables, state mappings.
     - Bicep/ARM: .bicep or .json templates and parameters files.
     - Helm/Kubernetes: Chart files and values.yaml; raw k8s YAML manifests for ConfigMaps/Secrets/Deployments.
   - Map runtime resource to IaC files using known conventions, module paths, naming, tags/labels/annotations, or repository metadata.

3. Detect change deltas
   - For each affected resource or configuration:
     - Determine the before state (declared or last-known desired).
     - Determine the after state (current runtime state post-remediation).
     - Classify change type: scale up/down, configuration update, parameter change, resource create/delete, image/version change, secret rotation, etc.

4. Produce structured change records
   - Create a machine- and human-readable record that includes:
     - Resource identity and scope.
     - IaC ownership and repository impact list.
     - Before/after values.
     - Required file edits with precise selectors and suggested diffs.
     - Validation steps, risk assessment, and backout/rollback suggestions.

5. Present repository update guidance
   - For each impacted file, specify:
     - File path (relative to repo root).
     - Target key/attribute setting or resource block.
     - Action (update/add/remove/rename).
     - Suggested new value(s) and minimal diff.
     - Optional: line numbers if reliably known; otherwise use selectors (YAMLPath/JSONPath/HCL address).
   - Recommend creating a PR with clear title, description, and links to the incident/work items.

6. Traceability and linkage
   - Include correlationId, initiating actor, timestamps, and related work item links (e.g., Azure DevOps Work Item URL).
   - If coordination with GitHub issues is required for tracking or stakeholder notification, read [github_issue.md](github_issue.md) to create or update issues and attach the change record.

## Structured Change Record Format
Produce a record that downstream systems can ingest. Prefer JSON; YAML is acceptable. Include at least:

```json
{
  "changeId": "uuid-1234",
  "timestamp": "2025-01-15T18:42:07Z",
  "correlationId": "123e4567-e89b-12d3-a456-426614174000",
  "environment": "prod",
  "actor": "automated-remediation",
  "summary": "Scale container app replicas from 2 to 4 due to sustained CPU > 80%",
  "resources": [
    {
      "type": "Microsoft.App/containerApps",
      "id": "/subscriptions/.../resourceGroups/.../providers/Microsoft.App/containerApps/albumapicsharp-2",
      "name": "albumapicsharp-2",
      "region": "eastus"
    }
  ],
  "changeType": "scale",
  "before": {
    "replicas": 2,
    "cpu": "0.5",
    "memory": "1Gi"
  },
  "after": {
    "replicas": 4,
    "cpu": "1.0",
    "memory": "2Gi"
  },
  "iac": {
    "type": "bicep",
    "repository": "https://dev.azure.com/org/project/_git/service-repo",
    "module": "modules/containerapp.bicep",
    "declaredPath": "infra/modules/containerapp.bicep"
  },
  "repositoryImpacts": [
    {
      "file": "infra/modules/containerapp.bicep",
      "selector": "resource containerapp ... .template.replicas",
      "action": "update",
      "before": 2,
      "after": 4,
      "suggestedDiff": "- replicas: 2\n+ replicas: 4",
      "rationale": "Align IaC with emergency scale adjustment",
      "risk": "Low; scaling only",
      "validation": [
        "Template validation succeeds",
        "Post-deploy replicas=4 observed"
      ],
      "backout": "Revert replicas to 2 if CPU normalizes"
    }
  ],
  "relatedWorkItems": [
    {
      "type": "AzureDevOpsWorkItem",
      "url": "https://dev.azure.com/org/project/_workitems/edit/12345"
    }
  ],
  "notes": "Runtime change applied to mitigate high CPU; reconcile IaC to prevent drift."
}
```

Minimum fields: changeId, timestamp, correlationId, summary, resources, changeType, before, after, iac, repositoryImpacts.

## IaC Identification Guidance
- Terraform
  - Look for .tf files referencing the resource provider/type.
  - Use resource addresses (module.path.resource_type.name).
  - Map parameters via variables.tf and tfvars.
- Bicep/ARM
  - Search for resource symbolic names and types in .bicep/.json.
  - Include parameter files (.parameters.json) and module references.
- Helm/Kubernetes
  - For replica counts, images, resources: values.yaml keys (replicaCount, image.tag, resources.limits/requests).
  - For ConfigMaps/Secrets: YAML manifests under k8s/ or charts/<name>/templates/.
- Mixed ownership
  - If a runtime system is managed by multiple IaC tools, split repositoryImpacts by ownership and mark conflicts clearly.

## Instructions and Best Practices
- Do not implement changes. Only document what must be updated in source control to match the known-good runtime state or intended remediation outcome.
- Be precise but cautious with line numbers; prefer selectors (YAMLPath/JSONPath/HCL address) when line numbers are unstable.
- Use minimal diffs that change only the necessary keys/arguments.
- If the declared IaC should remain the source of truth (e.g., emergency runtime change should be rolled back), record two options:
  - Option A: Update IaC to match runtime.
  - Option B: Revert runtime to match IaC.
- Always include validation steps and backout plans.
- Include compliance and approvals if required by policy (e.g., CAB ticket, change window).
- Group related changes into a single record when they share causality and deployment unit; otherwise create separate records.
- Ensure all numeric units (CPU, memory) include consistent units (m, cores, Mi, Gi). Convert where necessary.
- Redact secrets; for secret updates record the key path and rotation event, not plaintext values.

## Examples

### Kubernetes Helm Chart Update
- Scenario: Increased deployment replicas from 3 to 8 due to high CPU.
- Record:
  - before: values.yaml replicaCount=3
  - after: values.yaml replicaCount=8
  - repositoryImpacts:
    - file: helm/microservice-api/values.yaml
    - selector: "$.replicaCount"
    - action: update
    - suggestedDiff:
      - "- replicaCount: 3" / "+ replicaCount: 8"
  - Validation: kubectl get deploy shows replicas=8
  - Backout: set replicaCount back to 3

### Terraform Azure Resource Scaling
- Scenario: Scale App Service Plan from S1 to P1v2.
- repositoryImpacts:
  - file: terraform/azure/app-service.tf
  - selector: 'resource.azurerm_app_service_plan.<name>.sku_name'
  - action: update
  - before: "S1"
  - after: "P1v2"

### ARM/Bicep Resource Addition
- Scenario: Add storage account for backups.
- repositoryImpacts:
  - file: bicep/storage/backup-storage.bicep
  - action: add
  - suggestedContent: resource block with Standard_LRS replication
- Validation: deployment succeeds; account present

### ConfigMap/Secret Update
- Scenario: Update database connection string to secondary.
- repositoryImpacts:
  - file: k8s/configmaps/app-config.yaml
  - selector: "$.data.database_url"
  - action: update
  - before: prod-db-primary.example.com
  - after: prod-db-secondary.example.com
- Redaction: do not include credentials

## Output and Presentation
- Provide the structured record (JSON preferred) along with a concise human-readable summary.
- Include a short “Repository Update Plan” section listing each file path and the exact change.
- Link related work items and commits/PRs once created.

## Coordination with Issue Tracking
- When stakeholder notification or parallel tracking in GitHub is required, create or update an issue and attach the structured change record, summary, and repository update plan. Read [github_issue.md](github_issue.md) for instructions on:
  - Identifying the correct repository and labels.
  - Avoiding duplicates and linking related issues.
  - Returning issue URLs and status updates.
