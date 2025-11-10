# Function App Configuration Checker

## Overview
Diagnose and resolve configuration issues in Azure Function Apps by systematically analyzing hosting plan characteristics, application settings, triggers, bindings, identities, and dependent resources. Pay special attention to Blob-triggered functions configured to use Event Grid, ensuring end-to-end subscription and endpoint correctness. Provide clear, prioritized recommendations and validate outcomes.

## Capabilities
- Identify configuration mismatches across Function App settings, triggers, and bindings.
- Validate Blob-triggered functions using Event Grid, including subscription existence and webhook endpoint correctness.
- Assess environment- and SKU-specific settings that impact behavior (e.g., TLS minimum version).
- Compare production and deployment slot configurations for issues.
- Produce structured findings with actionable remediation steps.

## Planning Checklist
- Confirm Function App scope: app name, region, and hosting plan SKU (Consumption, Premium, Dedicated, Isolated, Flex Consumption).
- Enumerate triggers and bindings; note any Blob triggers using Event Grid.
- Collect critical app settings and connection strings.
- Determine if deployment slots are in use and whether slot-specific settings may differ.
- Define success criteria (e.g., Event Grid delivery working, trigger firing, configuration parity across slots).

## Confidence Assessment
- High Confidence (>80%)
  - Clear setting errors (missing/incorrect values) with known fixes.
  - Absent or malformed Event Grid subscriptions for Blob triggers using Event Grid.
  - SKU-specific misconfigurations with documented resolutions.
- Medium Confidence (50–80%)
  - Partial or inconsistent Event Grid setup.
  - Settings present but likely misconfigured; slot config differences that plausibly affect behavior.
- Low Confidence (<50%)
  - Intermittent issues without a consistent pattern.
  - Multiple interacting components with ambiguous signals.
  - Edge cases not covered by standard documentation.

## Solution Categorization
- Immediate Fixes: Correct application settings and connection strings; create or fix Event Grid subscriptions; adjust minimum TLS version (prefer 1.2+).
- Configuration Adjustments: Trigger/binding updates, identity and access corrections, SKU-aligned configuration tuning.
- Investigation Required: Complex multi-component scenarios needing deeper validation with logs/owners.
- Process Improvements: Pre-deployment configuration validation, slot parity checks, and configuration-as-code guardrails.

## Diagnostic Workflow
1. Initial Configuration Assessment
   - Retrieve Function App metadata and hosting plan details.
   - Enumerate triggers (HTTP, Timer, Queue, Service Bus, Blob, etc.) and flag Blob triggers using Event Grid.
   - Determine the hosting plan SKU to tailor depth (e.g., cold start implications, slot availability).

2. Blob Trigger with Event Grid Validation (if applicable)
   - Confirm the function uses BlobTriggerSource.EventGrid.
   - Retrieve the storage service URI setting: AzureWebJobsStorage__blobServiceUri.
   - Resolve the storage account resource ID from the blob service URI.
   - Enumerate Event Grid subscriptions on the storage account and locate the relevant subscription for the container/prefix.
   - Validate the Event Grid webhook endpoint format:
     - https://<FUNCTION_APP_NAME>.azurewebsites.net/runtime/webhooks/blobs?functionName=Host.Functions.<FUNCTION_TO_BE_TRIGGERED>&code=<BLOB_EXTENSION_KEY>
   - Verify required subscription parameters:
     - Event Type: BlobCreated
     - Endpoint Type: Webhook
     - Endpoint URL: as above
     - Filter: Path filter for the container/prefix (e.g., /blobServices/default/containers/<CONTAINER_NAME>/blobs/<BLOB_PREFIX>)
   - Check minimum TLS version (recommend 1.2 or greater) on the Function App.
   - If missing or incorrect:
     - Specify exact steps to create or correct the Event Grid subscription.
     - Include endpoint URL construction details and the required filter path.
     - Provide guidance for securely retrieving the Blob extension key and managing it as a secret.
     - Offer a concise checklist for post-creation validation (Event Grid delivery metrics, function invocation logs).

