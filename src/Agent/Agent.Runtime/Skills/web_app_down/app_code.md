# Application Code Mitigation (Deployment‑Induced Issues)

Mitigate Azure App Service Web App availability or error spikes directly caused by a recent deployment or slot swap. Goal: restore stability fast (rollback), then ensure configuration/runtime parity before a corrected rollout.

## Use Criteria (Referenced by Main Skill)

Open this file ONLY when the Downtime Diagnosis root cause is deployment‑induced application exceptions or the user explicitly requests rollback guidance.

## Rapid Rollback (Slot Swap Revert)

1. Identify last swap direction (e.g., staging → production). Issues started immediately after? Plan to revert.
2. Execute swap back (Portal or CLI) and allow warm‑up. Example CLI pattern:
   - az webapp deployment slot swap --resource-group [resource-group] --name [appName] --slot staging --target-slot production
3. Ensure warm‑up + health checks (WEBSITE_SWAP_WARMUP_PING_PATH / STATUSES) are configured to minimize cold starts.
4. Communicate concise status; pause further changes until availability/error rate normalize.
5. Validate recovery: availability >=99.9%, 5xx back to baseline, exceptions reduced.

## Deeper Remediation (When Rollback Not Possible)

1. Correlate deployment timestamps with error onset (SCM/Kudu logs, Activity Logs).
2. Catalog new exceptions (full stack traces) introduced post‑deployment.
3. Common causes:
   - Config: missing/incorrect app settings, slot stickiness errors.
   - Runtime: wrong stack/version, 32/64‑bit mismatch, startup command misconfig.
   - Dependencies: missing assemblies/native libs, breaking library changes.
   - External services: endpoint/firewall/DNS changes not mirrored across slots.
   - Startup: failing health probes, long cold start initialization.
4. Stabilize: redeploy last known good artifact OR hotfix in staging → validate → swap.
5. Temporarily disable auto‑swap if it would promote unstable builds.
6. Parity checklist before re‑release:
   - App settings & connection strings (slot settings correct)
   - Managed identity / Key Vault references
   - VNet integration / firewall rules
   - Runtime versions & bitness aligned
   - Schema migrations safe and idempotent
7. Improve safety: health checks, warm‑up ping path, minimize heavy startup tasks.
8. Re‑validate availability, errors, exceptions, performance (CPU/memory/threads).

## Monitoring & Verification Targets

- Availability >=99.9% (30 min window)
- 5xx/error rate returns to baseline
- Previously dominant exceptions no longer present or significantly reduced
- Resource metrics within normal historical ranges
- User confirms service stability

## Communication Snippets

- Acknowledgment: "Issue detected after recent deployment swap; reverting for stability."
- Rollback action: "Reverted swap; warming up for ~3 minutes."
- Recovery validation: "Availability restored; errors normalized. Preparing corrected deployment plan."

## Prevention Best Practices

- Always validate in a non‑production slot (warm‑up + health checks) before swapping.
- Maintain a last known good artifact for immediate rollback.
- Use gradual (canary) traffic routing for high‑risk changes.
- Enforce configuration parity via automated checks (IaC pipelines).
- Mark sensitive settings as slot‑sticky; avoid unintended promotion.
- Automate smoke tests pre‑swap and post‑swap.

## Reference

This file augments the Downtime Diagnosis skill for deployment‑induced application exception mitigation. Diagnosis sequence and when to open this file are defined in the main skill.
