<#
.SYNOPSIS
Downloads and installs SRECTL executable from Azure DevOps Universal Package.

.DESCRIPTION
This script:
- Downloads SRECTL executable from an Azure Artifacts Universal Package
- Installs it to the user's local directory or system-wide
- Configures PATH environment variable
- Creates shortcuts and aliases
- Provides uninstall capability

.PARAMETER PackageVersion
The version of the Universal Package to download. Use 'latest' to use the most recent known stable version, or specify an exact version like '1.0.3'. Note: Azure Artifacts Universal Packages don't support automatic latest version resolution.

.PARAMETER FeedUrl
The base Azure DevOps organization URL. Default targets the SREAgentCli feed.

.PARAMETER FeedName
The name of the Azure Artifacts feed. Default is 'SREAgentCli'.

.PARAMETER PackageName
The name of the Universal Package. Default is 'srectl-executables'.

.PARAMETER Organization
The Azure DevOps organization name. Default is 'msazure'.

.PARAMETER Project
The Azure DevOps project name. Default is 'One'.

.PARAMETER InstallPath
Custom installation path. If not specified, uses user's local directory.

.PARAMETER UserInstall
Install for current user only (no admin rights required). This is the default.

.PARAMETER SystemInstall
Install system-wide (requires administrator privileges).

.PARAMETER Platform
The target platform. Options: 'Auto', 'Windows', 'Linux', 'macOS-Intel', 'macOS-AppleSilicon'. Default is 'Auto'.

.PARAMETER AddDesktopShortcut
Create a desktop shortcut (Windows only).

.PARAMETER Silent
Run installation silently without user prompts.

.PARAMETER Upgrade
Upgrade existing installation to the latest version or specified version.

.EXAMPLE
.\install_exe.ps1
Downloads and installs the latest known stable version of SRECTL.

.EXAMPLE
.\install_exe.ps1 -Upgrade
Upgrades existing installation to the latest known stable version.

.EXAMPLE
.\install_exe.ps1 -PackageVersion "1.0.3" -UserInstall
Installs a specific version for the current user only.

.EXAMPLE
.\install_exe.ps1 -SystemInstall -AddDesktopShortcut
Installs the latest known stable version system-wide with a desktop shortcut.

.EXAMPLE
.\install_exe.ps1 -Organization "myorg" -Project "myproject" -FeedName "myfeed"
Installs from a custom Azure DevOps feed.
#>

param (
    [string]$PackageVersion = "latest",
    [string]$FeedUrl = "https://dev.azure.com/msazure",
    [string]$FeedName = "SREAgentCli",
    [string]$PackageName = "srectl-executables", 
    [string]$Organization = "msazure",
    [string]$Project = "One",
    [string]$InstallPath = "",
    [switch]$UserInstall = $true,
    [switch]$SystemInstall,
    [ValidateSet("Auto", "Windows", "Linux", "macOS-Intel", "macOS-AppleSilicon")]
    [string]$Platform = "Auto",
    [switch]$AddDesktopShortcut,
    [switch]$Silent,
    [switch]$Upgrade
)

$ErrorActionPreference = "Stop"

# Override UserInstall if SystemInstall is specified
if ($SystemInstall) {
    $UserInstall = $false
}

# Handle upgrade option
if ($Upgrade) {
    Write-Host "SRECTL Upgrade Mode" -ForegroundColor Green
    Write-Host "==================" -ForegroundColor Green
    
    # If PackageVersion is not explicitly set, use "latest" for upgrades
    if ($PackageVersion -eq "latest" -or $PackageVersion -eq "1.0.0") {
        $PackageVersion = "latest"
        Write-Host "Upgrading to latest version..." -ForegroundColor Cyan
    } else {
        Write-Host "Upgrading to version: $PackageVersion" -ForegroundColor Cyan
    }
} else {
    Write-Host "SRECTL Universal Package Installer" -ForegroundColor Green
    Write-Host "==================================" -ForegroundColor Green
}

if (-not $Silent) {
    Write-Host "Organization: $Organization" -ForegroundColor Gray
    Write-Host "Project: $Project" -ForegroundColor Gray
    Write-Host "Feed: $FeedName" -ForegroundColor Gray
    Write-Host "Package: $PackageName" -ForegroundColor Gray
    Write-Host "Version: $PackageVersion" -ForegroundColor Gray
    Write-Host "Install Type: $(if ($UserInstall) { 'User' } else { 'System' })" -ForegroundColor Gray
}

# Check prerequisites
if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Error "Azure CLI is not installed. Please install it from https://docs.microsoft.com/en-us/cli/azure/install-azure-cli"
    exit 1
}

# Check if Azure DevOps extension is installed
$extensionStatus = az extension list --query "[?name=='azure-devops'].name" -o tsv 2>$null
if (-not $extensionStatus) {
    Write-Host "Installing Azure DevOps CLI extension..." -ForegroundColor Yellow
    az extension add --name azure-devops --only-show-errors
}

