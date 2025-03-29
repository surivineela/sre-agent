$ErrorActionPreference = "Stop"

Write-Host "Building and publishing solution"
dotnet build "..\..\Agent\Agent.MetricsExporter\Agent.MetricsExporter.csproj" -c Release --interactive
dotnet publish "..\..\Agent\Agent.MetricsExporter\Agent.MetricsExporter.csproj" --no-build -o out/publish --interactive

Write-Host "Building image"
$ContainerRegistryEndpoint = "k8seprod.azurecr.io"
# $AzdEnvName = $(azd env get-value AZURE_ENV_NAME)
$currentTime = Get-Date -Format "yyyyMMddHHmmss"
$imageName = "$ContainerRegistryEndpoint/agent-metricsexporter:$currentTime"
docker build -t "$imageName" out/publish -f Dockerfile