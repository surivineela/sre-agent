#!/bin/bash
# push.sh - Build, tag, and push the Docker image to ACR
# Prerequisites:
#   - Docker installed and running
#   - Azure CLI installed and logged in (use `az login` before running)
#   - Dockerfile in the current directory

# Variables
REGISTRY_NAME="dailyreportacr"
IMAGE_NAME="mermaid-api"
IMAGE_TAG="latest"
ACR_LOGIN_SERVER="${REGISTRY_NAME}.azurecr.io"

echo "Building Docker image..."
docker build -t ${IMAGE_NAME}:latest .

echo "Tagging Docker image..."
docker tag ${IMAGE_NAME}:latest ${ACR_LOGIN_SERVER}/${IMAGE_NAME}:${IMAGE_TAG}

echo "Logging in to ACR..."
az acr login --name ${REGISTRY_NAME}

echo "Pushing Docker image to ACR..."
docker push ${ACR_LOGIN_SERVER}/${IMAGE_NAME}:${IMAGE_TAG}

echo "Docker image pushed successfully: ${ACR_LOGIN_SERVER}/${IMAGE_NAME}:${IMAGE_TAG}"
