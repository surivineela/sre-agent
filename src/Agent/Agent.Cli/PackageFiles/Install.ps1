<#
.SYNOPSIS
Installs SRECTL from the Universal Package.

.DESCRIPTION
This script detects the current platform and installs the appropriate SRECTL executable
and templates to the standard installation location.

.PARAMETER Force
Force overwrite existing installation without prompting.

.EXAMPLE
.\Install.ps1

.EXAMPLE
.\Install.ps1 -Force
#>

param (
    [switch]$Force
)

$ErrorActionPreference = "Stop"

Write-Host "SRECTL Installation" -ForegroundColor Green
Write-Host "===================" -ForegroundColor Green

# Detect platform
if ($PSVersionTable.PSVersion.Major -ge 6) {
    $platformWindows = $IsWindows
    $platformLinux = $IsLinux
    $platformMacOS = $IsMacOS
} else {
    # PowerShell 5.1 on Windows
    $platformWindows = $true
    $platformLinux = $false
    $platformMacOS = $false
}

if ($platformWindows) {
    $platform = "win-x64"
    $executableName = "srectl-win-x64.exe"
    $installDir = Join-Path $env:LOCALAPPDATA "SRECTL"
    $installExe = Join-Path $installDir "srectl.exe"
    Write-Host "Platform: Windows" -ForegroundColor Cyan
}
elseif ($platformLinux) {
    $platform = "linux-x64"
    $executableName = "srectl-linux-x64"
    $installDir = "/usr/local/lib/srectl"
    $installExe = "$installDir/srectl"
    $symlinkPath = "/usr/local/bin/srectl"
    Write-Host "Platform: Linux" -ForegroundColor Cyan
}
elseif ($platformMacOS) {
    # Detect architecture
    $arch = uname -m 2>$null
    if ($arch -eq "arm64") {
        $platform = "osx-arm64"
        $executableName = "srectl-osx-arm64"
    } else {
        $platform = "osx-x64"
        $executableName = "srectl-osx-x64"
    }
    $installDir = "/usr/local/lib/srectl"
    $installExe = "$installDir/srectl"
    $symlinkPath = "/usr/local/bin/srectl"
    Write-Host "Platform: macOS ($arch)" -ForegroundColor Cyan
}
else {
    Write-Error "Unsupported platform"
    exit 1
}

Write-Host "Installation Directory: $installDir" -ForegroundColor Cyan

# Get script directory (where the package was extracted)
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceExe = Join-Path $scriptDir $executableName
$sourceTemplates = Join-Path $scriptDir "Templates"

# Verify source files exist
if (-not (Test-Path $sourceExe)) {
    Write-Error "Executable not found: $sourceExe"
    Write-Host "Make sure you've extracted the package contents and are running this script from the extracted directory." -ForegroundColor Yellow
    exit 1
}

# Check for existing installation
if (Test-Path $installExe) {
    if (-not $Force) {
        Write-Host "Existing installation found: $installExe" -ForegroundColor Yellow
        $response = Read-Host "Overwrite existing installation? (Y/n)"
        if ($response -and $response.ToLower() -ne 'y' -and $response.ToLower() -ne 'yes') {
            Write-Host "Installation cancelled." -ForegroundColor Yellow
            exit 0
        }
    }
    
    Write-Host "Removing existing installation..." -ForegroundColor Cyan
    if ($platformWindows) {
        Remove-Item $installDir -Recurse -Force -ErrorAction SilentlyContinue
    }
    else {
        sudo rm -rf $installDir 2>$null
        if (Test-Path $symlinkPath) {
            sudo rm -f $symlinkPath 2>$null
        }
    }
}

# Create installation directory
Write-Host "Creating installation directory..." -ForegroundColor Cyan
if ($platformWindows) {
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
}
else {
    sudo mkdir -p $installDir
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to create installation directory. Make sure you have sudo privileges."
        exit 1
    }
}

# Copy executable
Write-Host "Installing executable..." -ForegroundColor Cyan
if ($platformWindows) {
    Copy-Item $sourceExe $installExe -Force
}
else {
    sudo cp $sourceExe $installExe
    sudo chmod +x $installExe
}

# Copy Templates folder if it exists
if (Test-Path $sourceTemplates) {
    Write-Host "Installing templates..." -ForegroundColor Cyan
    $installTemplates = Join-Path $installDir "Templates"
    
    if ($platformWindows) {
        if (Test-Path $installTemplates) {
            Remove-Item $installTemplates -Recurse -Force
        }
        Copy-Item $sourceTemplates $installTemplates -Recurse -Force
    }
    else {
        sudo rm -rf $installTemplates 2>$null
        sudo cp -r $sourceTemplates $installTemplates
    }
}

# Create symlink on Unix-like systems
if (-not $platformWindows) {
    Write-Host "Creating symlink..." -ForegroundColor Cyan
    sudo ln -sf $installExe $symlinkPath
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Failed to create symlink. You may need to add $installDir to your PATH manually."
    }
}

# Update PATH on Windows
if ($platformWindows) {
    Write-Host "Updating PATH..." -ForegroundColor Cyan
    $currentPath = [Environment]::GetEnvironmentVariable("PATH", "User")
    if ($currentPath -notlike "*$installDir*") {
        $newPath = if ($currentPath) { "$currentPath;$installDir" } else { $installDir }
        [Environment]::SetEnvironmentVariable("PATH", $newPath, "User")
        Write-Host "✅ PATH updated for current user" -ForegroundColor Green
        Write-Host "Note: Restart your terminal for PATH changes to take effect" -ForegroundColor Yellow
    }
    else {
        Write-Host "✅ PATH already contains installation directory" -ForegroundColor Green
    }
}

# Verify installation
Write-Host "`nVerifying installation..." -ForegroundColor Cyan
try {
    if ($platformWindows) {
        $null = & $installExe --version 2>&1
    }
    else {
        $null = & $symlinkPath --version 2>&1
    }
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Installation successful!" -ForegroundColor Green
    }
    else {
        Write-Warning "Installation completed but version check failed"
    }
}
catch {
    Write-Warning "Installation completed but verification failed: $($_.Exception.Message)"
}

# Display summary
Write-Host "`n[SUCCESS] SRECTL installation completed!" -ForegroundColor Green
Write-Host "`nInstallation Details:" -ForegroundColor Yellow
Write-Host "  - Executable: $installExe" -ForegroundColor White
Write-Host "  - Templates: $(Join-Path $installDir 'Templates')" -ForegroundColor White
if (-not $platformWindows) {
    Write-Host "  - Symlink: $symlinkPath" -ForegroundColor White
}

Write-Host "`nNext Steps:" -ForegroundColor Yellow
if ($platformWindows) {
    Write-Host "  1. Restart your terminal or PowerShell" -ForegroundColor White
    Write-Host "  2. Run: srectl --help" -ForegroundColor White
}
else {
    Write-Host "  1. Run: srectl --help" -ForegroundColor White
}
Write-Host "  3. Initialize: srectl init --resource-url <your-server-url>" -ForegroundColor White

Write-Host "`nTo uninstall:" -ForegroundColor Yellow
Write-Host "  Run: .\Uninstall.ps1" -ForegroundColor White
