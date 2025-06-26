#!/bin/bash
set -e

# Assign positional arguments to variables
subscription=${1:-""}
region=${2:-""}
resourceGroup=${3:-""}
agentName=${4:-""}
includeFirstPartyConfiguration=${5:-""}
version=${6:-""}

# Prompt for subscription, region, and resource group
if [ -z "$subscription" ]; then
    read -p "Enter the Azure subscription ID or name: " subscription
fi

if [ -z "$region" ]; then
    read -p "Enter the Azure region (e.g., eastus): " region
fi

if [ -z "$resourceGroup" ]; then
    read -p "Enter the Azure resource group name: " resourceGroup
fi

if [ -z "$agentName" ]; then
    read -p "Enter the SRE Agent name: " agentName
fi

if [ -z "$version" ]; then
    # The version can be found here: https://msazure.visualstudio.com/One/_build?definitionId=421313&_a=summary
    read -p "Enter the Docker image version (e.g., 1.0.123): " version
fi

# Set the subscription
echo "🔑 Setting Azure subscription to '$subscription'..."
az account set --subscription "$subscription"
echo "✅ Subscription set successfully!"

# Check if the resource group exists, and create it if it doesn't
if ! az group show --name "$resourceGroup" &>/dev/null; then
    echo "📦 Resource group '$resourceGroup' does not exist. Creating it..."
    az group create --name "$resourceGroup" --location "$region"
    echo "✅ Resource group created!"
else
    echo "📦 Resource group '$resourceGroup' already exists."
fi

# Prompt for includeFirstPartyConfiguration if not provided
if [ -z "$includeFirstPartyConfiguration" ]; then
    read -p "Include FirstPartyConfiguration? (true/false): " includeFirstPartyConfiguration
    if [ -z "$includeFirstPartyConfiguration" ]; then
        includeFirstPartyConfiguration="false" # Default to false
    fi
fi

# Fetch the managed identity clientId
echo "🔍 Fetching the managed identity clientId..."
managedIdentitySubscription="be8d491e-109c-4ee1-aaee-dc7615af0a42"
managedIdentityName="ACA1PAgent-uami"
managedIdentityResourceGroup="ACA1PAgent-rg"

echo "🔄 Switching to subscription '$managedIdentitySubscription' to fetch the managed identity..."
az account set --subscription "$managedIdentitySubscription"

managedIdentityClientId=$(az identity show --name "$managedIdentityName" --resource-group "$managedIdentityResourceGroup" --query "clientId" -o tsv || true)

if [ -z "$managedIdentityClientId" ]; then
    echo "❌ Error: Managed Identity not found in resource group $managedIdentityResourceGroup in subscription $managedIdentitySubscription"
    exit 1
fi

echo "✅ Managed Identity clientId: $managedIdentityClientId"

# Switch back to the original subscription
echo "🔄 Switching back to subscription '$subscription'..."
az account set --subscription "$subscription"

# Authenticate with Azure if needed
echo "🔐 Checking Azure authentication..."
if ! az account show &>/dev/null; then
    echo "🔑 Azure authentication required. Logging in..."
    az login
    echo "✅ Authentication successful!"
fi

# Use the official build image with specified version
echo "🐳 Using official Docker image..."
imageName="sreagentprod.azurecr.io/sre-agent-web:$version"
echo "✅ Using official image: $imageName"

# Deploy the Microsoft.App/agent resource
echo "🚀 Deploying Microsoft.App/agent resource..."
echo "Running the deployment command..."

set -x
az deployment group create \
    --name "rcaagent-deployment-dev-0" \
    --resource-group "$resourceGroup" \
    --template-file "rcaagent.bicep" \
    --parameters location="$region" \
                 managedIdentitySubscriptionId="$managedIdentitySubscription" \
                 managedIdentityNameForKustoAccess="$managedIdentityName" \
                 managedIdentityResourceGroupName="$managedIdentityResourceGroup" \
                 agentName="$agentName" \
                 agentImage="$imageName" \
                 includeFirstPartyConfiguration="$includeFirstPartyConfiguration"
set +x

echo "🎉 Deployment completed successfully with imageName: ${imageName} 🚀🚀🚀"
exit 0