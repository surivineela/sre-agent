# Change Propagation

## Overview
Record and format all write-only infrastructure and application modifications into structured change records for repository synchronization. Identify the Infrastructure-as-Code (IaC) sources impacted (Terraform, Bicep/ARM, Helm, Kubernetes manifests) and produce clear, actionable update guidance. Maintain GitOps consistency and audit trails by presenting evidence, before/after states, and recommended repository updates. Do not implement or apply changes—only record and format them.

## Capabilities
- Capture remediation actions and operational changes (scaling, configuration updates, resource creations/updates; if a deletion occurred elsewhere, record it as well).
- Identify the authoritative source (Terraform, Bicep/ARM, Helm charts, Kubernetes manifests) for each changed resource.
- Map runtime changes to repository file paths, keys/attributes, and lines/sections to update.
- Produce structured change records with before/after states and rationale for synchronization.
- Provide implementation guidance for maintainers (update instructions, diffs, suggested PR descriptions).
- Maintain evidence for auditability (command outputs, timestamps, resource identifiers).

## Core Workflow
1. Collect Change Inputs
   - Parse the operational log or chat history for each change event.
   - For every change, capture:
     - What changed (resource type, name, namespace/region/scope).
     - Where it changed (cluster/subscription, environment).
     - When it changed (timestamp).
     - Why it changed (reason, incident context).

2. Determine Authoritative Source
   - Identify IaC/control-plane ownership using available context:
     - Terraform: .tf files, modules, variables, state references.
     - Bicep/ARM: .bicep/.json templates and parameter files.
     - Helm: Chart files (Chart.yaml), values.yaml, templates/.
     - Kubernetes manifests: YAML files in k8s/ or env overlays (e.g., kustomize).
   - If multiple systems could own the resource, note ambiguity and recommend owner confirmation.

