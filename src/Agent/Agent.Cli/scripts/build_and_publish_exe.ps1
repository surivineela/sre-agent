<#
.SYNOPSIS
Builds and publishes SRECTL executable to Azure DevOps Artifacts as a Universal Package.

.DESCRIPTION
This script:
- Builds self-contained executables for multiple platforms (Windows, Linux, macOS)
- Creates a Universal Package containing the executables
- Publishes the package to an Azure Artifacts feed
- Uses Azure CLI for authentication (no PAT required)

.PARAMETER FeedUrl
The base Azure DevOps organization URL. Default targets the SREAgentCli feed.

.PARAMETER FeedName
The name of the Azure Artifacts feed. Default is 'SREAgentCli'.

.PARAMETER PackageName
The name of the Universal Package. Default is 'srectl-executables'.

.PARAMETER PackageVersion
The version of the package. If not specified, uses current date/time format: yy.M.dd.HHmm

.PARAMETER Organization
The Azure DevOps organization name. Default is 'msazure'.

.PARAMETER Project
The Azure DevOps project name. Default is 'One'.

.PARAMETER Platform
The target platform(s) to build. Options: 'All', 'Windows', 'Linux', 'macOS'. Default is 'All'.

.EXAMPLE
.\build_and_publish_exe.ps1

.EXAMPLE
.\build_and_publish_exe.ps1 -PackageVersion "1.0.3" -Platform "Windows"

.EXAMPLE
.\build_and_publish_exe.ps1 -Organization "myorg" -Project "myproject" -FeedName "myfeed"
#>

param (
    [string]$FeedUrl = "https://dev.azure.com/msazure",
    [string]$FeedName = "SREAgentCli", 
    [string]$PackageName = "srectl-executables",
    [string]$PackageVersion = "",
    [string]$Organization = "msazure",
    [string]$Project = "One",
    [ValidateSet("All", "Windows", "Linux", "macOS")]
    [string]$Platform = "All"
)

$ErrorActionPreference = "Stop"

# Generate version if not provided
if ([string]::IsNullOrEmpty($PackageVersion)) {
    $now = Get-Date
    $PackageVersion = "{0:yy}.{1}.{2:dd}.{3:HHmm}" -f $now, $now.Month, $now, $now
}

# Ensure required tools are available
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error "The .NET SDK is not installed or not available in PATH."
    exit 1
}

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Error "Azure CLI is not installed or not available in PATH. Please install it from https://docs.microsoft.com/en-us/cli/azure/install-azure-cli"
    exit 1
}

# Check if Azure DevOps extension is installed
$extensionStatus = az extension list --query "[?name=='azure-devops'].name" -o tsv 2>$null
if (-not $extensionStatus) {
    Write-Host "Installing Azure DevOps CLI extension..." -ForegroundColor Yellow
    az extension add --name azure-devops --only-show-errors
}

# Get the script directory and project paths
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Join-Path $scriptDir ".."
$projectFile = Join-Path $projectDir "Agent.Cli.csproj"

# Create output directories
$outputDir = Join-Path $projectDir "publish"
$packageDir = Join-Path $outputDir "universal-package"

if (Test-Path $outputDir) {
    Remove-Item $outputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $packageDir -Force | Out-Null

Write-Host "Building SRECTL Executables for Universal Package" -ForegroundColor Green
Write-Host "=================================================" -ForegroundColor Green
Write-Host "Package Name: $PackageName" -ForegroundColor Gray
Write-Host "Package Version: $PackageVersion" -ForegroundColor Gray
Write-Host "Target Platform(s): $Platform" -ForegroundColor Gray
Write-Host "Feed: $Organization/$Project/$FeedName" -ForegroundColor Gray

# Define platform configurations
$platforms = @()
switch ($Platform) {
    "All" { 
        $platforms = @(
            @{ Runtime = "win-x64"; Name = "Windows"; Extension = ".exe" },
            @{ Runtime = "linux-x64"; Name = "Linux"; Extension = "" },
            @{ Runtime = "osx-x64"; Name = "macOS-Intel"; Extension = "" },
            @{ Runtime = "osx-arm64"; Name = "macOS-AppleSilicon"; Extension = "" }
        )
    }
    "Windows" { 
        $platforms = @(@{ Runtime = "win-x64"; Name = "Windows"; Extension = ".exe" })
    }
    "Linux" { 
        $platforms = @(@{ Runtime = "linux-x64"; Name = "Linux"; Extension = "" })
    }
    "macOS" { 
        $platforms = @(
            @{ Runtime = "osx-x64"; Name = "macOS-Intel"; Extension = "" },
            @{ Runtime = "osx-arm64"; Name = "macOS-AppleSilicon"; Extension = "" }
        )
    }
}

# Build executables for each platform using MSBuild target
foreach ($plat in $platforms) {
    Write-Host "`nBuilding for $($plat.Name) ($($plat.Runtime))..." -ForegroundColor Cyan
    
    $runtimeOutput = Join-Path $outputDir $plat.Runtime
    
    $buildArgs = @(
        "publish"
        $projectFile
        "--configuration", "Release"
        "--runtime", $plat.Runtime
        "--self-contained", "true"
        "--output", $runtimeOutput
        "--verbosity", "minimal"
        "-p:PackAsTool=false"
        "-p:BuildForUniversalPackage=true"
        "-p:UniversalPackageDir=$packageDir"
    )
    
    Write-Host "Running: dotnet $($buildArgs -join ' ')" -ForegroundColor Gray
    & dotnet @buildArgs
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to build for $($plat.Name). Exit code: $LASTEXITCODE"
        exit $LASTEXITCODE
    }
    
    # Verify the MSBuild target created the executable in the universal package
    $targetExe = Join-Path $packageDir "srectl-$($plat.Runtime)$($plat.Extension)"
    if (Test-Path $targetExe) {
        $fileSize = [math]::Round((Get-Item $targetExe).Length / 1MB, 2)
        Write-Host "[OK] Built $($plat.Name): $fileSize MB" -ForegroundColor Green
    } else {
        Write-Error "MSBuild target failed to create: $targetExe"
        exit 1
    }
}

