#!/bin/bash
set -e

# Default values
IMAGE_NAME="agent-web"
IMAGE_TAG=$(date +%Y%m%d%H%M)
RESOURCE_GROUP="agent-web-rg"
LOCATION="eastus"
APP_NAME="agent-web-app"
ENVIRONMENT_NAME="agent-web-env"
PRIVATE_STAMP_ENV_PREFIX="jianbosun"

# Enable Docker BuildKit
export DOCKER_BUILDKIT=1

# Load environment variables if .env file exists
if [ -f .env ]; then
    echo "Loading environment variables from .env"
    source .env
fi

# Check required environment variables
if [ -z "$ACR_REGISTRY" ]; then
    echo "Error: ACR_REGISTRY environment variable is required"
    echo "Please set it to your Azure Container Registry (e.g., myregistry.azurecr.io)"
    exit 1
fi


# Print configuration
echo "Building and deploying with the following configuration:"
echo "ACR Registry: $ACR_REGISTRY"
echo "Image Name: $IMAGE_NAME"
echo "Image Tag: $IMAGE_TAG"
echo "Resource Group: $RESOURCE_GROUP"
echo "Location: $LOCATION"
echo "App Name: $APP_NAME"
echo "Environment Name: $ENVIRONMENT_NAME"
echo "Private Stamp env prefix: $PRIVATE_STAMP_ENV_PREFIX"
echo "Managed Identity Resource ID: $MI_RESOURCE_ID"
echo "Managed Identity Resource ID for Kusto: $MI_KUSTO_RESOURCE_ID"
echo "Managed Identity Client ID for Kusto: $MI_KUSTO_CLIENT_ID"

echo "Logging in to Azure Container Registry..."
# Get ACR access token and extract required values
ACCESS_TOKEN_JSON=$(az acr login --name "${ACR_REGISTRY}" --expose-token --output json)
ACCESS_TOKEN=$(echo $ACCESS_TOKEN_JSON | jq -r '.accessToken')

# Login to Docker with the token
echo "Logging in to Docker with ACR token..."
docker login $ACR_REGISTRY -u "00000000-0000-0000-0000-000000000000" -p $ACCESS_TOKEN

# Get managed identity details and extract client ID and tenant ID
echo "Retrieving managed identity details..."
if [ -z "$MI_RESOURCE_ID" ]; then
    echo "Error: MI_RESOURCE_ID environment variable is required"
    echo "Please set it to your managed identity resource ID"
    exit 1
fi

MI_DETAILS=$(az identity show --ids $MI_RESOURCE_ID --query "{clientId:clientId, tenantId:tenantId}" -o json)
MI_CLIENT_ID=$(echo $MI_DETAILS | jq -r '.clientId')
MI_TENANT_ID=$(echo $MI_DETAILS | jq -r '.tenantId')

# Validate the managed identity values
if [ -z "$MI_CLIENT_ID" ] || [ -z "$MI_TENANT_ID" ]; then
    echo "Error: Unable to retrieve managed identity details"
    echo "Please verify that MI_RESOURCE_ID is correct and you have permissions to access it"
    exit 1
fi

echo "Successfully retrieved managed identity details"
echo "Client ID: $MI_CLIENT_ID"
echo "Tenant ID: $MI_TENANT_ID"


# Get script directory
SCRIPT_DIR=$(dirname "$0")
REPO_ROOT=$(realpath "${SCRIPT_DIR}/../../..")
PROJECT_DIR="${REPO_ROOT}/src/Agent/Agent.Web"

# Clean and create publish directory
PUBLISH_DIR="${SCRIPT_DIR}/publish"
echo "Creating clean publish directory at ${PUBLISH_DIR}..."
rm -rf "${PUBLISH_DIR}"
mkdir -p "${PUBLISH_DIR}"

# Build the .NET application locally
echo "Building .NET application locally..."
dotnet publish "${PROJECT_DIR}/Agent.Web.csproj" \
    -c Release \
    -o "${PUBLISH_DIR}" \
    /p:UseAppHost=false

# Build the Docker image with platform flag
echo "Building Docker image using local build artifacts..."
docker build --platform linux/amd64 -t ${IMAGE_NAME}:${IMAGE_TAG} -f "${SCRIPT_DIR}/Dockerfile" "${SCRIPT_DIR}"/publish

# Tag the image for the registry
FULL_IMAGE_NAME="${ACR_REGISTRY}/${IMAGE_NAME}:${IMAGE_TAG}"
echo "Tagging image as ${FULL_IMAGE_NAME}..."
docker tag ${IMAGE_NAME}:${IMAGE_TAG} ${FULL_IMAGE_NAME}

# Push the image to ACR
echo "Pushing image to ACR..."
docker push ${FULL_IMAGE_NAME}

# Check if resource group exists, create if it doesn't
echo "Ensuring resource group ${RESOURCE_GROUP} exists..."
if ! az group show -n ${RESOURCE_GROUP} &>/dev/null; then
    echo "Creating resource group ${RESOURCE_GROUP}..."
    az group create --name ${RESOURCE_GROUP} --location ${LOCATION}
fi

# Check if Container App Environment exists, create if it doesn't
echo "Ensuring Container App Environment ${ENVIRONMENT_NAME} exists..."
if ! az containerapp env show --name ${ENVIRONMENT_NAME} --resource-group ${RESOURCE_GROUP} &>/dev/null; then
    echo "Creating Container App Environment ${ENVIRONMENT_NAME}..."
    az containerapp env create \
        --name ${ENVIRONMENT_NAME} \
        --resource-group ${RESOURCE_GROUP} \
        --location ${LOCATION}
