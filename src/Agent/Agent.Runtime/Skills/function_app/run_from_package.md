# WEBSITE_RUN_FROM_PACKAGE Diagnostics and Repair

## Purpose
Diagnose, verify, and repair WEBSITE_RUN_FROM_PACKAGE issues (invalid/unsupported mode, inaccessible or malformed package, missing functions). Prioritize functional breakages before optimizations; apply strict masking for sensitive data. Usage conditions reside in `SKILL.md`.

## Security Rules (Mandatory)

- Never display SAS tokens, keys, or secrets.
- Mask sensitive URL params: <https://account.blob.core.windows.net/container/app.zip?sp=***MASKED***>.
- Validate accessibility/structure without exposing credentials.
- Require explicit user authorization before updating settings.
- Update only after consent AND valid structure confirmed.

## Core Concepts

Modes: None | LocalPackage | ExternalUrl | Invalid.
Function App: host.json + function folders with function.json.
Web App: web.config + app content.
SKU: verify mode supported; distinguish broken vs suboptimal.

## Diagnostic Workflow

1. Assess: identify app + SKU; capture current setting (masked); determine mode; note functional symptoms.
2. Configuration: validate completeness (non-empty, .zip, well-formed URL); classify Must-Fix vs Optimization.
3. SKU: confirm mode supported; flag incompatibilities as broken.
4. Accessibility (ExternalUrl): DNS + reachability; token presence/expiry (not shown); file existence; typical issues = expired SAS / wrong path / blocked network. Use connectivity file for network/auth problems.
5. Structure: validate ZIP (host.json, function folders + function.json; web.config for Web Apps). Block changes until valid.
6. Categorize & impact: Configuration | Storage | Network | Permissions | SKU. Impact: Critical / High / Medium / Low. Reference connectivity or deployment files as needed.
7. Repair (needs consent): validate structure & URL durability; then fix setting (mode, URL, path); replace expired URL; adjust plan or mode. Post-change validate startup, function enumeration, error rate, accessibility.
8. Report: separate Must-Fix vs High vs Optimizations; confidence level; pre/post state summary.

## Best Practices

- Immutable, versioned blobs; no overwrite-in-place.
- Short-lived least-privilege SAS; rotate; never reveal.
- Validate structure in CI.
- Runtime-aligned layout (host.json root; web.config where needed).
- Private endpoints + DNS alignment; use slots for staged rollout; post-deploy health checks.
- Track artifact provenance (build ID, commit SHA).

## Common Issues

Expired SAS → 403 / failures → new SAS (consent) → update (masked) → validate.
Wrong blob path → 404 → correct path → verify existence.
Invalid ZIP → missing host.json/function.json → rebuild → validate locally → update.
Unsupported mode → ignored/fails → switch to supported or adjust plan.
Network restrictions → timeouts → review firewall/private endpoint/DNS (see connectivity).
Pipeline drift → old/missing artifact → enforce versioning + publish validation (see deployment checker).

## Output & Communication

- Mask sensitive URL params.
- If file missing: show masked path.
- Prioritize actions with confidence levels.
- After each step: brief validation + next decision.

## Examples

Zip download failed → ExternalUrl + 403/404 → expired SAS → new SAS (masked) → validate availability.
Missing functions → invalid structure (no host.json/function.json) → rebuild → update → confirm enumeration.
Intermittent startup (Premium) → storage throttling → optimize storage / retries / consider LocalPackage.

## Related References

Connectivity: [function_app_connectivity.md](function_app_connectivity.md) | Deployment: [function_app_deployment_checker.md](function_app_deployment_checker.md) | Configuration: [function_app_configuration_checker.md](function_app_configuration_checker.md)
