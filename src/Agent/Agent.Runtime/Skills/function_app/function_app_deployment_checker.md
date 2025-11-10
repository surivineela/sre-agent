# Function App Deployment Checker

## Overview
Diagnose and resolve deployment issues in Azure Function Apps by analyzing deployment history, logs, configurations, and artifacts. Provide clear remediation steps tailored to hosting plans and deployment methods, with explicit validation and post-change verification.

## Planning Checklist
- Identify the Function App resource, region, and hosting plan (Consumption, Premium, Flex Consumption, Dedicated/Isolated).
- Determine deployment method (Run From Package, Zip Deploy, Local Git, GitHub Actions/Azure DevOps, Container, Deployment Slots).
- Gather last 30 minutes of host availability and error metrics.
- Collect recent deployment history, status, and artifacts.
- Correlate availability and error patterns with deployment events.

## Confidence Assessment
- High (>80%):
  - Clear deployment failure messages in logs.
  - Missing/corrupted zip or invalid WEBSITE_RUN_FROM_PACKAGE target.
  - Strong correlation between deployment time and availability drop.
  - Known plan limitation or misconfiguration aligned with symptoms.
- Medium (50–80%):
  - Multiple functions failing with a common pattern.
  - Cross-source configuration anomalies.
  - Resource constraints temporally aligned with deployment.
  - Partial successes with identifiable failure boundaries.
- Low (<50%):
  - Intermittent failures without consistent patterns.
  - Multi-component issues with unclear locus.
  - Insufficient or conflicting diagnostics.
  - Edge cases outside standard scenarios.

## Solution Categories
- Immediate fixes: Correct WEBSITE_RUN_FROM_PACKAGE settings, replace invalid/missing zip, update misconfigurations.
- Specialized analysis: Deep investigation of Run From Package behavior and package accessibility; consult [run_from_package.md](run_from_package.md).
- Investigation required: Complex multi-component or pipeline-coupled deployment failures requiring iterative validation.
- Process improvements: Pre-deployment validation, CI/CD enhancements, deployment slot strategies, artifact integrity checks.

## Diagnostic Workflow
1. Initial Resource Validation
   - Confirm the Function App identity and retrieve basic configuration.
   - Determine hosting plan SKU and note plan-specific deployment behavior (e.g., cold starts on Consumption; slot support on Standard/Premium/Isolated).
   - Identify the active deployment method and relevant configuration keys.

2. Host Availability Assessment (Last 30 Minutes)
   - Retrieve availability and error metrics and render a time series titled "Function App Host Availability".
   - Present bolded average availability (e.g., **Average Availability: 84%**) with a one-line interpretation.
   - Clarify that 100% may indicate zero traffic; validate traffic volume if needed.

3. Focused Deployment Failure Analysis
   - Analyze deployment logs and platform diagnostics for deployment-specific failures (exclude runtime execution errors at this step).
   - Extract specific error messages and patterns (e.g., zip download failures, checksum errors, authentication/authorization issues, storage timeouts).

4. WEBSITE_RUN_FROM_PACKAGE Assessment (If Applicable)
   - Check for configuration issues and package accessibility.
   - If indicators include “Zip file download failed” or similar:
     - Retrieve the current WEBSITE_RUN_FROM_PACKAGE value and explain its role.
     - Verify blob/container path and existence of the referenced zip (exists/missing/invalid).
     - If missing or invalid:
       - Present remediation options:
         1) Update WEBSITE_RUN_FROM_PACKAGE to a valid zip path.
         2) Track resolution with an issue in the delivery backlog.
       - Verify the new zip exists before applying changes.
       - Apply the update only after explicit confirmation, then validate deployment success and host availability.
   - For advanced analysis of Run From Package behavior, SKU compatibility, package integrity, or access controls, consult [run_from_package.md](run_from_package.md).

5. General Deployment Configuration Analysis (Non-Run From Package)
   - Validate deployment method configuration (Zip Deploy, Oryx build, Kudu/SCM, GitHub Actions, Azure DevOps, Container).
   - Identify misconfigurations: incorrect branch/slot targets, missing startup file, incompatible runtime versions, build failures, insufficient file permissions, stale cached artifacts.
   - Provide method-specific recommendations and command/script-level validation steps.