fi

# Check if the Container App exists, if not create it, otherwise update it
if ! az containerapp show --name ${APP_NAME} --resource-group ${RESOURCE_GROUP} &>/dev/null; then
    echo "Creating Container App ${APP_NAME}..."
    az containerapp create \
        --name ${APP_NAME} \
        --resource-group ${RESOURCE_GROUP} \
        --environment ${ENVIRONMENT_NAME} \
        --image ${FULL_IMAGE_NAME} \
        --target-port 8080 --ingress external \
        --min-replicas 1 \
        --user-assigned $MI_RESOURCE_ID $MI_KUSTO_RESOURCE_ID \
        --env-vars AppSettings__EnvPrefix=$PRIVATE_STAMP_ENV_PREFIX \
        ASPNETCORE_ENVIRONMENT=Development \
        AppSettings__ManagedIdentityClientId=$MI_CLIENT_ID \
        AppSettings__Core__External__TeamsBot__AppId=$MI_CLIENT_ID \
        AppSettings__Core__External__TeamsBot__AppType=UserAssignedMsi \
        AppSettings__Core__External__TeamsBot__TenantId=$MI_TENANT_ID \
        AppSettings__Core__External__Kusto__Auth__AuthenticationType=UAMI \
        AppSettings__Core__External__Kusto__Auth__ManagedIdentityClientId=$MI_KUSTO_CLIENT_ID \
        IS_FIRST_PARTY=1 \
        AGENT_TYPE_NAME=ACAAgent \
        --query properties.configuration.ingress.fqdn
else
    echo "Updating Container App ${APP_NAME} with new image..."
    az containerapp update \
        --name ${APP_NAME} \
        --resource-group ${RESOURCE_GROUP} \
        --min-replicas 1 \
        --set-env-vars AppSettings__EnvPrefix=$PRIVATE_STAMP_ENV_PREFIX \
        ASPNETCORE_ENVIRONMENT=Development \
        AppSettings__ManagedIdentityClientId=$MI_CLIENT_ID \
        AppSettings__Core__External__TeamsBot__AppId=$MI_CLIENT_ID \
        AppSettings__Core__External__TeamsBot__AppType=UserAssignedMsi \
        AppSettings__Core__External__TeamsBot__TenantId=$MI_TENANT_ID \
        AppSettings__Core__External__Kusto__Auth__AuthenticationType=UAMI \
        AppSettings__Core__External__Kusto__Auth__ManagedIdentityClientId=$MI_KUSTO_CLIENT_ID \
        IS_FIRST_PARTY=1 \
        AGENT_TYPE_NAME=ACAAgent \
        --image ${FULL_IMAGE_NAME}
fi

# Define colors for better readability
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[0;33m'
BOLD='\033[1m'
RESET='\033[0m'

echo -e "${GREEN}${BOLD}Deployment completed successfully!${RESET}"
echo -e "${BLUE}Monitor the deployment with:${RESET} ${YELLOW}az containerapp show -n ${APP_NAME} -g ${RESOURCE_GROUP} --query properties.latestRevisionName${RESET}"

# Get the FQDN of the Container App
APP_URL=$(az containerapp show --name ${APP_NAME} --resource-group ${RESOURCE_GROUP} \
    --query "properties.configuration.ingress.fqdn" -o tsv)

# Get the latest revision name
LATEST_REVISION=$(az containerapp show --name ${APP_NAME} --resource-group ${RESOURCE_GROUP} \
    --query "properties.latestRevisionName" -o tsv)

if [ ! -z "$APP_URL" ]; then
    echo -e "${GREEN}${BOLD}Service is accessible at:${RESET} ${GREEN}https://${APP_URL}${RESET}"
    echo -e "${BLUE}Note: It may take a few minutes for the service to be fully available${RESET}"
    echo -e "\n${BOLD}Useful commands:${RESET}"
    echo -e "${BLUE}View all container logs:${RESET} ${YELLOW}az containerapp logs show -n ${APP_NAME} -g ${RESOURCE_GROUP}${RESET}"
    echo -e "${BLUE}View logs for specific revision:${RESET} ${YELLOW}az containerapp logs show -n ${APP_NAME} -g ${RESOURCE_GROUP} --revision ${LATEST_REVISION} --follow${RESET}"
    echo -e "${BLUE}List all replicas:${RESET} ${YELLOW}az containerapp revision list-replicas -n ${APP_NAME} -g ${RESOURCE_GROUP} --revision ${LATEST_REVISION}${RESET}"
else
    echo -e "${BLUE}Could not determine Container App URL. Once deployment is complete,${RESET}"
    echo -e "${BLUE}you can view the URL with:${RESET} ${YELLOW}az containerapp show -n ${APP_NAME} -g ${RESOURCE_GROUP} --query properties.configuration.ingress.fqdn${RESET}"
    echo -e "\n${BOLD}Useful commands:${RESET}"
    echo -e "${BLUE}View all container logs:${RESET} ${YELLOW}az containerapp logs show -n ${APP_NAME} -g ${RESOURCE_GROUP}${RESET}"
    echo -e "${BLUE}View logs for specific revision:${RESET} ${YELLOW}az containerapp logs show -n ${APP_NAME} -g ${RESOURCE_GROUP} --revision ${LATEST_REVISION}${RESET}"
    echo -e "${BLUE}List all revisions:${RESET} ${YELLOW}az containerapp revision list -n ${APP_NAME} -g ${RESOURCE_GROUP}${RESET}"
fi
