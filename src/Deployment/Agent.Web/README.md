# Agent.Web Deployment Guide for Tests

This guide explains how to deploy the Agent.Web application to Azure Container Apps (ACA) using the provided deployment script. The purpose for this deployment is mainly for testing integration with teams.

## Prerequisites

- Azure CLI installed and logged in
- Docker installed and running
- .NET SDK installed
- Access to an Azure Container Registry (ACR), create one if you don't have it
- Permissions to create resources in the target Azure subscription

## Setup Instructions

### 1. Configure Environment Variables

Copy the `.env.template` file to a new file named `.env` and update the values:

```bash
cp .env.template .env
```

Required configuration parameters:

| Variable                 | Description                                                   | Required |
|--------------------------|---------------------------------------------------------------|----------|
| ACR_REGISTRY             | Azure Container Registry URL (e.g., your-registry.azurecr.io) | Yes      |
| ENVIRONMENT_NAME         | Azure Container App Environment name                          | Yes      |
| RESOURCE_GROUP           | Azure Resource Group name                                     | Yes      |
| REGION                   | Azure Region (e.g., australiaeast)                            | Yes      |
| ACR                      | Azure Container Registry name                                 | Yes      |
| PRIVATE_STAMP_ENV_PREFIX | Prefix for your private stamp environment                     | Yes      |
| MI_CLIENT_ID             | Managed Identity Client ID                                    | Yes      |
| MI_TENANT_ID             | Managed Identity Tenant ID                                    | Yes      |

### 2. Run the Deployment Script

Execute the deployment script, you can run it multiple times for local development and quick testing:

```bash
./build_and_deploy_to_aca.sh
```

The script will:
1. Build the .NET application
2. Create a Docker image
3. Push the image to your ACR
4. Create or update a resource group if needed
5. Create or update the Container App Environment
6. Deploy the application to Azure Container Apps
7. Configure the application with system-assigned managed identity
8. Display the application URL and monitoring commands

## Required Permissions

### Managed Identity Configuration

⚠️ **IMPORTANT**: After deployment, you must grant the managed identity appropriate permissions:

1. **App Configuration Access**:
   - Grant the managed identity "App Configuration Data Reader" role on your App Configuration instance

2. **Key Vault Access**:
   - Grant the managed identity "KeyVault Secret User" role in your Key Vault

3. **Resource Group Access**:
   - If needed, grant appropriate roles on the resource group level and other resources like app service for demo.

## Deploying Changes to RCA Agent

### Prerequisites

1. **Register Required AFEC Flags**  
   Ensure the following AFEC flags are registered for the subscription where you create the agent:
   - `Microsoft.App/SREAgentPreview`
   - `Microsoft.Resources/EUAPParticipation`  

   For more details on how to register AFEC flags, refer to the documentation:  
   [Register AFEC Flags](https://eng.ms/docs/cloud-ai-platform/devdiv/serverless-paas-balam/serverless-paas-vikr/azure-container-apps/container-apps-documentation/azurearc/deployment/production/registerafec)

2. **Create a User-Assigned Managed Identity (UAMI)**  
   Ensure a UAMI is created and has read access to the Kusto clusters.

3. **Azure Container Registry (ACR)**  
   Ensure an Azure Container Registry (ACR) is created and accessible.

To deploy changes to the RCA agent, follow these steps:

### 1. Update the .NET Application
- Ensure the following values are **hardcoded** in the application(this will not be required once these appsettings are pulled directly from the keyvault config):
  - **Application Insights Connection String**: Set the `appsettings.Core.Azure.AppInsights.ConnectionString` in appsettings.json .
  - **IS_FIRST_PARTY**: Set this value to `true` or `false` as required. (This is currently hardcoded in the Program.cs file)
  - **AGENT_NAME**: Set the name of the agent (e.g., `RCAAgent`). (This is currently hardcoded in the FirstPartyAgentsFactor.cs)

### 2. Ensure a UAMI Has Access to the Kusto Clusters
- Create a User-Assigned Managed Identity (UAMI) and grant it access to the Kusto clusters:
  1. Create the managed identity:
     ```bash
     az identity create --name <identity-name> --resource-group <resource-group-name>
     ```
  2. Grant the identity read access to the `cappsdb` tables by running a Kusto deployment.
  3. Note the `clientId` of the managed identity and update the `aca-kusto.json` file in the `FirstPartyAgents.Core` project.

- Update the following fields in `aca-kusto.json` under the FirstPartyAgent.Core project:
  - **`clientId`**: Set this to the `clientId` of the managed identity created in the previous step.
  - **`authType`**: Set this to `UAMI` (User-Assigned Managed Identity).

Example `Auth` configuration:
```json
"Auth": {
  "AuthenticationType": "UAMI",
  "Authority": "",
  "AuthorityHost": "",
  "ApplicationClientId": "",
  "ApplicationCertificate": "",
  "ManagedIdentityClientId": "5cc4f734-a234-443c-9092-04e06a2b80c2"
}
```

### 3. Build and Publish the Application and Deploy the Agent

- Use the following command to publish the .NET application and build the Docker image:
  ```bash
  ./deploy_rca_agent_dev.sh <subscriptionId> <location> <rg name> <acr name> <includeFirstPartyConfig>
  ```

  **Example Command**:
  ```bash
  ./deploy_rca_agent_dev.sh 79ab50cf-1b41-4b24-a33f-26c8940f4469 swedencentral tdarolyrcaagentrg rcaagentacrtdaroly true
  ```

  **Error Scenario**:
  If the agent creation fails with the following error:
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

  **Workaround**:
  If you are using a new subscription to create the agent, you should send a payload without `FirstPartyConfiguration` to bypass the first-party validation and record your subscription ID in the Cosmos DB. After this, you can create the agent as desired without encountering this error.

  To achieve this:
  - Set `includeFirstPartyConfig` to `false` in the first script run.
  - Once the subscription is validated, set `includeFirstPartyConfig` to `true` for subsequent runs.

  **Example Commands**:
  - First Run (to bypass validation):
    ```bash
    ./deploy_rca_agent_dev.sh 79ab50cf-1b41-4b24-a33f-26c8940f4469 swedencentral tdarolyrcaagentrg rcaagentacrtdaroly false
    ```

  - Subsequent Runs (after validation):
    ```bash
    ./deploy_rca_agent_dev.sh 79ab50cf-1b41-4b24-a33f-26c8940f4469 swedencentral tdarolyrcaagentrg rcaagentacrtdaroly true
    ```

---

### 4. Modifying the First Party Configuration

The Bicep template used to create and deploy this agent is `rcaagent.bicep`. Note that to include the custom image, these fields need to be populated.

**Example Configuration**:
```json
"firstPartyConfiguration": {
  "agentImageConfiguration": {
    "imageName": "rcaagentacr.azurecr.io/rcaagent/agent:v2.4",
    "registryUserName": "rcaagentacr", 
    "registryPassword": "redacted"
  }
}

---
