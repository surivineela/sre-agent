<#
.SYNOPSIS
Uninstalls SRECTL from the system.

.DESCRIPTION
This script removes SRECTL executable, templates, and cleans up PATH configurations.

.PARAMETER Force
Skip confirmation prompts.

.EXAMPLE
.\Uninstall.ps1

.EXAMPLE
.\Uninstall.ps1 -Force
#>

param (
    [switch]$Force
)

$ErrorActionPreference = "Stop"

Write-Host "SRECTL Uninstallation" -ForegroundColor Yellow
Write-Host "=====================" -ForegroundColor Yellow

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
    $installDir = Join-Path $env:LOCALAPPDATA "SRECTL"
    $installExe = Join-Path $installDir "srectl.exe"
    Write-Host "Platform: Windows" -ForegroundColor Cyan
}
elseif ($platformLinux -or $platformMacOS) {
    $installDir = "/usr/local/lib/srectl"
    $installExe = "$installDir/srectl"
    $symlinkPath = "/usr/local/bin/srectl"
    $platformName = if ($platformLinux) { "Linux" } else { "macOS" }
    Write-Host "Platform: $platformName" -ForegroundColor Cyan
}
else {
    Write-Error "Unsupported platform"
    exit 1
}

# Check if installed
if (-not (Test-Path $installDir)) {
    Write-Host "SRECTL is not installed at: $installDir" -ForegroundColor Yellow
    exit 0
}

Write-Host "Installation Directory: $installDir" -ForegroundColor Cyan

# Confirm uninstallation
if (-not $Force) {
    $response = Read-Host "Are you sure you want to uninstall SRECTL? (Y/n)"
    if ($response -and $response.ToLower() -ne 'y' -and $response.ToLower() -ne 'yes') {
        Write-Host "Uninstallation cancelled." -ForegroundColor Yellow
        exit 0
    }
}

# Remove installation directory
Write-Host "`nRemoving SRECTL files..." -ForegroundColor Cyan
try {
    if ($platformWindows) {
        Remove-Item $installDir -Recurse -Force
        Write-Host "✅ Removed: $installDir" -ForegroundColor Green
    }
    else {
        sudo rm -rf $installDir
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Removed: $installDir" -ForegroundColor Green
        }
        else {
            Write-Error "Failed to remove installation directory"
        }
    }
}
catch {
    Write-Error "Failed to remove installation directory: $($_.Exception.Message)"
    exit 1
}

# Remove symlink on Unix-like systems
if (-not $platformWindows) {
    if (Test-Path $symlinkPath) {
        Write-Host "Removing symlink..." -ForegroundColor Cyan
        sudo rm -f $symlinkPath
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Removed symlink: $symlinkPath" -ForegroundColor Green
        }
    }
}

# Clean up PATH on Windows
if ($platformWindows) {
    Write-Host "Cleaning up PATH..." -ForegroundColor Cyan
    $currentPath = [Environment]::GetEnvironmentVariable("PATH", "User")
    if ($currentPath -like "*$installDir*") {
        $newPath = ($currentPath -split ';' | Where-Object { $_ -ne $installDir }) -join ';'
        [Environment]::SetEnvironmentVariable("PATH", $newPath, "User")
        Write-Host "✅ Removed from PATH" -ForegroundColor Green
        Write-Host "Note: Restart your terminal for PATH changes to take effect" -ForegroundColor Yellow
    }
}

Write-Host "`n[SUCCESS] SRECTL has been uninstalled successfully!" -ForegroundColor Green

if ($platformWindows) {
    Write-Host "`nNote: Please restart your terminal or PowerShell to complete the uninstallation." -ForegroundColor Yellow
}
