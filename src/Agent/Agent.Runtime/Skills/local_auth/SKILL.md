# Local Authentication Remediation Skill

## 1. Purpose & Scope

Identify and eliminate insecure local / key-based authentication across Azure resources by transitioning to identity-based access (Managed Identity, Azure AD / Entra ID). Applies to:
• Storage Accounts • Cosmos DB • Azure SQL Servers • Event Hub Namespaces • Service Bus Namespaces • App Services (FTP & SCM basic auth) • AKS clusters (local accounts)

## 2. When to Use This Skill

Load this skill when the user asks to assess, improve, disable, or remediate key-based / local authentication for any supported resource type, or requests guidance on migrating to identity-based auth.

## 3. Initial Resource Discovery

Search available conversation/history/context for Azure resource IDs or names. Patterns to look for internally (record concisely in ReasoningScratchPad):
• Full IDs: /subscriptions/.../resourceGroups/.../providers/...
• Lines starting with RESOURCE| or between RESOURCE_DATA_START / RESOURCE_DATA_END blocks
• Explicit resource names + resource group / subscription hints

If no resource IDs found: ask the user for the exact Azure resource IDs using the canonical format:
`/subscriptions/<subId>/resourceGroups/<rg>/providers/<provider>/<type>/<name>`

## 4. First Response Logic

If resources with suspected local auth are identified: present a concise list (name, location, full ID) and ask user to choose one option:

1. Ignore / Defer (temporary)
2. Track via GitHub Issue
3. Immediate Remediation (recommended)

If none identified: request IDs (do not fabricate).

Internal-only: Keep search notes minimal; do not expose internal enumeration to user. Follow system prompt conciseness rules.

## 5. Decision Options

### Option 1: Ignore / Defer

• Requires user-provided duration; default 30 days if omitted (state explicitly).
• Convert natural language duration to TimeSpan.
• Tag resource via AddIgnoreTagToResource.
• Warn: delaying remediation maintains exposure (key leakage, lateral movement).

### Option 2: Track via GitHub Issue

• Use CreateGithubIssue per resource or grouped logically.
• Include: resource ID, current auth mechanism, recommended identity-based alternative, security impact, suggested priority & timeline.

### Option 3: Immediate Remediation (Recommended)

Prerequisites (must confirm with user): scope, impact on application code, availability of Managed Identity / Azure AD setup, rollback feasibility.
Outcome: disable local / key-based auth, enable identity-based auth, communicate required downstream code changes.

## 6. Pre-Remediation Warnings (Option 3 Only)

Inform user BEFORE executing any write operation:
• Only authentication settings change; no data plane mutation.
• Application updates needed: replace connection strings / keys with token or Managed Identity flows.
• Test flows post-change (CI/CD, background jobs, scripts).
• Ensure rollback plan (e.g., re-enable key-based access) if failure impacts production.

## 7. Resource-Type Actions (Option 3)

Perform actions only after explicit user confirmation. Use listed tools (do not mention tool names to user):
• Storage: StorageAccountSetSharedKeySupport (disable shared keys); StorageAccountSetContainerPublicAccess (disable public container access).
• Azure SQL: AzureSqlServerSetLocalAuthSupport (enforce Azure AD only); verify Azure AD admin set; ensure connection strings updated.
• Cosmos DB: CosmosDbSetKeyBasedAuthSupport (disable key-based auth); configure RBAC; instruct SDK token usage.
• Event Hub: EventHubSetLocalAuthSupport; shift publishers/consumers to Managed Identity / Azure AD.
• Service Bus: ServiceBusSetLocalAuthSupport; update processors to identity-based authorization.
• App Service: AzureAppServiceSetFtpAuthenticationSupport (disable FTP basic); AzureAppServiceSetScmAuthenticationSupport (disable SCM basic); ensure deployment pipeline uses Managed Identity.
• AKS: (Only if user chose Option 3) Disable local accounts; integrate Azure AD; enable workload identity / pod-managed identity.

## 8. CLI Usage & AKS Specifics

### General Azure CLI Usage

Use azure_cli_command_executor for Azure CLI commands when:
• A required read/write operation isn't covered by the listed specialized tools.
• Verifying post-remediation state (e.g., confirming shared key disablement flags, auth settings).
• Retrieving configuration not exposed via existing tools (diagnostic settings, identity assignments, feature flags).
• Fallback for unsupported resource types requiring auth posture verification.

Requirements:
• Subscription ID must be known (do not assume or guess).
• Never fabricate parameters; request missing ones explicitly.
• Present a plan and get confirmation before any destructive/auth-changing CLI command.
• Parse raw CLI output; summarize only relevant auth/security status (avoid dumping unless user asks).
• If CLI output is ambiguous, request clarification rather than proceeding.

Preferred Flow:

1. Attempt specialized tool first (if available for the resource type/action).
2. If gap remains, use azure_cli_command_executor with a precise command.
3. Summarize outcome (changed setting, verification status, next required step).

### AKS Specifics

Use azure_cli_command_executor for cluster-level changes or verification not covered by other tools. Sample internal commands (only expose on user request):
`az aks update --resource-group <rg> --name <cluster> --disable-local-accounts`
`az aks get-credentials --resource-group <rg> --name <cluster> --admin --overwrite-existing`
Ensure subscription ID is known before invoking CLI. After disabling local accounts, advise using Azure AD tokens / workload identity for pods.

## 9. Post-Remediation Summary

Provide (concise):
• List of resources changed (name + ID)
• Previous vs new auth mode
• Required application updates still pending
• Follow-up recommendation: secret scanning, rotation of any previously exposed keys, enabling monitoring for unauthorized access attempts.

## 10. Edge Cases & Clarifications

• Partial names only: request full resource IDs before remediation.
• Conflicting instructions (e.g., ignore & remediate same resource): ask user to clarify priority.
• Missing Managed Identity: recommend enabling identity first; pause remediation.
• Multi-resource batch: confirm scope; apply changes sequentially per type to simplify rollback.
• Any destructive ambiguity: halt and request explicit confirmation.

## 11. Safety Alignment

All write actions require explicit user confirmation (scope + impact). Present a concise plan before executing; never guess parameters. Respect system prompt conciseness and do not mention tool names in user-facing messages.

## 12. Internal Use Notes

• Prefer identity-based remediation; only accelerate if user urgency stated.
• Do not reload this skill redundantly once active.

---
This file is the entry point for the local_auth skill. No additional markdown files exist; all logic contained here.
