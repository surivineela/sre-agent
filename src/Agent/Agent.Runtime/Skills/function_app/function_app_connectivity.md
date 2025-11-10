# Function App Connectivity

## Purpose
Focused diagnostics for connectivity and authentication issues between a Function App and its Storage Account (DNS, TCP 443, identity/connection string, RBAC). Follow a layered approach: confirm target account, test resolution/connectivity, identify auth method, remediate within that method, then validate.

## Initial Checklist

- App name, region, SKU, intended Storage Account.
- Confirm Storage Account name with user before any change.
- Retrieve AzureWebJobsStorage (string or accountName) + identity configuration.
- Plan tests: DNS → TCP 443 → auth → minimal data-plane op.
- Determine required data-plane roles (Blob/Queue/Table) using least privilege.

## Critical Rules

- Do not change auth method (keep connection string vs managed identity as-is).
- Explicitly verify Storage Account name with user before changes.
- Check existing role assignments first; avoid duplicates.
- Report permission/API errors verbatim.

## Capabilities

- DNS + TCP (443) connectivity validation.
- Auth method detection: connection string, system-assigned, user-assigned identity.
- Method-specific remediation (refresh string, assign RBAC roles, fix settings).
- Objective before/after validation.

## Workflow
1. Scope & goal (issue summary, suspected Storage Account) → confirm account with user.

2. DNS resolve blob/queue/table endpoints (privatelink patterns if applicable).

3. TCP 443 connectivity tests; note network/firewall/private endpoint constraints.

4. Detect auth method (connection string / system / user-assigned identity).

5. Remediate within method (see below sections).

6. Validate: repeat DNS + TCP + minimal data-plane access. Wait ≥5s; retry up to 3 times for RBAC propagation.

7. Summarize root cause, actions, evidence, and next steps.

## Connectivity Diagnostics

- DNS: resolve blob/queue/table endpoints; privatelink → expect private IPs.
- TCP 443: failures imply firewall/VNet/private endpoint/egress issues.
- Network rules: ensure Function App outbound path allowed; validate private endpoint routing + DNS.

## Authentication Method Actions

### Connection String

Validation: format, account name, key current OR SAS expiry & permissions (e.g., rwdlac). Network rules permit access.

Remediation: refresh/correct string; add missing AzureWebJobsStorage if absent. Re-validate via minimal data-plane op.

### System-Assigned Identity

Validation: identity enabled; AzureWebJobsStorage__accountName correct; required services documented.

Roles: check existing; assign only needed data-plane roles (Blob/Queue/Table). Avoid control-plane unless required.

Remediation: add missing roles; wait ≥5s; retry validation.

### User-Assigned Identity

Validation: select correct identity; accountName + clientId correct; required services listed.

Roles: check existing; assign minimal Blob/Queue/Table roles.

Remediation: grant missing roles; confirm clientId; wait ≥5s; retry.

## Configuration Validation

- AzureWebJobsStorage (string): present + correct account + valid key/SAS.
- AzureWebJobsStorage__accountName (identity): present + correct account.
- clientId (user-assigned): matches intended identity.
- Service-specific (e.g., ADLS Gen2): endpoints + permissions align with roles.

## Error Patterns

- DNS/TCP failure → network/firewall/VNet/private endpoint; adjust rules / routing / DNS.
- 403 → missing roles (identity) or invalid key/SAS perms; add roles / refresh string.
- 401 → invalid creds, disabled identity, wrong clientId; fix & re-validate.
- Missing settings → add required setting after user confirmation.

## Best Practices

- Preserve auth method.
- Always user-confirm Storage Account.
- Least privilege data-plane roles only.
- Avoid duplicate role assignments.
- Wait & retry for RBAC propagation.
- Preserve exact error messages.

## Examples

Managed identity missing roles → add minimal Blob role → wait → list containers.
Connection string expired SAS → refresh string → update setting → enumerate containers.
DNS resolves privatelink + TCP failure → adjust VNet/firewall/private endpoint → retest TCP.