6. Deployment History Pattern Analysis
   - Retrieve deployment history and construct a timeline of events versus availability/error metrics.
   - Display a table of recent deployments with timestamp, initiator/commit, artifact, slot, status, and error summary.
   - Identify recurring failures, rollbacks, or partial deployments; correlate with health degradation or recovery.

7. Remediation Plan and Validation
   - Present root cause findings with a stated confidence level.
   - Provide step-by-step remediation tailored to the hosting plan and method.
   - Include pre-change checks, change steps, and post-change validation:
     - Re-deploy or update configuration.
     - Restart/apply changes if required by the plan/method.
     - Re-check availability, error rates, and function readiness.
   - If supported by SKU, recommend safe rollout via deployment slots with swap validation.

8. Preventative Measures
   - Add pre-deployment validation gates (zip integrity, storage access check, runtime version alignment).
   - Pin and validate WEBSITE_RUN_FROM_PACKAGE URIs with versioned artifact paths.
   - Implement CI/CD checks for function.json and extensions metadata.
   - Use canary slots and automated health checks before swaps.
   - Store deployment evidence (artifacts, logs, manifests) for traceability.

## Plan- and Method-Specific Guidance
- Consumption/Flex Consumption:
  - Expect cold starts post-deployment; validate health after warm-up.
  - Ensure package access is performant and reliable.
- Premium/Dedicated/Isolated:
  - Prefer deployment slots for zero-downtime swaps.
  - Validate slot-specific settings and sticky configurations.
- Zip Deploy/Kudu:
  - Confirm deployment completeness and no locked files.
  - Check Kudu logs for build/extract errors and disk space constraints.
- GitHub Actions/Azure DevOps:
  - Verify workflow variables/secrets (storage SAS, runtime version, slot name).
  - Ensure artifact publish step aligns with Function runtime requirements.
- Containers:
  - Verify image build success, ENTRYPOINT/CMD, and configuration compatibility with Functions runtime.

## Output Requirements
- Use time series for availability and deployment event alignment.
- Bold critical metrics and statuses (e.g., **Average Availability**, **Failure Count**, **File Exists: No**).
- Provide a deployment history table with timestamp, commit/branch, artifact, slot, status, and error summary.
- Present complete findings; do not truncate critical observations.
- Maintain progress markers (e.g., Step 3/8: Deployment Failure Analysis).

## Error Handling
- Report only permission errors that block deployment analysis, specifying the impacted scope.
- Adapt quietly to tooling limitations; prioritize observable data and actionable guidance.
- Do not expose SKU or tool support limitations; focus on what can be validated.

## Operational Guidelines
- State the purpose of each diagnostic step in one line before executing it.
- After each step, validate findings in 1–2 lines and decide the next action or correction.
- Continue systematically until deployment analysis is complete; avoid stopping at uncertainty.

## When to Consult Additional Files
- WEBSITE_RUN_FROM_PACKAGE and Package Accessibility
  - Use [run_from_package.md](run_from_package.md) for specialized analysis of package configuration, access control, integrity, SKU nuances, and repair procedures.
- Post-Deployment Configuration Issues
  - Use [function_app_configuration_checker.md](function_app_configuration_checker.md) when deployment succeeds but functions misbehave due to settings, bindings, or trigger configurations (including Blob triggers via Event Grid).
- Runtime Execution Failures
  - Use [function_app_execution_failures.md](function_app_execution_failures.md) when errors are predominantly execution-time (exceptions, timeouts, retries) with clean deployment results.

## Examples
- Missing ZIP in Run From Package:
  - Symptoms: Host availability drops after deployment; logs show “Zip file download failed.”
  - Steps: Verify WEBSITE_RUN_FROM_PACKAGE; check blob path; file missing.
  - Fix: Upload valid zip; update WEBSITE_RUN_FROM_PACKAGE; validate host availability improves and errors drop.
- Pipeline Misconfiguration to Wrong Slot:
  - Symptoms: “Successful” deployment but no changes in production; history shows deploy to staging.
  - Steps: Inspect workflow variables; confirm target slot mismatch.
  - Fix: Correct slot target; redeploy; perform slot swap with health checks; verify production traffic.
- Partial Zip Deploy with Locked Files:
  - Symptoms: Mixed old/new code; sporadic 5xx.
  - Steps: Review Kudu logs; detect file lock errors.
  - Fix: Stop site during deploy or use run-from-package; redeploy; confirm consistent function versions and stable availability.
