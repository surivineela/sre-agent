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
