---
name: function_app
description: Load this skill when investigating Azure Function App reliability or availability issues (host runtime errors, availability degradation, deployment/package problems, execution failures, configuration or connectivity anomalies). It provides structured, progressive diagnostics across connectivity, configuration, deployment, execution, and run-from-package scenarios. Prefer this skill for end-to-end Function App incident analysis
tools:
  - ListFunctionApps
  - GetFunctionAppInfo
  - GetFunctionAppDeploymentSlots
  - GetArmResourceAsJson
  - HasHostRuntimeErrors
  - GetFunctionAppRequestAvailability
  - GetFunctionAppDeploymentFailureAnalysis
  - HasRunFromPackageIssues
  - DiagnoseRunFromPackageIssues
  - RepairRunFromPackageConfiguration
  - PlotTimeSeriesData
  - PlotBarChart
  - PlotScatter
  - WaitInMilliSeconds
  - CloseAzureMonitorAlert
  - CheckConnectivityToAzureWebJobsStorage
  - CheckTcpConnectivity
  - CheckDnsResolution
  - GetAppSetting
  - ConfigureAppSettingsForManagedIdentityStorage
  - ListKeysAndUpdateAppSettings
  - AddRoleAssignment
  - CheckRoleAssignment
  - GetRoleDetailsFromName
  - SearchResource
  - GetFailedFunctionInvocations
  - GetTop3ExceptionsPerFunction
  - GetTop3ExceptionsWithStackTraces
  - GetFunctionAppExecutionFailures
  - GetFunctionAppCallStacks
  - GetFunctionAppDeploymentHistory
  - GetFunctionAppSlotSwapHistory
  - PerformDeploymentSwapForApp
  - CreateGithubIssue
  - UpdateAppSettings
  - GetFunctionAppConfigurationChecks
  - GetEventGridSubscriptions
  - GetResourceIdFromStorageServiceUri
  - VerifyFilesInBlobContainer
  - UpdateWebsiteRunFromPackage
  - GetFunctionAppDeploymentChecks
  - GetRunFromPackageConfiguration
  - GetSkuCapabilities
  - VerifyRunFromPackageConfiguration
  - ValidatePackageAccessibility
  - InspectPackageStructure
  - GetPackageMetadata
  - GeneratePackageSasUrl
  - GetRunFromPackageRecommendations
---

# Azure Function App SRE Skill

## Purpose

Structured end-to-end diagnosis of Azure Function App availability and reliability issues: host runtime errors, availability drops, deployment/package problems, execution failures, configuration or connectivity anomalies. Progressively consult referenced files only when the corresponding condition is detected.

## Quick Classification (start here)

1. Host Runtime errors present? → Check deployments first; if zip/package messages appear, see [run_from_package.md](run_from_package.md); else see [function_app_connectivity.md](function_app_connectivity.md).
2. No host errors but availability degraded? → Inspect deployments; if recent failure/swap issues, see [function_app_deployment_checker.md](function_app_deployment_checker.md).
3. Deployment/path clean but functions failing (exceptions/timeouts/retries)? → See [function_app_execution_failures.md](function_app_execution_failures.md).
4. Function execution healthy but triggers/bindings misbehave (Event Grid Blob, settings, identity) → See [function_app_configuration_checker.md](function_app_configuration_checker.md).
5. WEBSITE_RUN_FROM_PACKAGE suspected (download failures, missing functions after deploy) → See [run_from_package.md](run_from_package.md) at the moment of suspicion.

## Minimal Workflow (sequential)

1. Scope: app name, region, SKU, timeframe, affected functions.
2. Host health (past 30 min): availability + host runtime error presence; bold average (e.g., **Average Availability: 83%**). If 100%, confirm traffic volume.
3. Branch: select one path only:
   - Host Runtime error path → correlate with recent deployment/swap; route to connectivity or package as above.
   - No Host errors → evaluate deployment history; if clear move to execution.
4. Execution analysis (only if deployment clean): exception & failed invocation patterns (top types, stack traces). If configuration anomalies implied, branch to configuration.
5. Configuration validation: app settings, triggers/bindings (Blob + Event Grid), identity, WEBSITE_RUN_FROM_PACKAGE presence (without deep package checks unless indicated).
6. Final deployment validation (if skipped earlier) and slot considerations.
7. Summarize root cause(s), confidence, actions taken, and post-action availability delta.

## Confidence Heuristics

High: Clear host/runtime error or strong deployment-time correlation; dominant (>50%) repeatable failure pattern; explicit misconfiguration.
Medium: Multi-source corroboration (errors + metrics) with partial pattern (20–50%).
Low: Intermittent, conflicting, or insufficient data.

## When to Consult Files (conditions must be met)

- Connectivity issues after ruling out deployment/package: [function_app_connectivity.md](function_app_connectivity.md)
- Repeated execution failures (exceptions/timeouts/retries): [function_app_execution_failures.md](function_app_execution_failures.md)
- Misconfigurations (settings, triggers, Event Grid Blob subscriptions, identity): [function_app_configuration_checker.md](function_app_configuration_checker.md)
- Deployment anomalies (failed swaps, artifact issues): [function_app_deployment_checker.md](function_app_deployment_checker.md)
- Package download/mount issues, missing functions post-deploy: [run_from_package.md](run_from_package.md)

## Output Essentials

- Time series for availability / failure metrics; bold key stats (e.g., **Error Rate: 42%**).
- Tables for unsuccessful deployments/swaps (timestamp, commit/branch, artifact, slot, status, error summary).
- List concrete next actions with confidence level.

## Operational Guidelines

- Before each step: one-line purpose. After each step: 1–2 line validation + next decision.
- Do not speculate; rely on observable data. Report only permission errors that block progress (include scope).
- All referenced markdown files are part of this single skill; they are consulted progressively—do not label this as a handoff.

## Example Mappings

- Host runtime “Zip file download failed” → Deployment correlation → [run_from_package.md](run_from_package.md) → Fix → Re-check availability improvement.
- Availability drop after slot swap; no host errors → [function_app_deployment_checker.md](function_app_deployment_checker.md) → Identify failed artifact → Redeploy/rollback → Confirm improved **Average Availability**.
- Elevated 5xx without deployment issues → [function_app_execution_failures.md](function_app_execution_failures.md) → Dominant exception → Propose targeted code/config fix → Re-evaluate error rate.
- Blob trigger not firing (Event Grid path) → [function_app_configuration_checker.md](function_app_configuration_checker.md) → Repair subscription → Validate trigger invocations.

## Related Files

Connectivity: [function_app_connectivity.md](function_app_connectivity.md) | Execution: [function_app_execution_failures.md](function_app_execution_failures.md) | Configuration: [function_app_configuration_checker.md](function_app_configuration_checker.md) | Deployment: [function_app_deployment_checker.md](function_app_deployment_checker.md) | Run From Package: [run_from_package.md](run_from_package.md)
