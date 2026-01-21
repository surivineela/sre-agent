---
name: logs_resource_discovery
description: Load this skill when a user asks where logs/metrics/traces for an Azure resource are going, how to find its connected Application Insights / Log Analytics workspace / Data Explorer cluster, needs instrumentation keys or connection strings, wants to confirm diagnostic settings coverage, or reports missing / unclear logging configuration. Also use it to inventory monitoring infrastructure before suggesting query or analysis steps. After discovery, if the user shifts to deep performance or incident diagnosis, defer to the relevant domain skill; this skill focuses on discovering and enumerating logging & monitoring attachments, not root cause analysis.
tools:
  - GetResourceDetailedProperties
  - SearchResource
  - ListResourcesByType
  - SearchResourceByName
  - GetResourceIdForResourceName
  - ListResourceGroups
  - VisualizeApplicationComponents
---

# Logs Resource Discovery Skill

## Purpose

Discover and enumerate logging & monitoring infrastructure attached to a specific Azure resource: Application Insights components, Log Analytics workspaces, Azure Data Explorer (Kusto) clusters, and diagnostic settings. Output actionable identifiers (resource IDs, workspace IDs, instrumentation keys / connection strings) plus gaps & misconfigurations. This skill focuses on discovery; deeper incident diagnosis or performance analysis should pivot to other domain skills once the logging landscape is mapped.

## Core Capabilities

- Locate Application Insights, Log Analytics workspaces, Data Explorer clusters related to the target resource.
- Inspect diagnostic settings for destinations & coverage (logs + metrics categories enabled or missing).
- Parse resource properties, app settings, environment variables for logging references (keys, connection strings).
- Search related monitoring resources by type, name pattern, tags, same RG/subscription affinity.
- Visualize relationships (resource ↔ monitoring components).
- Recommend next discovery-dependent steps (which query tool to use, what to enable, where to look next).

## Discovery Focus

- Application Insights: instrumentation key / connection string; confirm reference source (direct config vs app settings).
- Log Analytics: workspace IDs referenced via diagnostic settings or agents.
- Data Explorer (Kusto): clusters ingesting diagnostics for the resource type.
- Diagnostic Settings: destinations, categories enabled vs missing; absence of settings.
- Affinity & Relationships: collocated resources (same RG/subscription) matching naming/tag patterns.
- User Input: request a known monitoring resource ID only if automated enumeration yields none.

## Workflow

1. Context: GetResourceDetailedProperties(resourceId) to understand type + current diagnostic settings.
2. Direct References: scan properties/app settings/env vars for AI connection string, instrumentation key, workspace IDs, cluster URIs.
3. Diagnostic Settings: enumerate & capture destinations (Log Analytics / Event Hub / Storage / Partner) and note missing categories.
4. Enumerate Monitoring Resources:
   - ListResourcesByType for Microsoft.Insights/components, Microsoft.OperationalInsights/workspaces, Microsoft.Kusto/clusters (same RG then subscription).
   - SearchResourceByName & SearchResource for naming patterns (-ai-, -insights-, -logs-, -la-), tags, and inferred environment labels.
5. Relationship Map: VisualizeApplicationComponents with discovered resources.
6. Gap Handling: If none found, ask user for expected logging target (e.g., known workspace or AI resource ID) then re-run minimal checks.
7. Output & Next Steps: Provide IDs, connection strings (avoid exposing secrets beyond what is already surfaced in configuration), and recommend enabling or adjusting diagnostic settings, or querying via appropriate tools (monitor / kusto) only if the user requests analysis.

## Resource-Type Notes

- Web / Function Apps: prioritize Application Insights & diagnostic settings (AppServiceHTTPLogs, AppServicePlatformLogs).
- Container Apps / AKS: look for workspace linkage (ContainerInsights) & cluster / environment diagnostic settings.
- VMs: confirm Azure Monitor agent / legacy Log Analytics agent workspace mapping.
- Storage / Databases: verify diagnostic settings + event routing (Log Analytics / Event Hub / Storage).

## Discovery Strategies

### Application Insights

- List Microsoft.Insights/components (RG → subscription).
- Search tags & app settings for keys/connection strings: APPLICATIONINSIGHTS_CONNECTION_STRING, InstrumentationKey.
- Pattern-match names: -ai-, -insights-, -appinsights-.

### Log Analytics Workspaces

- List Microsoft.OperationalInsights/workspaces.
- Cross-check diagnostic settings referencing workspace IDs.
- Match naming or tags aligning with service/environment.

### Data Explorer (Kusto)

- List Microsoft.Kusto/clusters.
- Identify clusters receiving diagnostic data for relevant services.

## Tool Usage Patterns

- Start: GetResourceDetailedProperties.
- Enumerate types: ListResourcesByType.
- Pattern & tag search: SearchResourceByName, SearchResource.
- Name → ID resolution: GetResourceIdForResourceName (if user supplies partial names).
- Scope context: ListResourceGroups (if RG uncertain or user asks for cross-RG scan).
- Relationship diagram: VisualizeApplicationComponents.
- Query (only after user asks for analysis): monitor (Log Analytics), kusto (Data Explorer), datadog (Datadog).

## Recommendations Guidance

Use discovered artifacts to direct next steps:

- Application issues: point to Application Insights (traces, exceptions, performance) + any missing diagnostic categories.
- Infrastructure / platform: advise Log Analytics queries for VM/AKS/container logs & metrics.
- Missing logging: provide ordered steps to create diagnostic settings (target workspace / AI) & enable required categories.
- Performance telemetry not found: recommend enabling relevant metrics/log categories & re-run discovery.

## Example Minimal Flow

1. GetResourceDetailedProperties(resourceId)
2. ListResourcesByType (AI components in RG)
3. ListResourcesByType (OperationalInsights workspaces in RG)
4. Check diagnostic settings → capture destinations & missing categories
5. SearchResourceByName for pattern "{baseName}-ai" and "{baseName}-logs"
6. VisualizeApplicationComponents (resource + discovered monitoring assets)
7. Output IDs + note gaps (e.g., "No diagnostic settings found; enable categories X,Y via Diagnostic Settings")

## Best Practices

- Return concrete identifiers (resource IDs, workspace IDs) prioritizing immediate usability.
- Highlight gaps succinctly: "No diagnostic settings", "Application Insights referenced but component not found", etc.
- Avoid speculative analysis; keep scope to discovery unless user explicitly pivots.
- Preserve security: do not fabricate secrets; surface only existing keys/strings already in configuration context.
- Suggest enabling retention/alerting only after confirming a workspace/component exists.