# Auto-detect platform if needed
if ($Platform -eq "Auto") {
    if ($IsWindows -or $env:OS -eq "Windows_NT") {
        $Platform = "Windows"
        $executableName = "srectl.exe"
        $platformRuntime = "win-x64"
    }
    elseif ($IsMacOS) {
        # Detect Apple Silicon vs Intel
        $architecture = uname -m 2>$null
        if ($architecture -eq "arm64") {
            $Platform = "macOS-AppleSilicon"  
            $platformRuntime = "osx-arm64"
        } else {
            $Platform = "macOS-Intel"
            $platformRuntime = "osx-x64"
        }
        $executableName = "srectl"
    }
    else {
        $Platform = "Linux"
        $executableName = "srectl"
        $platformRuntime = "linux-x64"
    }
} else {
    # Map platform to runtime and executable name
    switch ($Platform) {
        "Windows" { 
            $executableName = "srectl.exe"
            $platformRuntime = "win-x64"
        }
        "Linux" { 
            $executableName = "srectl"
            $platformRuntime = "linux-x64"
        }
        "macOS-Intel" { 
            $executableName = "srectl"
            $platformRuntime = "osx-x64"
        }
        "macOS-AppleSilicon" { 
            $executableName = "srectl"
            $platformRuntime = "osx-arm64"
        }
    }
}

Write-Host "Detected Platform: $Platform ($platformRuntime)" -ForegroundColor Cyan

# Determine installation path
if ([string]::IsNullOrEmpty($InstallPath)) {
    if ($UserInstall) {
        if ($Platform -eq "Windows") {
            $InstallPath = Join-Path $env:LOCALAPPDATA "SRECTL"
        } else {
            $InstallPath = Join-Path $env:HOME ".local/bin"
        }
    } else {
        if ($Platform -eq "Windows") {
            $InstallPath = "${env:ProgramFiles}\SRECTL"
        } else {
            $InstallPath = "/usr/local/bin"
        }
    }
}

Write-Host "Installation Path: $InstallPath" -ForegroundColor Cyan

# Check for admin rights if system install
if (-not $UserInstall -and $Platform -eq "Windows") {
    $currentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentUser)
    $isAdmin = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    
    if (-not $isAdmin) {
        Write-Error "System-wide installation requires administrator privileges. Run PowerShell as Administrator or use -UserInstall."
        exit 1
    }
}

