# FirstPartyAgent Deployment Guide

## Prerequisites
- Azure CLI (az)
- Azure Developer CLI (azd)
- Docker
- Access to internal Nuget feed

## Deployment Files
Located in `src/Deployment/FirstPartyAgent`:
- azure.yaml (azd configuration)
- infra folder (bicep files)
- Dockerfile

## Deployment Steps

1. **Preparation**
   - Navigate to deployment directory:
     ```bash
     cd src/Deployment/FirstPartyAgent
     ```
   - Login to azd:
     ```bash
     azd auth login --scope https://management.azure.com//.default
     ```
   - Select target subscription:
     ```bash
     az account set --subscription <target-subscription>
     ```

2. **First-time Deployment**
   - Create new azd environment:
     ```bash
     azd env new
     ```
   - Provision Azure resources:
     ```bash
     azd provision
     ```
   > **Note**: Increase gpt-4o deployment quota in Azure Portal to avoid 429 errors

3. **Build and Deploy**
   - Run `build_and_publish_image.ps1` to build and push Docker image
   - Run `azd provision` to deploy the image

## Production Deployment
- Contact yefwuang, zhenquan.xu, xiangy for configuration files
- Use subscription "Container Apps Operational Agent (be8d491e-109c-4ee1-aaee-dc7615af0a42)"

[Back to Graph Visualization](graph-visualization.md) 