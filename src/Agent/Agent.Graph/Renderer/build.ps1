# push.ps1 - Build, tag, and push the Docker image to ACR
# Prerequisites:
#   - Docker installed and running
#   - Azure CLI installed and logged in (run `az login` before executing this script)
#   - Dockerfile in the current directory

$registryName = "dailyreportacr"
$imageName = "mermaid-api"
$imageTag = "latest"
$acrLoginServer = "$registryName.azurecr.io"

Write-Host "Building Docker image..."
docker build -t "$imageName:latest" .

Write-Host "Tagging Docker image..."
docker tag "$imageName:latest" "$acrLoginServer/$imageName:$imageTag"

Write-Host "Logging in to ACR..."
az acr login --name $registryName

Write-Host "Pushing Docker image to ACR..."
docker push "$acrLoginServer/$imageName:$imageTag"

Write-Host "Docker image pushed successfully: $acrLoginServer/$imageName:$imageTag"