# Confirm installation unless silent
if (-not $Silent) {
    Write-Host "`nThis will install SRECTL to: $InstallPath" -ForegroundColor Yellow
    Write-Host "Continue? (Y/n): " -NoNewline -ForegroundColor Yellow
    $response = Read-Host
    if ($response -and $response.ToLower() -ne 'y' -and $response.ToLower() -ne 'yes') {
        Write-Host "Installation cancelled." -ForegroundColor Yellow
        exit 0
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
az devops configure --defaults organization=$FeedUrl project=$Project

# Create temporary download directory
$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) "srectl-install-$(Get-Random)"
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

try {
    # Handle 'latest' version
    if ($PackageVersion -eq "latest") {
        Write-Host "Attempting to resolve latest package version..." -ForegroundColor Cyan
        
        # Azure Artifacts Universal Packages don't have a direct way to query for latest version
        # We'll try a few common approaches and fall back to a known version
        $fallbackVersion = "1.0.3"
        
        Write-Host "Note: Azure Artifacts Universal Packages don't support automatic latest version resolution." -ForegroundColor Yellow
        Write-Host "Using known stable version: $fallbackVersion" -ForegroundColor Cyan
        Write-Host "To use a specific version, run: .\install_exe.ps1 -PackageVersion 'x.y.z'" -ForegroundColor Gray
        Write-Host "To see available versions, visit: https://dev.azure.com/msazure/One/_artifacts/feed/SREAgentCli/UPack/srectl-executables/overview" -ForegroundColor Gray
        
        if (-not $Silent) {
            $confirmation = Read-Host "Continue with version $fallbackVersion? (Y/n)"
            if ($confirmation -and $confirmation.ToLower() -ne 'y' -and $confirmation.ToLower() -ne 'yes') {
                Write-Host "Installation cancelled." -ForegroundColor Yellow
                exit 0
            }
        }
        
        $PackageVersion = $fallbackVersion
    }

    # Download Universal Package
    Write-Host "`nDownloading SRECTL Universal Package..." -ForegroundColor Cyan
    Write-Host "Package: $PackageName v$PackageVersion" -ForegroundColor Gray
    
    $downloadArgs = @(
        "artifacts", "universal", "download"
        "--organization", $FeedUrl
        "--project", $Project
        "--scope", "project" 
        "--feed", $FeedName
        "--name", $PackageName
        "--version", $PackageVersion
        "--path", $tempDir
    )
    
    & az @downloadArgs
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to download Universal Package. Exit code: $LASTEXITCODE"
        exit $LASTEXITCODE
    }
    
    Write-Host "[OK] Package downloaded successfully" -ForegroundColor Green

    # Find the correct executable for the platform
    $sourceExecutable = Join-Path $tempDir "srectl-$platformRuntime$(if ($Platform -eq 'Windows') { '.exe' } else { '' })"
    
    if (-not (Test-Path $sourceExecutable)) {
        Write-Error "Platform-specific executable not found: $sourceExecutable"
        Write-Host "`nAvailable files in package:" -ForegroundColor Yellow
        Get-ChildItem $tempDir | ForEach-Object { Write-Host "  $($_.Name)" -ForegroundColor Gray }
        exit 1
    }
    
    $exeSize = [math]::Round((Get-Item $sourceExecutable).Length / 1MB, 2)
    Write-Host "[OK] Found executable: srectl-$platformRuntime ($exeSize MB)" -ForegroundColor Green

    # Create installation directory
    Write-Host "`nInstalling SRECTL..." -ForegroundColor Cyan
    if (-not (Test-Path $InstallPath)) {
        New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
        Write-Host "[OK] Created installation directory: $InstallPath" -ForegroundColor Green
    }

    # Copy executable
    $targetExecutable = Join-Path $InstallPath $executableName
    Copy-Item $sourceExecutable $targetExecutable -Force
    Write-Host "[OK] Installed executable: $targetExecutable" -ForegroundColor Green

    # Make executable on Unix systems
    if ($Platform -ne "Windows") {
        chmod +x $targetExecutable 2>$null
        Write-Host "[OK] Made executable" -ForegroundColor Green
    }

    # Configure PATH
    Write-Host "`nConfiguring PATH..." -ForegroundColor Cyan
    $pathUpdated = $false
    
    if ($Platform -eq "Windows") {
        $envTarget = if ($UserInstall) { "User" } else { "Machine" }
        $currentPath = [Environment]::GetEnvironmentVariable("PATH", $envTarget)
        
        if ($currentPath -notlike "*$InstallPath*") {
            $newPath = if ($currentPath.EndsWith(";")) { 
                $currentPath + $InstallPath 
            } else { 
                $currentPath + ";" + $InstallPath 
            }
            [Environment]::SetEnvironmentVariable("PATH", $newPath, $envTarget)
            $pathUpdated = $true
            Write-Host "[OK] Added to PATH ($envTarget level)" -ForegroundColor Green
        } else {
            Write-Host "[OK] Already in PATH" -ForegroundColor Green
        }
    } else {
        # Unix systems - add to shell profile
        $shellProfile = if (Test-Path "$env:HOME/.zshrc") { "$env:HOME/.zshrc" } else { "$env:HOME/.bashrc" }
        $pathExport = 'export PATH="' + $InstallPath + ':$PATH"'
        
        if ((Get-Content $shellProfile -ErrorAction SilentlyContinue) -notcontains $pathExport) {
            Add-Content $shellProfile "`n# Added by SRECTL installer"
            Add-Content $shellProfile $pathExport
            $pathUpdated = $true
            Write-Host "[OK] Added to PATH in $shellProfile" -ForegroundColor Green
        } else {
            Write-Host "[OK] Already in PATH" -ForegroundColor Green
        }
    }

    # Create Start Menu shortcut (Windows)
    if ($Platform -eq "Windows") {
        Write-Host "`nCreating shortcuts..." -ForegroundColor Cyan
        
        $startMenuPath = if ($UserInstall) {
            Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
        } else {
            "${env:ProgramData}\Microsoft\Windows\Start Menu\Programs"
        }
        
        $shell = New-Object -ComObject WScript.Shell
        $shortcut = $shell.CreateShortcut((Join-Path $startMenuPath "SRECTL.lnk"))
        $shortcut.TargetPath = $targetExecutable
        $shortcut.WorkingDirectory = Split-Path $targetExecutable
        $shortcut.Description = "SRE Agent CLI Tool"
        $shortcut.Save()
        Write-Host "[OK] Created Start Menu shortcut" -ForegroundColor Green
        
        # Create desktop shortcut if requested
        if ($AddDesktopShortcut) {
            $desktopPath = [Environment]::GetFolderPath("Desktop")
            $desktopShortcut = $shell.CreateShortcut((Join-Path $desktopPath "SRECTL.lnk"))
            $desktopShortcut.TargetPath = $targetExecutable
            $desktopShortcut.WorkingDirectory = Split-Path $targetExecutable
            $desktopShortcut.Description = "SRE Agent CLI Tool"
            $desktopShortcut.Save()
            Write-Host "[OK] Created desktop shortcut" -ForegroundColor Green
        }
    }

    # Create uninstaller
    Write-Host "`nCreating uninstaller..." -ForegroundColor Cyan
    $uninstallScript = if ($Platform -eq "Windows") { 
        Join-Path $InstallPath "uninstall.ps1" 
    } else { 
        Join-Path $InstallPath "uninstall.sh" 
    }
    
    $uninstallContent = if ($Platform -eq "Windows") {
        $envTargetValue = if ($UserInstall) { 'User' } else { 'Machine' }
        $desktopShortcutRemoval = if ($AddDesktopShortcut) { 
            "Remove-Item `"$([Environment]::GetFolderPath('Desktop'))\SRECTL.lnk`" -Force -ErrorAction SilentlyContinue" 
        } else { "" }
        
        @"
# SRECTL Uninstaller
Write-Host "Uninstalling SRECTL..." -ForegroundColor Yellow

# Remove executable
Remove-Item "$targetExecutable" -Force -ErrorAction SilentlyContinue

# Remove from PATH
`$envTarget = "$envTargetValue"
`$currentPath = [Environment]::GetEnvironmentVariable("PATH", `$envTarget)
`$newPath = `$currentPath -replace [regex]::Escape("$InstallPath") + ";?", ""
`$newPath = `$newPath -replace ";+", ";" -replace "^;|;`$", ""
[Environment]::SetEnvironmentVariable("PATH", `$newPath, `$envTarget)

# Remove shortcuts
Remove-Item "$startMenuPath\SRECTL.lnk" -Force -ErrorAction SilentlyContinue
$desktopShortcutRemoval

# Remove installation directory
Remove-Item "$InstallPath" -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "SRECTL has been uninstalled." -ForegroundColor Green
"@
    } else {
        $uninstallContent = @'
#!/bin/bash
# SRECTL Uninstaller
echo "Uninstalling SRECTL..."

# Remove executable
rm -f "{0}"

# Remove from shell profile
sed -i '/# Added by SRECTL installer/d' "{1}" 2>/dev/null
sed -i '\|export PATH="{2}:$PATH"|d' "{1}" 2>/dev/null

# Remove installation directory if it's our custom directory
if [[ "{2}" == *".local/bin"* ]] || [[ "{2}" == *"SRECTL"* ]]; then
    rm -rf "{2}"
fi

echo "SRECTL has been uninstalled."
'@ -f $targetExecutable, $shellProfile, $InstallPath
    }
    
    $uninstallContent | Out-File $uninstallScript -Encoding UTF8
    if ($Platform -ne "Windows") {
        chmod +x $uninstallScript 2>$null
    }
    Write-Host "[OK] Created uninstaller: $uninstallScript" -ForegroundColor Green

    # Verify installation
    Write-Host "`nVerifying installation..." -ForegroundColor Cyan
    
    # Test executable directly
    try {
        $version = & $targetExecutable --version 2>$null
        if ($version) {
            Write-Host "[OK] SRECTL is working: $version" -ForegroundColor Green
        } else {
            Write-Warning "SRECTL executable exists but --version failed"
        }
    } catch {
        Write-Warning "Could not verify SRECTL installation: $($_.Exception.Message)"
    }

    # Installation complete
    Write-Host "`n[SUCCESS] SRECTL installation completed successfully!" -ForegroundColor Green
    
    Write-Host "`nInstallation Summary:" -ForegroundColor Yellow
    Write-Host "  Executable: $targetExecutable" -ForegroundColor Gray
    Write-Host "  Version: $PackageVersion" -ForegroundColor Gray
    Write-Host "  Platform: $Platform" -ForegroundColor Gray
    Write-Host "  Install Type: $(if ($UserInstall) { 'User' } else { 'System' })" -ForegroundColor Gray
    
    if ($pathUpdated) {
        Write-Host "`n[WARNING] PATH has been updated. You may need to:" -ForegroundColor Yellow
        if ($Platform -eq "Windows") {
            Write-Host "  - Restart your terminal/PowerShell session" -ForegroundColor White
        } else {
            Write-Host "  - Run: source $shellProfile" -ForegroundColor White
            Write-Host "  - Or restart your terminal session" -ForegroundColor White
        }
    }
    
    Write-Host "`nNext Steps:" -ForegroundColor Yellow
    Write-Host "  - Run: srectl --help" -ForegroundColor White
    Write-Host "  - Run: srectl init --resource-url <your-server-url>" -ForegroundColor White
    Write-Host "  - To upgrade: .\install_exe.ps1 -Upgrade" -ForegroundColor White
    Write-Host "  - To uninstall: $(Split-Path $uninstallScript -Leaf)" -ForegroundColor White

} finally {
    # Cleanup
    if (Test-Path $tempDir) {
        Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
