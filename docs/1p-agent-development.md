# 1P Agent Development Guidelines

> Note: All the 3P agent development guidelines are applicable. 

### Permissions
- [Join TM-SREAgent-Dev SG](https://coreidentity.microsoft.com/manage/entitlement/entitlement/tmsreagentde-y5h0)

## Table of Contents
- [Architecture](./architecture.md)
    - [Basics of Data-Plane APIs](./dataplane-api.md)
- [Development Setup](./development-setup.md)
    - [Development Setup FAQs](./development-setup-faqs.md)
- [Running the Agent](./running-the-app.md)
    - [Approval Setup](#approval-setup)
    - [AppSettings Configuration](#appsettings-configuration)
- [How to Add a Sub-Agent](#how-to-add-a-sub-agent)

---

## Running the Agent (specific to 1P agent)

### Approval Setup
Approval setup is not required as the 1P agent works without any approval as of now.

### AppSettings Configuration
The only `appsettings.development.json` (gitignored) configuration you need to set up is as follows:

```json
{
    "AppSettings": {
        "EnvPrefix": "envPrefix", // example: ramithar
        "Core": {
            "External": {
                "ICMWorkflows": {
                    "Enabled": false,
                    "UserToken": "<output of AAPT-Antares-OperationalAgent\\src\\Agent\\FirstPartyAgent.Core\\FirstPartySubAgents\\ACA\\ContainerAppICMAgent> .\\GetWorkflowToken.ps1>" // needed only when Enabled is true
                }
            }
        }
    }
}
```
### Update launch.setting
Go to `Agent.Web/Properties/launchSetting.json` and update/add below environment variables
```
"IS_FIRST_PARTY": "1",
"AGENT_NAME": "RCAAgent"
```
> Note: this should be part of gitignored. DO NOT COMMIT AS IT WILL BREAK 3P Agent experience.

---

## How to Add a Sub-Agent

- Follow the steps documented for 3P sub-agents [here](./adding-a-sub-agent.md).
- Use the `HelloWorld` Agent as a reference and required boilerplate code files .
- Refer to the ContainerApps DNS or Revision sub-agent to understand more development patterns.