3. General Configuration Analysis (all scenarios)
   - Application Settings and Connection Strings
     - Confirm presence and correctness of critical settings (e.g., AzureWebJobsStorage, connection strings for triggers/bindings).
     - Validate casing, value formats, and environment-variable substitutions.
   - Trigger- and Binding-Specific Checks
     - HTTP: Auth level, allowed methods, function route conflicts.
     - Timer: CRON expression validity and time zone expectations.
     - Queue/Service Bus: Queue/topic names, connection string permissions, dead-letter handling.
     - Blob (non-Event Grid): Polling model settings, path patterns, and storage permissions.
   - Identity and Access
     - If using managed identities, verify role assignments on dependent resources (Storage Account, Service Bus, etc.).
     - Ensure permissions match trigger/binding operations (read/list vs. write).
   - Network/Platform Controls
     - Confirm inbound access to the webhook endpoint is not blocked by IP restrictions or Private Endpoints (if applicable).
     - Validate WEBSITE_RUN_FROM_PACKAGE coherence with app settings that influence runtime behavior.
   - Platform Settings
     - Enforce minimum TLS version to 1.2 or higher.
     - Confirm FUNCTIONS_WORKER_RUNTIME matches code stack.
     - Ensure FUNCTIONS_EXTENSION_VERSION is supported and compatible with extensions in use.

4. Deployment Slot Analysis (if supported by SKU)
   - Identify available slots and whether traffic routing is in use.
   - Compare slot configurations:
     - App settings with stickiness (“slot settings”) vs. production.
     - Connection strings and identity assignments per slot.
     - Extension bundle versions and worker/runtime versions.
   - Highlight any mismatches that can alter trigger behavior or dependency access after swap.

5. Recommendations and Resolution
   - Provide prioritized, actionable steps grouped by Critical, High, and Medium priority.
   - Include exact setting names, expected formats, and example values where safe.
   - For Event Grid scenarios, include a brief “Create Subscription” runbook:
     - Determine container/prefix scope.
     - Construct endpoint URL.
     - Choose BlobCreated event.
     - Apply path filter.
     - Validate delivery and function invocations.
   - Outline validation steps and success criteria after applying changes.

## Instructions and Best Practices
- Maintain configuration-as-code with reviews enforcing required settings and versions.
- Use slot settings for secrets and environment-specific values; validate slot parity before swaps.
- Prefer managed identities over connection strings where supported; assign least-privilege roles.
- Standardize on TLS 1.2+ and align FUNCTIONS_EXTENSION_VERSION and extension bundles to supported versions.
- For Event Grid with Blob triggers:
  - Use precise path filters to limit noise and ensure performance.
  - Keep the Blob extension key secure; rotate periodically.
  - Monitor Event Grid delivery metrics and function invocation logs for confirmation.

## Output Requirements
- Present complete findings without truncation.
- Use structured lists or simple tables for configuration comparisons and recommendations.
- Bold critical configuration values and error conditions, for example:
  - **Missing setting: AzureWebJobsStorage**
  - **Incorrect TLS Min Version: 1.0 (expected: 1.2+)**
- Maintain a clear step-by-step progression through the analysis.

## Error Handling
- Report only permission-related errors that fully block analysis, specifying the resource and scope affected.
- When blocked, propose alternative checks or information needed to proceed.
- Do not disclose platform or SKU limitations; focus on observable data and actionable next steps.

## Operational Guidelines
- Before each major check, state its purpose in one concise line and the expected outcome.
- After each check, validate findings in 1–2 lines and decide to proceed or adjust course.
- Continue until all pertinent configuration areas are covered; avoid stopping at uncertainty.

## Examples
- Blob Trigger using Event Grid not firing
  - Finding: **No Event Grid subscription** on storage account for container “images”.
  - Action: Create subscription with:
    - Event Type: BlobCreated
    - Endpoint Type: Webhook
    - Endpoint URL: https://myfuncapp.azurewebsites.net/runtime/webhooks/blobs?functionName=Host.Functions.ProcessImage&code=<key>
    - Filter: /blobServices/default/containers/images/blobs/raw/
  - Follow-up: Verify Event Grid delivery metrics and function invocation counts.

- Slot swap introduced failures
  - Finding: **Slot-only setting mismatch**: FUNCTIONS_EXTENSION_VERSION differs between “staging” and “production”.
  - Action: Align extension version; mark as slot setting where appropriate; revalidate trigger behavior before swap.

## See Also
- Connectivity and authentication troubleshooting: [function_app_connectivity.md](function_app_connectivity.md)
- Execution failure diagnostics: [function_app_execution_failures.md](function_app_execution_failures.md)
- Deployment checks and validation: [function_app_deployment_checker.md](function_app_deployment_checker.md)
- WEBSITE_RUN_FROM_PACKAGE and package accessibility: [run_from_package.md](run_from_package.md)
