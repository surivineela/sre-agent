#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Generates ARM templates from Bicep files for EV2 deployment.

.DESCRIPTION
    This script converts Bicep templates to ARM JSON templates using Azure Bicep CLI.
    It processes the main template, parameter file, and all module files.

.PARAMETER BicepTemplatesPath
    Path to the BicepTemplates folder containing .bicep files

.PARAMETER ArmTemplatesPath
    Path where the generated ARM templates (.json files) should be saved

.EXAMPLE
    .\generate-arm-templates.ps1 -BicepTemplatesPath ".\BicepTemplates" -ArmTemplatesPath ".\ArmTemplates"
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$BicepTemplatesPath,
    
    [Parameter(Mandatory=$true)]
    [string]$ArmTemplatesPath
)

$ErrorActionPreference = "Stop"

# Verify az CLI is available
if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Error "Azure CLI (az) is not found. Please install Azure CLI: https://aka.ms/installazurecli"
    exit 1
}

# Resolve to absolute paths
if (-not [System.IO.Path]::IsPathRooted($BicepTemplatesPath)) {
    $BicepTemplatesPath = Join-Path $PWD $BicepTemplatesPath
}
if (-not [System.IO.Path]::IsPathRooted($ArmTemplatesPath)) {
    $ArmTemplatesPath = Join-Path $PWD $ArmTemplatesPath
}

$BicepTemplatesPath = [System.IO.Path]::GetFullPath($BicepTemplatesPath)
$ArmTemplatesPath = [System.IO.Path]::GetFullPath($ArmTemplatesPath)

if (-not (Test-Path $BicepTemplatesPath)) {
    Write-Error "BicepTemplates path does not exist: $BicepTemplatesPath"
    exit 1
}

Write-Host "Generating ARM templates from Bicep files..." -ForegroundColor Cyan
Write-Host "  Source: $BicepTemplatesPath" -ForegroundColor Gray
Write-Host "  Output: $ArmTemplatesPath" -ForegroundColor Gray
Write-Host ""

# Create output directories
New-Item -ItemType Directory -Path $ArmTemplatesPath -Force | Out-Null
New-Item -ItemType Directory -Path "$ArmTemplatesPath\modules" -Force | Out-Null

$buildSuccess = $true
$allWarnings = @()

# Build main Bicep file
$mainBicep = Join-Path $BicepTemplatesPath "sreagentContainerAppsExtension.bicep"
if (Test-Path $mainBicep) {
    Write-Host "Building sreagentContainerAppsExtension.bicep..." -ForegroundColor Yellow
    try {
        $output = az bicep build --file $mainBicep --outdir $ArmTemplatesPath 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  ✓ Generated sreagentContainerAppsExtension.json" -ForegroundColor Green
        } else {
            Write-Warning "  Failed to build sreagentContainerAppsExtension.bicep"
            $buildSuccess = $false
        }
        
        # Display any warnings or errors from bicep
        if ($output) {
            $output | ForEach-Object {
                if ($_ -match "Warning|Error") {
                    Write-Host "  $_" -ForegroundColor Yellow
                    $allWarnings += $_
                }
            }
        }
    } catch {
        Write-Warning "  Error: $_"
        $buildSuccess = $false
    }
}

# Build parameter file
$paramFile = Join-Path $BicepTemplatesPath "sreagentContainerAppsExtension.bicepparam"
if (Test-Path $paramFile) {
    Write-Host "Building sreagentContainerAppsExtension.bicepparam..." -ForegroundColor Yellow
    try {
        $paramOutput = Join-Path $ArmTemplatesPath "sreagentContainerAppsExtension.parameters.json"
        $output = az bicep build-params --file $paramFile --outfile $paramOutput 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  ✓ Generated sreagentContainerAppsExtension.parameters.json" -ForegroundColor Green
        } else {
            Write-Warning "  Failed to build parameter file"
            $buildSuccess = $false
        }
        
        if ($output) {
            $output | ForEach-Object {
                if ($_ -match "Warning|Error") {
                    Write-Host "  $_" -ForegroundColor Yellow
                    $allWarnings += $_
                }
            }
        }
    } catch {
        Write-Warning "  Error: $_"
        $buildSuccess = $false
    }
}

# Build module Bicep files
$modulesPath = Join-Path $BicepTemplatesPath "modules"
if (Test-Path $modulesPath) {
    $moduleFiles = Get-ChildItem -Path $modulesPath -Filter "*.bicep"
    foreach ($moduleFile in $moduleFiles) {
        Write-Host "Building $($moduleFile.Name)..." -ForegroundColor Yellow
        try {
            $output = az bicep build --file $moduleFile.FullName --outdir "$ArmTemplatesPath\modules" 2>&1
            if ($LASTEXITCODE -eq 0) {
                $jsonName = [System.IO.Path]::ChangeExtension($moduleFile.Name, ".json")
                Write-Host "  ✓ Generated $jsonName" -ForegroundColor Green
            } else {
                Write-Warning "  Failed to build $($moduleFile.Name)"
                $buildSuccess = $false
            }
            
            if ($output) {
                $output | ForEach-Object {
                    if ($_ -match "Warning|Error") {
                        Write-Host "  $_" -ForegroundColor Yellow
                        $allWarnings += $_
                    }
                }
            }
        } catch {
            Write-Warning "  Error: $_"
            $buildSuccess = $false
        }
    }
}

Write-Host ""
if ($buildSuccess) {
    Write-Host "ARM templates generated successfully!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "ARM template generation completed with warnings" -ForegroundColor Yellow
    exit 0
}
