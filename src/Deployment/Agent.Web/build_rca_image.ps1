$ErrorActionPreference = "Stop"

Write-Host "Building and publishing solution"
# dotnet build "..\..\Agent\Agent.Web\Agent.Web.csproj" -c Release --interactive
dotnet publish "..\..\Agent\Agent.Web\Agent.Web.csproj" -o out/publish
Copy-Item ".\.dockerignore" -Destination "out/publish" -Force

Write-Host "Building image"
$containerRegistryEndpoint = "rcaagentacrdogfood.azurecr.io"
$gitCommit = (git rev-parse HEAD).Substring(0, 7)
$tag = "$gitCommit" # This version prefix is enforced by https://github.com/serverless-paas-balam/sreagent-infra/blob/main/.github/workflows/build-deploy.yml#L71
Write-Host "Building development image"
# $AzdEnvName = $(azd env get-value AZURE_ENV_NAME)
$imageName = "$containerRegistryEndpoint/rca-agent-dogfood-web:$tag"
# $imageName = "$containerRegistryEndpoint/agent-web:$currentTime"
docker build -t "$imageName" out/publish -f Dockerfile-1p