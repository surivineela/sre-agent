# Azure Container Apps HTTP Error Diagnostics

## Overview
Diagnose and resolve HTTP errors encountered when accessing Azure Container Apps endpoints. Analyze HTTP status codes (e.g., 400, 401, 403, 404, 408, 429, 500, 502, 503, 504), networking and ingress configuration, authentication, SSL/TLS and custom domains, and egress rules. Provide a clear root-cause analysis and remediation steps. When user-driven actions are preferred, provide step-by-step guidance and Azure CLI commands.

## When to Use
- Users report HTTP errors or connectivity failures reaching a Container App endpoint (default domain or custom domain).
- Symptoms include SSL/TLS or certificate errors, authentication/authorization failures, unexpected 4xx/5xx responses, timeouts, or intermittent availability.
- Ingress/egress or network policy changes are suspected.

## Prerequisites and Initial Notes
- Begin with a broad connectivity and network diagnosis using available tools (for example, a “NetworkDiagnosisTool” if available).
- Confirm target resources: subscription, resource group, Container App name, and managed environment.
- Gather recent timestamps, request paths, client IP, and correlation IDs if available.
- The subnet used by Azure Container Apps is retrieved from the managed environment: vnetConfiguration.infrastructureSubnetId.

## Diagnostic Workflow

1. Initial Assessment
   - Identify the exact HTTP code(s), frequency, affected endpoints/paths, and whether the issue is global or scoped to certain regions/users.
   - Determine whether the error occurs on the default ACA FQDN, a custom domain, or both.
   - Capture relevant request/response headers (e.g., Server, Via, x-envoy-upstream-service-time).

2. Resource Validation
   - Verify the Container App is Running and the latest revision is Healthy.
   - Check recent deployments, configuration changes (ingress, auth, secrets, env vars), and traffic splits.
   - Validate probes (liveness/readiness/startup) and their paths/ports; unhealthy probes can surface as 50x/timeout symptoms.

3. Network Analysis
   - Confirm managed environment subnet and inspect NSG rules, route tables (UDRs), and firewall policies for required egress/ingress.
   - Validate external vs. internal ingress settings and whether the app is reachable from the test location.
   - For private environments, verify private DNS and resolution for the ACA endpoint.

4. Deep Dive (Based on Findings)
   - Logs and Metrics: Review application logs, ingress logs, and platform metrics (requests, latency, 4xx/5xx rate, pod restarts).
   - Configuration: Inspect ingress config (targetPort, transport, session affinity, TLS), auth settings (AAD/OIDC), and custom domain/cert bindings.
   - Dependencies: Check upstream services (databases, external APIs) for failures causing 5xx or timeouts.

5. Root Cause and Remediation
   - Synthesize evidence to pinpoint the primary failure domain (client, network, ingress, auth, app, or dependency).
   - Provide corrective actions and, if requested, execute supported changes or present CLI steps with safety checks.
   - Produce a concise report with issue summary, root cause, evidence, and next steps.

## Data to Collect
- Error details: HTTP code, path, method, frequency, client/source IP ranges, timeframe, correlation/request IDs.
- Endpoint type: Default ACA FQDN vs. custom domain; TLS mode; certificate details (issuer, SANs, expiration).
- App revision and status, traffic splits, probes, targetPort, and auth configuration.
- Relevant logs: ingress controller, app logs, platform events, and metrics around incident time.

## Common Error Patterns and Checks

### 400 Bad Request
- Causes: malformed headers, oversized headers/cookies, mismatched host headers on custom domains, invalid request payload.
- Checks:
  - Confirm Host header matches configured domain; inspect reverse proxies/CDN settings.
  - Validate ingress annotations/config; test with curl including explicit Host header for custom domains.
- Remediation: Normalize client headers; adjust upstream/proxy configs; if needed, enable compression or reduce cookie sizes.

### 401/403 Unauthorized/Forbidden
- Causes: misconfigured authentication (AAD/OIDC), missing/expired tokens, IP restrictions, app-level authorization.
- Checks:
  - Inspect auth settings (issuer, audience, callback URLs); verify token scopes/roles.
  - Check any configured allowlists/denylists or WAF policies.
- Remediation: Correct auth provider setup; refresh tokens; align scopes/roles; update access policies.

### 404 Not Found
- Causes: incorrect path, revision routing misconfiguration, app not serving the route, probes hitting wrong path.
- Checks:
  - Validate traffic splits and active revision; confirm path is served by the app and matches ingress rules.
  - Confirm probe paths do not overlap user routes inadvertently.
- Remediation: Fix routing, update revision splits, correct app route registration.

### 408/504 Request Timeout
- Causes: slow upstream dependencies, insufficient replicas/resources, network egress blocks, long server processing.
- Checks:
  - Review latency metrics and app logs around errors; verify dependent endpoints availability and timeouts.
  - Confirm egress routes and NSG/UDR allow outbound traffic to dependencies.
- Remediation: Optimize code/queries, increase timeouts, scale replicas, allow required egress, consider async patterns.

### 429 Too Many Requests
- Causes: rate limiting by app, gateway, or upstream services.
- Checks:
  - Look for rate limit headers; confirm autoscaling adequacy; assess traffic spikes.
