<#
.SYNOPSIS
Installs or upgrades SRECTL .NET Global Tool from NuGet feed.

.DESCRIPTION
This script installs or upgrades the SRECTL .NET Global Tool from the specified NuGet feed.
It automatically uninstalls any existing version before installing the new one.

.PARAMETER FeedUrl
The NuGet feed URL. Default targets the SREAgentCli NuGet feed.

.PARAMETER Version
The version to install. Use 'latest' to automatically install the most recent version from the feed, or specify an exact version like '1.0.3'. Default is 'latest'.

.PARAMETER Upgrade
Upgrade existing installation to the latest version or specified version.

.EXAMPLE
.\install-nupkg.ps1

.EXAMPLE
.\install-nupkg.ps1 -Version "1.0.3"

.EXAMPLE
.\install-nupkg.ps1 -Upgrade

.EXAMPLE
.\install-nupkg.ps1 -FeedUrl "https://api.nuget.org/v3/index.json" -Version "1.0.2"
#>

param(
    [string]$FeedUrl = "https://pkgs.dev.azure.com/msazure/One/_packaging/SREAgentCli/nuget/v3/index.json",
    [string]$Version = "latest",
    [switch]$Upgrade
)

$ErrorActionPreference = "Stop"

# Handle upgrade option
if ($Upgrade) {
    Write-Host "SRECTL .NET Global Tool Upgrade" -ForegroundColor Green
    Write-Host "===============================" -ForegroundColor Green
    
    # If Version is not explicitly set, use "latest" for upgrades
    if ($Version -eq "latest") {
        Write-Host "Upgrading to latest version..." -ForegroundColor Cyan
    } else {
        Write-Host "Upgrading to version: $Version" -ForegroundColor Cyan
    }
} else {
    Write-Host "SRECTL .NET Global Tool Installer" -ForegroundColor Green
    Write-Host "=================================" -ForegroundColor Green
}

Write-Host "Feed URL: $FeedUrl" -ForegroundColor Gray
Write-Host "Version: $Version" -ForegroundColor Gray

# Check if .NET is installed
try {
    $dotnetVersion = dotnet --version 2>$null
    if ($dotnetVersion) {
        Write-Host "[OK] .NET SDK found: $dotnetVersion" -ForegroundColor Green
    } else {
        Write-Error ".NET SDK is not installed. Please install it from https://dotnet.microsoft.com/download"
        exit 1
    }
} catch {
    Write-Error ".NET SDK is not installed. Please install it from https://dotnet.microsoft.com/download"
    exit 1
}

Write-Host "`nUninstalling existing SRECTL installation..." -ForegroundColor Cyan

# Uninstall existing version (suppress errors if not installed)
dotnet tool uninstall sreagent.cli --global 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Host "[OK] Existing installation removed" -ForegroundColor Green
} else {
    Write-Host "[INFO] No existing installation found" -ForegroundColor Gray
}

Write-Host "`nInstalling SRECTL..." -ForegroundColor Cyan

# Build install command
$installArgs = @(
    "tool", "install", "sreagent.cli", "--global", 
    "--add-source", $FeedUrl,
    "--verbosity", "normal"
)

# Add version if not "latest"
if ($Version -ne "latest") {
    $installArgs += "--version"
    $installArgs += $Version
}

# Install the tool
& dotnet @installArgs

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to install SRECTL. Exit code: $LASTEXITCODE"
    exit $LASTEXITCODE
}

Write-Host "[OK] SRECTL installed successfully!" -ForegroundColor Green

# Verify installation
Write-Host "`nVerifying installation..." -ForegroundColor Cyan
try {
    $version = srectl --version 2>$null
    if ($version) {
        Write-Host "[OK] SRECTL is working: $version" -ForegroundColor Green
    } else {
        Write-Warning "SRECTL was installed but --version failed"
    }
} catch {
    Write-Warning "Could not verify SRECTL installation: $($_.Exception.Message)"
}

Write-Host "`n[SUCCESS] SRECTL installation completed!" -ForegroundColor Green

Write-Host "`nNext Steps:" -ForegroundColor Yellow
Write-Host "  - Run: srectl --help" -ForegroundColor White
Write-Host "  - Run: srectl init --resource-url <your-server-url>" -ForegroundColor White
Write-Host "  - To uninstall: dotnet tool uninstall sreagent.cli --global" -ForegroundColor White
Write-Host "  - To upgrade: .\install-nupkg.ps1 -Upgrade" -ForegroundColor White
