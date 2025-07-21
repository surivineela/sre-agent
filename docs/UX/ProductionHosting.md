# Production hosting

This document contains info about our hosting/infra in production.

**NOTE:** DELETE (and slowly Create/all?) operations against PROD resources are now expected to be done through Ev2 or Geneva/Jarvis actions as they contain logic/safeguards to ensure things aren't still being used, etc. I don't have all the details, but if we need to delete, or maybe even risk-ily modify something, this needs further looking-into. See: https://eng.ms/docs/quality/compliant-automated-touches/systems/codedsharedoperationswalkthrough/deletestorageacctwalkthrough

- [JIT doc](https://msazure.visualstudio.com/One/_git/AAPT-SREAgent-ControlPlane?path=%2Fdocs%2Ftroubleshooting.md&_a=preview)
- **Subscription:** `a413c07b-f487-441a-a5c9-796961d41baa` (SRE Agent Portal CDN sub)
- **Resource group:** `RscGrp_EastUS`

## CDN (Azure Front Door over Storage Account)

- Usage:
  - Icons
- Storage account: `sreagentassetstorage`
  - Performance: Standard
    - Random finding: Premium perf accounts will inexplicably fail when trying to enable "Static websites" (portal and CLI)
  - Redundancy: Geo-zone-redundant storage (RA-GZRS)
  - Post-create:
    - Data management -> Static website -> Enabled
    - Containers -> `$web` -> *insert stuff here*
      - `icons`
- AFD: `sreagent-assets`
  - Tier: Premium
  - Endpoint name: `sreagent-assets`
  - Origin type: Storage (Static website)
  - Origin host name: `sreagentstorage.z13.web.core.windows.net`
  - Enable caching + query string behavior to "Ignore query string"
  - WAF policy -> Create new `sreagentassetswafpolicy` + "Add bot protection"
  - Post-create:
    - WAF policy:
      - Managed rules -> Assign -> Microsoft_DefaultRuleSet_2.1 + Microsoft.BotManagerRuleSet_<latest>
      - Custom rules -> Add custom rule:
        - Custom rule name: `ratelimit`
        - Rule type: Rate limit
        - Priority: 100000
        - Rate limit duration: 5 minutes
        - Country/Region: Unknown
    - Rule sets:
      - Rule set name: `cacheruleset`
      - Rule name: `cache1hrrule`
      - Add action -> Route configuration override -> Enable caching + "Ignore query string" + Disabled compression + Override always + 1Hours
      - -> Front Door manager -> `default-route` -> Apply `cacheruleset` rules/rule set
- Endpoints:
  - Storage account static website: TODO
  - AFD endpoint: https://sreagent-assets-c7e0h4enanawfphk.b02.azurefd.net

### Configuration

**NOTE**: Only had to run the register commands through AzCLI before portal creates for the storage account and AFD would work (portal had a deny policy on that for some reason)

1. `az login` (AME)
1. `az account set --subscription a413c07b-f487-441a-a5c9-796961d41baa`
1. `az provider register --namespace Microsoft.Storage` -> same for `Microsoft.Cdn` and `Microsoft.Network`
1. `az group create --name <rsc-grp-name> --location <location>`
1. `az storage account create --name sreagentassetstorage --resource-group RscGrp_EastUS --location eastus --sku Standard_ZRS --kind StorageV2 --min-tls-version TLS1_2 --allow-blob-public-access false`
  - NOTE: Command (specifically SKU) not confirmed
1. `az storage blob service-properties update --account-name sreagentassetstorage --static-website`
1. Didn't bother with AFD commands
