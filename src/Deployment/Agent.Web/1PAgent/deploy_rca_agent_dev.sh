#!/bin/bash
set -e

# Assign positional arguments to variables
subscription=${1:-""}
region=${2:-""}
resourceGroup=${3:-""}
agentName=${4:-""}
acrName=${5:-""}
includeFirstPartyConfiguration=${6:-""}

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

# Build and publish the .NET application
echo "🔨 Building and publishing the .NET solution..."
dotnet build "..\..\..\Agent\Agent.Web\Agent.Web.csproj" -c Release --interactive
dotnet publish "..\..\..\Agent\Agent.Web\Agent.Web.csproj" -o out/publish --interactive
echo "✅ Build and publish completed!"

# Set the subscription
echo "🔑 Setting Azure subscription to '$subscription'..."
az account set --subscription "$subscription"
echo "✅ Subscription set successfully!"

# Prompt for ACR name, generate one if not provided
if [ -z "$acrName" ]; then
    read -p "Enter the Azure Container Registry name (leave blank to auto-generate): " acrName
    if [ -z "$acrName" ]; then
        acrName="acr$(date +%s)" # Generate a unique ACR name using the current timestamp
        echo "✨ No ACR name provided. Generated ACR name: $acrName"
    fi
fi

# Check if the resource group exists, and create it if it doesn't
if ! az group show --name "$resourceGroup" &>/dev/null; then
    echo "📦 Resource group '$resourceGroup' does not exist. Creating it..."
    az group create --name "$resourceGroup" --location "$region"
    echo "✅ Resource group created!"
else
    echo "📦 Resource group '$resourceGroup' already exists."
fi

# Check if the ACR exists, and create it if it doesn't
if ! az acr show --name "$acrName" --resource-group "$resourceGroup" &>/dev/null; then
    echo "🛠️ Azure Container Registry '$acrName' does not exist. Creating it..."
    az acr create --name "$acrName" --resource-group "$resourceGroup" --sku Premium --location "$region" --admin-enabled true
    echo "✅ ACR created successfully!"
    echo "🔓 Enabling anonymous pull access for ACR '$acrName'..."
    az acr update --name "$acrName" --anonymous-pull-enabled true
    echo "✅ Anonymous pull access enabled!"
else
    echo "🛠️ Azure Container Registry '$acrName' already exists."
fi

# Prompt for includeFirstPartyConfiguration if not provided
if [ -z "$includeFirstPartyConfiguration" ]; then
    read -p "Include FirstPartyConfiguration? (true/false): " includeFirstPartyConfiguration
    if [ -z "$includeFirstPartyConfiguration" ]; then
        includeFirstPartyConfiguration="false" # Default to false
    fi
fi

# Fetch the ACR endpoint
echo "🌐 Fetching the ACR endpoint..."
acrEndpoint=$(az acr show --name "$acrName" --query "loginServer" -o tsv)
echo "✅ ACR endpoint: $acrEndpoint"

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

# Fetch the ACR credentials
echo "🔑 Fetching ACR credentials..."
registryUserName=$(az acr credential show --name "$acrName" --query "username" -o tsv || true)
registryPassword=$(az acr credential show --name "$acrName" --query "passwords[0].value" -o tsv || true)

if [ -z "$registryUserName" ] || [ -z "$registryPassword" ]; then
    echo "❌ Error: Failed to retrieve ACR credentials. Ensure the ACR exists and you have the necessary permissions."
    exit 1
fi
echo "✅ ACR credentials fetched successfully!"

# Build the Docker image
echo "🐳 Building Docker image for RCA agent..."
dockerTag=$(date -u +"%y.%m.%d-%H-%M-%SUTC")
imageName="$acrEndpoint/rcaagent-web:$dockerTag"
docker build -t "$imageName" out/publish -f Dockerfile
echo "✅ Docker image built: $imageName"

# Push the Docker image to ACR
echo "📤 Pushing Docker image to ACR..."
if ! docker push "$imageName"; then
    echo "🔑 Docker push failed. Attempting to log in to ACR..."
    echo "$registryPassword" | docker login "$acrEndpoint" --username "$registryUserName" --password-stdin
    docker push "$imageName"
fi
echo "✅ Docker image pushed successfully!"

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
                 registryUserName="$registryUserName" \
                 registryPassword="$registryPassword"\
                 includeFirstPartyConfiguration="$includeFirstPartyConfiguration"
set +x

echo "🎉 Deployment completed successfully with imageName: ${imageName}! 🚀🚀🚀"
exit 0