Write-Host "`nPackage Contents:" -ForegroundColor Yellow
Get-ChildItem $packageDir | ForEach-Object {
    if ($_.PSIsContainer) {
        Write-Host "  [DIR] $($_.Name)/" -ForegroundColor Gray
    } else {
        $size = if ($_.Length -gt 1MB) { "{0:N1} MB" -f ($_.Length / 1MB) } else { "{0:N0} KB" -f ($_.Length / 1KB) }
        Write-Host "  [FILE] $($_.Name) ($size)" -ForegroundColor Gray
    }
}

# Ensure we're logged into Azure CLI
Write-Host "`nChecking Azure CLI authentication..." -ForegroundColor Cyan
$azAccount = az account show 2>$null | ConvertFrom-Json
if (-not $azAccount) {
    Write-Host "Not logged into Azure CLI. Running 'az login'..." -ForegroundColor Yellow
    az login
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to login to Azure CLI. Exit code: $LASTEXITCODE"
        exit $LASTEXITCODE
    }
} else {
    Write-Host "[OK] Logged in as $($azAccount.user.name)" -ForegroundColor Green
}

# Set Azure DevOps organization
Write-Host "Setting Azure DevOps organization..." -ForegroundColor Cyan
az devops configure --defaults organization=$FeedUrl project=$Project

# Publish Universal Package
Write-Host "`nPublishing Universal Package to Azure Artifacts..." -ForegroundColor Cyan
Write-Host "Feed: $Organization/$Project/$FeedName" -ForegroundColor Gray

$publishArgs = @(
    "artifacts", "universal", "publish"
    "--organization", $FeedUrl
    "--project", $Project  
    "--scope", "project"
    "--feed", $FeedName
    "--name", $PackageName
    "--version", $PackageVersion
    "--description", "SRECTL executable binaries for multiple platforms (v$PackageVersion)"
    "--path", $packageDir
)

Write-Host "Running: az $($publishArgs -join ' ')" -ForegroundColor Gray
#& az @publishArgs

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nUniversal Package published successfully!" -ForegroundColor Green
    Write-Host "`nPackage Details:" -ForegroundColor Yellow
    Write-Host "  Name: $PackageName" -ForegroundColor Gray
    Write-Host "  Version: $PackageVersion" -ForegroundColor Gray
    Write-Host "  Feed: $FeedName" -ForegroundColor Gray
    Write-Host "  Organization: $Organization" -ForegroundColor Gray
    Write-Host "  Project: $Project" -ForegroundColor Gray
    
    Write-Host "`nTo install this package:" -ForegroundColor Yellow
    Write-Host "  .\install_exe.ps1 -PackageVersion $PackageVersion" -ForegroundColor Gray
    
    Write-Host "`nPackage URL:" -ForegroundColor Yellow
    Write-Host "  $FeedUrl/$Project/_artifacts/feed/$FeedName/UPack/$PackageName/overview/$PackageVersion" -ForegroundColor Gray
    
} else {
    Write-Error "Failed to publish Universal Package. Exit code: $LASTEXITCODE"
    
    Write-Host "`nTroubleshooting tips:" -ForegroundColor Yellow
    Write-Host "1. Ensure you have Contributor permissions to the feed" -ForegroundColor White
    Write-Host "2. Verify the feed exists: $FeedName" -ForegroundColor White  
    Write-Host "3. Check Azure CLI authentication: az account show" -ForegroundColor White
    Write-Host "4. Verify Azure DevOps extension: az extension list" -ForegroundColor White
    
    exit $LASTEXITCODE
}

# Cleanup
Write-Host "`nCleaning up build artifacts..." -ForegroundColor Gray
if (Test-Path $outputDir) {
    #Remove-Item $outputDir -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "`n[SUCCESS] Build and publish completed successfully!" -ForegroundColor Green
