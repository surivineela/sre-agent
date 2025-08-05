<#
.SYNOPSIS
Builds and publishes a .NET CLI tool to an Azure DevOps Artifacts feed.

.DESCRIPTION
This script:
- Builds your .NET CLI tool using `dotnet pack`
- Pushes the resulting `.nupkg` to an Azure Artifacts NuGet feed
- Uses the Azure DevOps credential provider (no PAT required)

.PARAMETER FeedUrl
The Azure DevOps Artifacts NuGet feed URL (v3 endpoint).

.PARAMETER PackageDir
The output folder for the `.nupkg`. Default is `./nupkg`.

.For custom feed url
.\build_and_publish.ps1 -FeedUrl "https://pkgs.dev.azure.com/myorg/myproject/_packaging/myfeed/nuget/v3/index.json"
#>

param (
    [string]$FeedUrl = "https://pkgs.dev.azure.com/msazure/One/_packaging/SREAgentCli/nuget/v3/index.json",

    [string]$PackageDir = "./nupkg"
)

# Ensure .NET SDK is available
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error "The .NET SDK is not installed or not available in PATH."
    exit 1
}

# Get the script directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Project directory is one level up from script directory
$projectDir = Join-Path $scriptDir ".."

# Path to your .csproj file (adjust name as needed)
$projectFile = Join-Path $projectDir "Agent.Cli.csproj"

# Output directory for nupkg
$fullPackageDir = Resolve-Path (Join-Path $projectDir $PackageDir) -ErrorAction SilentlyContinue
if (-not $fullPackageDir) {
    New-Item -ItemType Directory -Path (Join-Path $projectDir $PackageDir) -Force | Out-Null
    $fullPackageDir = Resolve-Path (Join-Path $projectDir $PackageDir)
}

Write-Host "Building and packing the tool..."

# Pack as .NET global tool (PackAsTool=true by default)
dotnet pack $projectFile --configuration Release --output $fullPackageDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to pack the tool. Exit code: $LASTEXITCODE"
    exit $LASTEXITCODE
}

# Find the .nupkg file
$packagePath = Get-ChildItem -Path $fullPackageDir -Filter *.nupkg | Sort-Object LastWriteTime -Descending | Select-Object -First 1

if (-not $packagePath) {
    Write-Error "No .nupkg file found in $fullPackageDir"
    exit 1
}

Write-Host "Found package: $($packagePath.Name)"

# Push to Azure Artifacts using credential provider (no PAT required)
Write-Host "Pushing package to Azure Artifacts feed..."
dotnet nuget push $packagePath.FullName `
    --source $FeedUrl `
    --api-key "AzureDevOps" `
    --skip-duplicate

if ($LASTEXITCODE -eq 0) {
    Write-Host "Package pushed successfully."
} else {
    Write-Error "Failed to push the package. Exit code: $LASTEXITCODE"
    exit $LASTEXITCODE
}
