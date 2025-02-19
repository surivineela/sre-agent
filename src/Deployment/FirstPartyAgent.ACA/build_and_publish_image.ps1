$ErrorActionPreference = "Stop"

Write-Host "Building and publishing solution"
dotnet build "..\..\Agent\FirstPartyAgent.ACA.Web\FirstPartyAgent.ACA.Web.csproj" -c Release --interactive
dotnet publish "..\..\Agent\FirstPartyAgent.ACA.Web\FirstPartyAgent.ACA.Web.csproj" --no-build -o out/publish --interactive

Write-Host "Building and pushing image"
$ContainerRegistryEndpoint = $(azd env get-value AZURE_CONTAINER_REGISTRY_ENDPOINT)
$AzdEnvName = $(azd env get-value AZURE_ENV_NAME)
$currentTime = Get-Date -Format "yyyyMMddHHmmss"
$imageName = "$ContainerRegistryEndpoint/${AzdEnvName}:$currentTime"
docker build -t "$imageName" out/publish -f Dockerfile
az acr login -n $ContainerRegistryEndpoint
docker push "$imageName"
azd env set IMAGE_TO_DEPLOY $imageName