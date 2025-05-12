# Deployment Guide for RCA Agent

This document provides instructions to deploy changes to the RCA agent in Dogfood, Production, or a private test environment. Always test new agents or configurations in Dogfood before releasing to Production unless the changes are minor (e.g., simple plugins or bug fixes).

---

## Deployment Steps

### Build and Publish the Application
Use the following command to publish the .NET application and build the Docker image. Run this command using WSL or Git Bash Shell:

> Directory Location of running this script: `AAPT-Antares-OperationalAgent/src/Deployment/Agent.Web/1PAgent`

```bash
./deploy_rca_agent_dev.sh <subscriptionId> <location> <resourceGroupName> <sreAgentName> <acrName> <includeFirstPartyConfig>
```
Note: It will ask for Az-login so take required actions.
---

## Deployment Environments

### Dogfood Deployment
Test your changes in the Dogfood environment using the following command:
```bash
./deploy_rca_agent_dev.sh be8d491e-109c-4ee1-aaee-dc7615af0a42 swedencentral ACA1PAgentDogfood-rg RCAAgentDogfood rcaagentacrdogfood true
```

### Production Deployment
Deploy to Production using the following command:
```bash
./deploy_rca_agent_dev.sh be8d491e-109c-4ee1-aaee-dc7615af0a42 swedencentral ACA1PAgent-rg RCAAgent rcaagentacr true
```

---

## Private Test Deployment
Optionally, deploy the agent to a private subscription for testing or debugging in a remote cloud environment. Use the following command:
```bash
./deploy_rca_agent_dev.sh 79ab50cf-1b41-4b24-a33f-26c8940f4469 swedencentral tdarolyrcaagentrg tdarolyrcaagent rcaagentacrtdaroly true
```
Scenarios for private remote test setup:
- When you are setting up new configuration for remote agent for first time
- Stuck due to some other's changes in Dogfood env

Apart from these scenarios, one should always use local agent deployment setup as that is more flexible.

### Common Issues During Private Test Deployment

#### Error: DeploymentFailed
If the private agent creation fails with the following error:
```json
{
  "status": "Failed",
  "error": {
    "code": "DeploymentFailed",
    "target": "/subscriptions/79ab50cf-1b41-4b24-a33f-26c8940f4469/resourceGroups/tdarolyrcaagentrg/providers/Microsoft.Resources/deployments/rcaagent-deployment-dev-0",
    "message": "At least one resource deployment operation failed. Please list deployment operations for details. Please see https://aka.ms/arm-deployment-operations for usage details.",
    "details": [
      {
        "code": "InvalidSubscriptionForFirstPartyConfiguration",
        "message": "Setting the FirstPartyConfigurations is not allowed for subscription '79ab50cf-1b41-4b24-a33f-26c8940f4469'."
      }
    ]
  }
}
```

#### Solution
To resolve this issue:
1. Set `includeFirstPartyConfig` to `false` in the first script run to bypass the first-party validation and record your subscription ID in the Cosmos DB.
2. After validation, set `includeFirstPartyConfig` to `true` for subsequent runs.

**Example Commands:**
- First Run (to bypass validation):
  ```bash
  ./deploy_rca_agent_dev.sh be8d491e-109c-4ee1-aaee-dc7615af0a42 swedencentral ACA1PAgent-rg RCAAgent rcaagentacr false
  ```
- Subsequent Runs (after validation):
  ```bash
  ./deploy_rca_agent_dev.sh be8d491e-109c-4ee1-aaee-dc7615af0a42 swedencentral ACA1PAgent-rg RCAAgent rcaagentacr true
  ```