Param(
    [bool]$IsProd = $true
)

$ErrorActionPreference = "Stop"

Write-Host "Building and publishing solution"
dotnet build "..\..\Agent\Agent.Web\Agent.Web.csproj" -c Release --interactive
dotnet publish "..\..\Agent\Agent.Web\Agent.Web.csproj" --no-build -o out/publish --interactive

Write-Host "Building image"
if ($IsProd) {
    $containerRegistryEndpoint = "k8seprod.azurecr.io"
    $tag = Get-Date -Format "yyyyMMddHHmmss"
    Write-Host "Building production image"
} else {
    $containerRegistryEndpoint = "sreagent.azurecr.io"
    $tag = "1.0.224" # This version is enforced by https://github.com/serverless-paas-balam/sreagent-infra/blob/main/.github/workflows/build-deploy.yml#L71
    Write-Host "Building development image"
}
# $AzdEnvName = $(azd env get-value AZURE_ENV_NAME)
$imageName = "$containerRegistryEndpoint/agent-web:$tag"
# $imageName = "$containerRegistryEndpoint/agent-web:$currentTime"
docker build -t "$imageName" out/publish -f Dockerfile