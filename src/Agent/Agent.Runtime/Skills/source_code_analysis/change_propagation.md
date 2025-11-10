# Change Propagation

## Overview

Open this file AFTER remediation actions changed runtime infrastructure or configuration and those changes must be reconciled back to IaC / config sources. It is a record-only workflow: produce structured change records (no execution, no direct edits). Goal: restore GitOps parity and provide audit trail.

Triggers:

- Scaling adjustments (replicas, SKU, instances)
- Config changes (timeouts, flags, connection endpoints)
- Resource adds/removals
- Runtime hotfixes not yet reflected in committed IaC

## Capabilities

- Enumerate each runtime change as a discrete record
- Map to authoritative IaC/config file(s)
- Capture before vs required committed state
- Provide minimal diff / edit guidance
- Indicate IaC mechanism(s) (Terraform | Bicep | ARM | Helm | K8s | App Config)
- Avoid exposing secrets; reference secret keys/locations

## When to Use

Immediately after confirming runtime adjustments that differ from committed code. Skip if no divergence exists.

## Inputs Needed

- Incident context (what/why/when)
- Observed runtime before/after values
- IaC mechanism(s) present
- Read access to relevant files (if missing, provide best‑guess + verification note)

## Workflow (Condensed)

1. Enumerate runtime changes → list discrete items.
2. Identify authoritative file(s) (prefer override layer: values.yaml, tfvars, param files).
3. Capture before (committed) vs after (required committed) state + resource identifiers.
4. Draft minimal edit guidance (file path, YAML/JSON path or near line, tiny diff snippet).
5. Check environment scoping, secrets handling, potential drift/conflicts.
6. Compile structured change records → link issue ID for audit.

## Structured Change Record Template

One record per change:

- Title: concise summary ("Scale Orders API replicas 3→8 prod")
- Context: reason, environment(s), timestamp(s), incident/issue IDs
- IaC Mechanism(s): Terraform | Bicep | ARM | Helm | K8s | App Config
- Target Resource(s): identifiers + type
- Source File(s): repo‑relative path(s) + precedence notes
- Before: committed value(s) or "not present"
- After: required committed value(s)
- Guidance: edit location (line/YAML path), minimal diff
- Verification: plan/apply, helm template, kubectl get, test run
- Risk/Impact: expected effect, rollout caveats
- Ownership: reviewers
- References: incident, dashboards, runbooks

## Detect IaC Mechanism(s)

Scan for: `*.tf`, `*.bicep`, ARM template `*.json`, `charts/`, `values.yaml`, Kubernetes manifests (apiVersion/kind), `appsettings.*.json`, configmaps, `.env`. Map each change to its authoritative source; avoid duplicating same setting across layers (flag conflicts).

## Implementation Notes (Record-Only)

Terraform: edit module/root source (not generated), update variable or tfvars, note drift.
Bicep/ARM: prefer Bicep; adjust resource/module params; maintain symbolic names; param file for env overrides.
Helm: prefer values.yaml or env override; template edits only if unavoidable (validate with helm template/lint).
Kubernetes: modify Deployment/StatefulSet spec (replicas/resources) or kustomize overlay.
App Config: change environment-specific file or ConfigMap; never inline secrets (reference existing keys).

## Examples (Condensed)

Helm Replica Increase:

- File: `helm/microservice-api/values.yaml` 3→8
- Diff: `- replicaCount: 3` → `+ replicaCount: 8`
- Verify: helm template → Deployment replicas=8

Terraform SKU Change:

- File: `terraform/azure/app-service.tf` `sku_name` S1→P1v2
- Verify: terraform plan shows SKU change

ConfigMap Failover:

- File: `k8s/configmaps/app-config.yaml` database_url primary→secondary
- Verify: rollout status + application connectivity

## Quality & Safety

No secrets; environment scope explicit; minimal diffs only; call out drift; if line uncertain provide YAML/JSON path + search hint.

## Outputs

- Structured change records (mechanism, file path, resource IDs, before/after, diff, verification, reviewers, references)

## Related Files

- Open `github_issue.md` to integrate records into the incident issue.
