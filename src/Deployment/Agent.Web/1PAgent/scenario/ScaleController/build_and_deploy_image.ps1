# Set environment variables
$ACR_NAME = "tsushiagent" # e.g., mycontainerregistry
# Update note for Corp Teant Link with fix GetIncident for local execution.
$IMAGE = "$ACR_NAME.azurecr.io/myrepo/agent:v1.15" # e.g., mycontainerregistry.azurecr.io/myrepo/agent:v1
# Log in to Azure Container Registry
az acr login --name $ACR_NAME
# Navigate to agent source code directory
# cd C:\sre\sreagent-runtime\src\Agent 
Set-Location (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..\..\Agent'))
# Publish the .NET project
dotnet publish "Agent.Web/Agent.Web.csproj" -o out/publish
# Build the Docker image
docker build -t $IMAGE out/publish -f "../../src/Deployment/Agent.Web/1PAgent/scenario/ScaleController/Dockerfile"
# Push the image to ACR
docker push $IMAGE