3. Locate Repository Targets
   - Map each change to probable repository paths (e.g., helm/<service>/values.yaml, terraform/<module>/*.tf, bicep/<area>/*.bicep, k8s/<env>/*.yaml).
   - Include branch/environment conventions if known (e.g., main, develop, env/prod).
   - If exact lines/keys are unknown, provide targeted search hints (file globs, keys to grep).

4. Build Structured Change Records
   - For each change, produce:
     - Resource metadata: kind, name, identifiers (namespace, subscription, resource group, region).
     - IaC system: Terraform | Bicep/ARM | Helm | Kubernetes.
     - Repository mapping: repo, branch, path(s), key/attribute.
     - Before/after state: explicit values or embedded diff.
     - Evidence: command outputs, excerpts from current files if available.
     - Implementation guidance: exact edit instructions, suggested commit message/PR title.
     - Status: pending/applied (always “pending” in this context).

5. Validate Consistency
   - Check that after-state aligns with the executed change.
   - Confirm that recommended updates correspond to the correct environment/overlay.
   - Flag any drift risk (e.g., runtime hotfix not reflected in IaC).

6. Present Results
   - Return a consolidated list of change records.
   - Include summaries per system (Terraform, Helm, etc.) and a final checklist for maintainers.

## Structured Change Record Template
Use this template for each change item. Prefer JSON for machine consumption or YAML if requested.

```json
{
  "id": "chg-2025-10-24-001",
  "timestamp": "2025-10-24T12:34:56Z",
  "context": {
    "environment": "prod",
    "cluster": "aks-prod-westus2",
    "subscriptionId": "<GUID>",
    "resourceGroup": "rg-prod-aks"
  },
  "resource": {
    "system": "Helm",
    "kind": "Deployment",
    "name": "microservice-api",
    "namespace": "payments"
  },
  "change": {
    "attribute": "replicaCount",
    "before": 3,
    "after": 8,
    "reason": "High CPU utilization; increase replicas to stabilize latency"
  },
  "repository": {
    "repo": "org/infrastructure",
    "branch": "main",
    "paths": [
      "helm/microservice-api/values.yaml"
    ],
    "key": "replicaCount",
    "lineHint": "approx line 12"
  },
  "evidence": {
    "commands": [
      "kubectl get deploy microservice-api -n payments -o jsonpath='{.status.readyReplicas}'",
      "kubectl get hpa -n payments"
    ],
    "outputs": [
      "readyReplicas: 8",
      "HPA not configured"
    ]
  },
  "implementationGuidance": {
    "instructions": [
      "Update helm/microservice-api/values.yaml: change replicaCount from 3 to 8."
    ],
    "diff": "--- a/helm/microservice-api/values.yaml\n+++ b/helm/microservice-api/values.yaml\n- replicaCount: 3\n+ replicaCount: 8\n",
    "commitMessage": "Increase microservice-api replicas from 3 to 8 (prod) due to high CPU",
    "prTitle": "Scale microservice-api replicas to 8 in prod"
  },
  "status": "pending"
}
```

## IaC Identification Heuristics
- Terraform
  - Look for .tf files, modules named for the resource domain, variables.tf mapping, or references to azurerm_* resources.
  - State or backend hints may indicate environment-specific directories (e.g., terraform/env/prod).
- Bicep/ARM
  - .bicep files per domain (networking, storage, compute) and parameter files (*.parameters.json).
  - Resource symbolic names and module composition indicate ownership.
- Helm
  - Helm chart directories with Chart.yaml; values.yaml holds tunables (replicas, resources, env).
  - templates/ directory contains templated Kubernetes manifests.
- Kubernetes Manifests
  - k8s/ or env overlays (e.g., kustomize with kustomization.yaml).
  - Separate per-env directories (e.g., k8s/prod/).

If ownership is unclear, propose the most likely source and mark the record with "ownershipUncertain": true alongside next steps to verify.

## Instructions and Best Practices
- Record-only: do not apply changes, run terraform plan/apply, or helm upgrade.
- One record per discrete change; group related changes under a parent incident id.
- Be explicit about environment scoping to avoid cross-env contamination.
- Capture full before/after values; if “before” is unknown, state “unknown” and add a file lookup task.
- Provide precise file paths and keys; if exact lines are unknown, include lineHint and search guidance.
- Preserve evidence verbatim (command outputs, error messages) for audit trails.
- Align updates with the authoritative source. If a runtime kubectl patch modified a field managed by Terraform, direct the update to Terraform, not raw YAML.
- Flag drift scenarios and recommend a reconciliation step (e.g., run terraform plan or helm template diff) for maintainers.
- Use clear, consistent commit and PR conventions including environment, service, and rationale.
- Do not redact secrets in the repository files beyond organizational policy; when updating Secrets via sealed/encrypted methods, point to the correct generator process rather than embedding plaintext.

## Examples

### Kubernetes Helm Chart Update (replica scale)
- Action: Increase deployment replicas from 3 to 8.
- Mapping:
  - System: Helm
  - File: helm/microservice-api/values.yaml
  - Key: replicaCount
- Record:
  - Before: 3
  - After: 8
  - Guidance: Update values.yaml; include diff and commit message.

### Terraform Azure Resource Scaling (App Service Plan SKU)
- Action: Scale App Service Plan S1 → P1v2.
- Mapping:
  - System: Terraform
  - File: terraform/azure/app-service.tf
  - Key: sku_name
  - Line hint: ~25
- Record:
  - Before: "S1"
  - After: "P1v2"
  - Guidance: Update sku_name, run plan/apply (by maintainers), include rationale.

### ARM/Bicep Template Resource Addition (Storage Account)
- Action: Add storage account for backups.
- Mapping:
  - System: Bicep
  - File: bicep/storage/backup-storage.bicep
- Record:
  - Before: Absent
  - After: New resource with Standard_LRS
  - Guidance: Add resource block; include example snippet and parameters if applicable.

### ConfigMap/Secret Update (Failover connection string)
- Action: Update database_url to secondary.
- Mapping:
  - System: Kubernetes manifests (or Helm values if templated)
  - File: k8s/configmaps/app-config.yaml
  - Key: data.database_url
  - Line hint: ~8
- Record:
  - Before: prod-db-primary.example.com
  - After: prod-db-secondary.example.com
  - Guidance: Update value; if secrets are managed via sealed-secrets or external secret manager, direct maintainers to the correct workflow.

## Output and Presentation
- Provide a consolidated list of change records in JSON or YAML.
- Include a human-readable summary at the top:
  - Total changes by system
  - Affected services/resources
  - Any ownership uncertainties or drift risks
- End with a maintainer checklist:
  - Review mappings and ownership
  - Apply updates in the correct repos/branches
  - Open PRs with provided titles/messages
  - Perform validation (terraform plan, helm diff, kubectl diff)
  - Close the loop by linking PRs/issues to the incident

## Related Skill Files and When to Use Them

### GitHub Issue Management
- Use [github_issue.md](github_issue.md) to formally track the recorded changes, create or update issues for review/approval, link related incidents, and notify stakeholders.
- Recommended when:
  - Changes require cross-team review or CAB approval.
  - Multiple repositories or systems are impacted and need coordination.
  - Tracking status, ownership, and PR links is necessary for auditability.

## Maintainer Checklist Template
- Confirm IaC ownership for each resource.
- Validate file paths and keys; adjust line numbers as needed.
- Apply updates on a feature branch; open PR with provided title and context.
- Run appropriate validation (terraform plan, helm template/diff, kubectl kustomize + diff).
- Ensure environment overlays reflect the intended scope (dev/staging/prod).
- Link PRs to the tracking issue and incident ID.
- After merge, verify runtime state matches desired state via the control plane.