- Remediation: Increase capacity/replicas, tune rate limits, enable KEDA-based autoscaling.

### 500 Internal Server Error
- Causes: unhandled exceptions, misconfigurations, bad deployments or secrets.
- Checks:
  - Correlate 500 bursts with recent releases/config changes; inspect stack traces; confirm secrets/env vars.
- Remediation: Fix defects/configs, roll back or redeploy stable revision, add error handling and observability.

### 502/503 Bad Gateway/Service Unavailable
- Causes: backend pod not ready, failing probes, port mismatch, resource exhaustion, network interruptions.
- Checks:
  - Verify targetPort matches container listening port; probe paths return 200; readiness is stable.
  - Check scaling state and restarts; ensure sufficient CPU/memory.
- Remediation: Correct port/probe config, resolve crashes, scale appropriately, fix dependency health.

### TLS/SSL and Custom Domain Issues
- Symptoms: certificate mismatch, expired cert, SNI/hostname mismatch, mixed content, incomplete chain.
- Checks:
  - Validate certificate binding to the custom domain and SAN coverage; verify expiration and issuer chain.
  - Confirm CNAME/A records point to the ACA ingress and DNS TTL has propagated.
- Remediation: Renew/rebind certificates, ensure full chain, correct DNS, align hostnames/SNI.

## Azure CLI Reference

- Get Container App details:
  az containerapp show -n <app-name> -g <rg>

- Get revisions and traffic:
  az containerapp revision list -n <app-name> -g <rg>
  az containerapp ingress show -n <app-name> -g <rg>

- Show managed environment and subnet:
  az containerapp env show -n <env-name> -g <rg>
  # Subnet ID: .vnetConfiguration.infrastructureSubnetId

- Show logs (live and recent):
  az containerapp logs show -n <app-name> -g <rg> --follow
  az containerapp logs show -n <app-name> -g <rg> --revision <rev-name> --container <container>

- Validate custom domain and certificate:
  az containerapp hostname list -n <app-name> -g <rg>
  az containerapp hostname bind -n <app-name> -g <rg> --hostname <domain> --certificate <cert-name>

- Scale replicas (if needed for capacity-related errors):
  az containerapp update -n <app-name> -g <rg> --min-replicas <min> --max-replicas <max>

Note: Replace placeholders with actual resource names. Run read-only commands first, then apply changes with confirmation.

## Reporting Template
- Summary: One-paragraph description of the issue and scope.
- Evidence:
  - HTTP codes and frequency
  - Affected routes/domains and timeframe
  - Relevant logs/metrics snapshots
  - Config snippets (ingress/auth/ports/probes)
- Root Cause: Single, primary cause stated clearly.
- Remediation:
  - Immediate fix applied or step-by-step actions
  - Validation steps and rollback plan
- Next Steps:
  - Monitoring/alerts to prevent recurrence
  - Related configurations to review

## Examples

### Example 1: 502 after deployment
Plan:
- Confirm revision health, probes, and targetPort.
- Review logs for readiness failures and restarts.
- Correlate with deployment time.

Findings:
- Latest revision failing readiness on /healthz (404).
- targetPort set to 8080; app serves on 8000.

Action:
- Update targetPort to 8000 and readiness path to /health.
- Validate: revision becomes Healthy; 502s stop.

### Example 2: 403 on custom domain with AAD
Plan:
- Inspect auth config (issuer/audience), token scopes, and callback URLs.
- Test with curl using valid token.

Findings:
- Audience mismatch between app and token.

Action:
- Correct audience in auth settings (or token request).
- Validate: authenticated requests return 200.

### Example 3: TLS certificate mismatch
Plan:
- Verify hostname binding, certificate SANs, and DNS.
- Check certificate expiration and chain.

Findings:
- Certificate SAN does not include www subdomain.

Action:
- Bind a cert covering both root and www, or update DNS to covered host.
- Validate: browser shows secure connection, no TLS errors.

## Network-Specific Guidance
- Identify the managed environment subnet via vnetConfiguration.infrastructureSubnetId, then validate:
  - NSG rules: allow required ingress/egress; no deny rules blocking outbound to dependencies.
  - Route tables (UDRs): correct routes to internet or private endpoints as required.
  - DNS: confirm private DNS zones and records if using internal ingress or private environments.

## Related Resources

- Latency Diagnostics
  - Use when HTTP errors correlate with high latency, slow upstreams, or timeout patterns. Read [diagnostic_latency.md](diagnostic_latency.md).

- CPU Diagnostics (top-level skill)
  - Use when errors surface during CPU saturation, spikes, or throttling. Refer to the diagnostic_cpu skill for analysis and remediation workflows.

- Memory Diagnostics (top-level skill)
  - Use when OutOfMemory conditions, memory leaks, or high memory pressure coincide with HTTP errors. Refer to the diagnostic_memory skill.

- Metrics and Chart Visualization (top-level skill)
  - Use to visualize request rates, error rates, latency percentiles, and resource metrics in Azure Monitor. Prepare resource IDs and a clear analysis intent. Refer to the metrics_and_chart_visualization skill.
