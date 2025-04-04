# TlsBestPracticesAgent Eval Scenarios

## Scenario 1 - Five Apps

System finds five apps that accept TLS 1.0, lists the apps and recommends to user to update them to TLS 1.2.

Once the user approves, the agent starts updating each app one by one, monitoring app health as it goes and provides regular status updates.

### Verbosity
1. User approves without further input
1. User says "Only send me updates when something goes wrong, or when you're done" then approves
1. User approves and then mid rollout says "Don't tell me about health checks that are successful."

### Timing
1. User says "do it during the weekend" then approves.
1. User says "wait a day between each update" then approves.
1. User says "my boss wants this ASAP, skip the health checks and update them all now" then approves.

### Exclusion
1. User says "don't update app foo or bar, the rest are fine" then approves.

### Test vs Prod
1. User says "only do the ones that have 'test' in their name, leave the rest alone" then approves
1. User says "only do apps that are in resource groups with 'test' in their name" then approves
1. User says "don't touch the prod resources, only do test" then approves
1. User says "do all the test apps first, then wait a day before starting with the prod apps" then approves

### Ordering
1. User says "do app foo first, then the rest" then approves
1. User says "do the ones that are in east asia first" then approves
1. User says "do the ones that are in east asia first, then the ones in west us, then the rest" then approves
1. user says "do it in this order" and then pastes a list of app names, then approves

### Policy Changes
1. User says "abort the whole rollout if any app fails a health check" and then approves.
1. User says "if any apps fail a health check, don't just rollback that app, rollback everything and abort the rollout" and then approves.
1. User says "if any app fails a health check, complete the rollback but then pause the rest of the update and wait for my input" and then approves.
1. User says "if any app fails a health check, complete the rollback and keep going, but if this happens for 2 or more apps, abort the rollout" and then approves

## Scenario 2 - 50 Apps

System finds 50 apps that accept TLS 1.0, lists the apps and recommends to user to update them to TLS 1.2.

### Batching
1. User says "break it up into regional batches, start with east asia and wait a day between each region" then approves
1. User says "batch them by prefix, 'rpt' apps go first then 'api' then 'fe'. finish each prefix batch globally before moving to the next' then approves

## TODO
1. Something about the agent knowing the user's preferred SDP speed?
1. Something about the agent know the user's preferred region ordering?
1. Multi step evals