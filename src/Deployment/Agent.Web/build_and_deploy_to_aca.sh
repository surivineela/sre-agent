#!/bin/bash
set -e

# Default values
IMAGE_NAME="agent-web"
IMAGE_TAG=$(date +%Y%m%d%H%M)
RESOURCE_GROUP="agent-web-rg"
LOCATION="eastus"
APP_NAME="agent-web"
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
echo "Managed Identity Client ID: $MI_CLIENT_ID"

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
        --env-vars AppSettings__EnvPrefix=$PRIVATE_STAMP_ENV_PREFIX \
        ASPNETCORE_ENVIRONMENT=Development \
        AppSettings__ManagedIdentityClientId=$MI_CLIENT_ID \
        AppSettings__Core__External__TeamsBot__AppId=$MI_CLIENT_ID \
        AppSettings__Core__External__TeamsBot__AppType=UserAssignedMsi \
        AppSettings__Core__External__TeamsBot__TenantId=$MI_TENANT_ID \
        --query properties.configuration.ingress.fqdn
    az containerapp identity assign --name ${APP_NAME} --resource-group ${RESOURCE_GROUP} --system-assigned
else
    echo "Updating Container App ${APP_NAME} with new image..."
    az containerapp update \
        --name ${APP_NAME} \
        --resource-group ${RESOURCE_GROUP} \
        --set-env-vars AppSettings__EnvPrefix=$PRIVATE_STAMP_ENV_PREFIX \
        ASPNETCORE_ENVIRONMENT=Development \
        AppSettings__ManagedIdentityClientId=$MI_CLIENT_ID \
        AppSettings__Core__External__TeamsBot__AppId=$MI_CLIENT_ID \
        AppSettings__Core__External__TeamsBot__AppType=UserAssignedMsi \
        AppSettings__Core__External__TeamsBot__TenantId=$MI_TENANT_ID \
        --image ${FULL_IMAGE_NAME}
fi

echo "Deployment completed successfully!"
echo "Monitor the deployment with: az containerapp show -n ${APP_NAME} -g ${RESOURCE_GROUP} --query properties.latestRevisionName"

# Wait for Container App to be ready
echo "Waiting for Container App to be ready..."
TIMEOUT=30 # 30 seconds timeout
INTERVAL=3 # 3 seconds interval
ELAPSED=0

while [ $ELAPSED -lt $TIMEOUT ]; do
    # Get deployment status
    APP_STATUS=$(az containerapp show --name ${APP_NAME} --resource-group ${RESOURCE_GROUP} \
        --query "properties.latestRevisionStatus" -o tsv 2>/dev/null)

    # Check if app is ready
    if [[ "$APP_STATUS" == "Running" ]]; then
        echo "Container App is running and ready!"
        break
    fi

    # If we've reached the timeout, exit with error
    if [ $ELAPSED -ge $TIMEOUT ]; then
        echo "Error: Timed out waiting for Container App to be ready."
        echo "Current status: $APP_STATUS"
        echo "Check the app with: az containerapp show -n ${APP_NAME} -g ${RESOURCE_GROUP}"
        exit 1
    fi

    echo "Waiting for Container App to be ready... (${ELAPSED}s/${TIMEOUT}s)"
    sleep $INTERVAL
    ELAPSED=$((ELAPSED + INTERVAL))
done

# Get the FQDN of the Container App
APP_URL=$(az containerapp show --name ${APP_NAME} --resource-group ${RESOURCE_GROUP} \
    --query "properties.configuration.ingress.fqdn" -o tsv)

# Get the latest revision name
LATEST_REVISION=$(az containerapp show --name ${APP_NAME} --resource-group ${RESOURCE_GROUP} \
    --query "properties.latestRevisionName" -o tsv)

if [ ! -z "$APP_URL" ]; then
    echo "Service is accessible at: https://${APP_URL}"
    echo "Note: It may take a few minutes for the service to be fully available"
    echo "View all container logs: az containerapp logs show -n ${APP_NAME} -g ${RESOURCE_GROUP}"
    echo "View logs for specific revision: az containerapp logs show -n ${APP_NAME} -g ${RESOURCE_GROUP} --revision ${LATEST_REVISION}"
    echo "List all replicas: az containerapp revision list-replicas -n ${APP_NAME} -g ${RESOURCE_GROUP} --revision ${LATEST_REVISION}"
else
    echo "Could not determine Container App URL. Once deployment is complete,"
    echo "you can view the URL with: az containerapp show -n ${APP_NAME} -g ${RESOURCE_GROUP} --query properties.configuration.ingress.fqdn"
    echo "View all container logs: az containerapp logs show -n ${APP_NAME} -g ${RESOURCE_GROUP}"
    echo "View logs for specific revision: az containerapp logs show -n ${APP_NAME} -g ${RESOURCE_GROUP} --revision ${LATEST_REVISION}"
    echo "List all revisions: az containerapp revision list -n ${APP_NAME} -g ${RESOURCE_GROUP}"
fi
