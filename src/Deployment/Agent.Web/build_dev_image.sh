#!/bin/bash

# Parse command line arguments
while getopts "r:" opt; do
    case $opt in
        r) CONTAINER_REGISTRY="$OPTARG";;
        *) echo "Usage: $0 -r <ContainerRegistry>" >&2; exit 1;;
    esac
done

# Check if required parameter is provided
if [ -z "$CONTAINER_REGISTRY" ]; then
    echo "Error: ContainerRegistry parameter is required"
    echo "Usage: $0 -r <ContainerRegistry>"
    exit 1
fi

# Exit on any error
set -e

# Login to Azure Container Registry
echo "Logging in to ACR: $CONTAINER_REGISTRY"
az acr login --name "${CONTAINER_REGISTRY%%.*}" || { echo "Failed to login to ACR"; exit 1; }

echo "Building and publishing solution"
dotnet build "../../Agent/Agent.Web/Agent.Web.csproj" -c Release --interactive
dotnet publish "../../Agent/Agent.Web/Agent.Web.csproj" --no-build -o out/publish --interactive
cp "./.dockerignore" "out/publish/" -f

echo "Building image"
GIT_COMMIT=$(git rev-parse HEAD | cut -c1-7)
TIMESTAMP=$(date +%s%N | cut -b6-13)
TAG="1.0.224-$GIT_COMMIT-$TIMESTAMP" # This version prefix is enforced by https://github.com/serverless-paas-balam/sreagent-infra/blob/main/.github/workflows/build-deploy.yml#L71
echo "Building development image"

IMAGE_NAME="$CONTAINER_REGISTRY/agent-web:$TAG"
docker build -t "$IMAGE_NAME" out/publish -f Dockerfile

echo "Pushing image to registry"
docker push "$IMAGE_NAME"

# Get ACR login server, username, and password
ACR_LOGIN_SERVER=$(az acr show --name "${CONTAINER_REGISTRY%%.*}" --query loginServer -o tsv)
ACR_USERNAME=$(az acr credential show --name "${CONTAINER_REGISTRY%%.*}" --query username -o tsv)
ACR_PASSWORD=$(az acr credential show --name "${CONTAINER_REGISTRY%%.*}" --query passwords[0].value -o tsv)

# Print configuration
cat <<EOF
"firstPartyConfiguration": {
  "agentImageConfiguration": {
    "imageName": "$ACR_LOGIN_SERVER/agent-web:$TAG",
    "registryUserName": "$ACR_USERNAME",
    "registryPassword": "$ACR_PASSWORD"
  }
}
EOF