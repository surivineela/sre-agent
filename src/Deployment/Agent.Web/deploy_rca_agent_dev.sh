#!/bin/bash
set -e

# Assign positional arguments to variables
subscription=${1:-""}
region=${2:-""}
resourceGroup=${3:-""}
acrName=${4:-""}
includeFirstPartyConfiguration=${5:-""}

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

# Set the subscription
echo "Setting Azure subscription to '$subscription'..."
az account set --subscription "$subscription"

# Prompt for ACR name, generate one if not provided
if [ -z "$acrName" ]; then
    read -p "Enter the Azure Container Registry name (leave blank to auto-generate): " acrName
    if [ -z "$acrName" ]; then
        acrName="acr$(date +%s)" # Generate a unique ACR name using the current timestamp
        echo "No ACR name provided. Generated ACR name: $acrName"
    fi
fi

# Check if the resource group exists, and create it if it doesn't
if ! az group show --name "$resourceGroup" &>/dev/null; then
    echo "Resource group '$resourceGroup' does not exist. Creating it..."
    az group create --name "$resourceGroup" --location "$region"
else
    echo "Resource group '$resourceGroup' already exists."
fi

# Check if the ACR exists, and create it if it doesn't
if ! az acr show --name "$acrName" --resource-group "$resourceGroup" &>/dev/null; then
    echo "Azure Container Registry '$acrName' does not exist. Creating it..."
    az acr create --name "$acrName" --resource-group "$resourceGroup" --sku Premium --location "$region" --admin-enabled true
    echo "Enabling anonymous pull access for ACR '$acrName'..."
    az acr update --name "$acrName" --anonymous-pull-enabled true
else
    echo "Azure Container Registry '$acrName' already exists."
fi

# Prompt for includeFirstPartyConfiguration if not provided
if [ -z "$includeFirstPartyConfiguration" ]; then
    read -p "Include FirstPartyConfiguration? (true/false): " includeFirstPartyConfiguration
    if [ -z "$includeFirstPartyConfiguration" ]; then
        includeFirstPartyConfiguration="false" # Default to false
    fi
fi

# Fetch the ACR endpoint
acrEndpoint=$(az acr show --name "$acrName" --query "loginServer" -o tsv)

# Fetch the managed identity clientId (this is hardcoded for now, but we can create a global dev UAMI and add it to list of readers and reference that here)
managedIdentitySubscription="be8d491e-109c-4ee1-aaee-dc7615af0a42"
managedIdentityName="ACA1PAgent-uami"
managedIdentityResourceGroup="ACA1PAgent-rg"
managedIdentityClientId=$(az identity show --name "$managedIdentityName" --resource-group "$managedIdentityResourceGroup" --query "clientId" -o tsv || true)

echo "Switching to subscription '$managedIdentitySubscription' to fetch the managed identity..."
az account set --subscription "$managedIdentitySubscription"

managedIdentityClientId=$(az identity show --name "$managedIdentityName" --resource-group "$managedIdentityResourceGroup" --query "clientId" -o tsv || true)

if [ -z "$managedIdentityClientId" ]; then
    echo "Error: Managed Identity not found in resource group $managedIdentityResourceGroup in subscription $managedIdentitySubscription"
    exit 1
fi

if [ -z "$managedIdentityClientId" ]; then
    echo "Error: Managed Identity not found in resource group $managedIdentityResourceGroup"
    exit 1
fi

# Switch back to the original subscription
echo "Switching back to subscription '$subscription'..."
az account set --subscription "$subscription"

# Authenticate with Azure if needed
echo "Checking Azure authentication..."
if ! az account show &>/dev/null; then
    echo "Azure authentication required. Logging in..."
    az login
fi

# Fetch the ACR credentials
echo "Fetching ACR credentials..."
registryUserName=$(az acr credential show --name "$acrName" --query "username" -o tsv || true)
registryPassword=$(az acr credential show --name "$acrName" --query "passwords[0].value" -o tsv || true)

if [ -z "$registryUserName" ] || [ -z "$registryPassword" ]; then
    echo "Error: Failed to retrieve ACR credentials. Ensure the ACR exists and you have the necessary permissions."
    exit 1
fi

# Build and publish the .NET application
echo "Building and publishing solution..."
dotnet build "..\..\Agent\Agent.Web\Agent.Web.csproj" -c Release --interactive
dotnet publish "..\..\Agent\Agent.Web\Agent.Web.csproj" -o out/publish --interactive

# Build the Docker image
echo "Building image for RCA agent..."
currentTime=$(date +%Y%m%d%H%M%S)
imageName="$acrEndpoint/rcaagent-web:$currentTime"
docker build -t "$imageName" out/publish -f Dockerfile

# Push the Docker image to ACR
echo "Pushing Docker image to ACR..."
if ! docker push "$imageName"; then
    echo "Docker push failed. Attempting to log in to ACR..."
    echo "$registryPassword" | docker login "$acrEndpoint" --username "$registryUserName" --password-stdin
    docker push "$imageName"
fi

# Deploy the Microsoft.App/agent resource
echo "Deploying Microsoft.App/agent resource..."
echo "Running the following deployment command:"

set -x
az deployment group create \
    --name "rcaagent-deployment-dev-0" \
    --resource-group "$resourceGroup" \
    --template-file "rcaagent.bicep" \
    --parameters location="$region" \
                 managedIdentitySubscriptionId="$managedIdentitySubscription" \
                 managedIdentityNameForKustoAccess="$managedIdentityName" \
                 managedIdentityResourceGroupName="$managedIdentityResourceGroup" \
                 agentImage="$imageName" \
                 registryUserName="$registryUserName" \
                 registryPassword="$registryPassword"\
                 includeFirstPartyConfiguration="$includeFirstPartyConfiguration"
set +x

echo "Deployment completed successfully!"