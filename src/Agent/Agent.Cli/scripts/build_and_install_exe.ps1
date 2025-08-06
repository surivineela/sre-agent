<#
.SYNOPSIS
Builds SRECTL executable locally and installs it without publishing.

.DESCRIPTION
This script:
- Builds a self-contained executable for the current platform
- Installs it to the user's local directory or system-wide
- Configures PATH environment variable
- Creates shortcuts and aliases
- Provides clean local installation without external dependencies

.PARAMETER Platform
The target platform to build. Options: 'Auto', 'Windows', 'Linux', 'macOS-Intel', 'macOS-AppleSilicon'. Default is 'Auto'.

.PARAMETER InstallPath
Custom installation path. If not specified, uses user's local directory.

.PARAMETER UserInstall
Install for current user only (no admin rights required). This is the default.

.PARAMETER SystemInstall
Install system-wide (requires administrator privileges).

.PARAMETER AddDesktopShortcut
Create a desktop shortcut (Windows only).

.PARAMETER Force
Force overwrite existing installation without prompting.

.PARAMETER NoPathUpdate
Skip updating the PATH environment variable.

.EXAMPLE
.\build_and_install_exe.ps1

.EXAMPLE
.\build_and_install_exe.ps1 -SystemInstall

.EXAMPLE
.\build_and_install_exe.ps1 -Platform Windows -AddDesktopShortcut -Force

.EXAMPLE
.\build_and_install_exe.ps1 -InstallPath "C:\Tools\SRECTL" -NoPathUpdate
#>

param (
    [ValidateSet("Auto", "Windows", "Linux", "macOS-Intel", "macOS-AppleSilicon")]
    [string]$Platform = "Auto",
    [string]$InstallPath = "",
    [switch]$UserInstall = $true,
    [switch]$SystemInstall,
    [switch]$AddDesktopShortcut,
    [switch]$Force,
    [switch]$NoPathUpdate
)

$ErrorActionPreference = "Stop"

# Override UserInstall if SystemInstall is specified
if ($SystemInstall) {
    $UserInstall = $false
}

# Ensure required tools are available
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error "The .NET SDK is not installed or not available in PATH."
    exit 1
}

# Get the script directory and project paths
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Join-Path $scriptDir ".."
$projectFile = Join-Path $projectDir "Agent.Cli.csproj"

if (-not (Test-Path $projectFile)) {
    Write-Error "Project file not found: $projectFile"
    exit 1
}

Write-Host "SRECTL Local Build and Install" -ForegroundColor Green
Write-Host "==============================" -ForegroundColor Green

# Auto-detect platform
if ($Platform -eq "Auto") {
    if ($IsWindows -or $env:OS -eq "Windows_NT") {
        $Platform = "Windows"
        $platformRuntime = "win-x64"
        $executableName = "srectl.exe"
    }
    elseif ($IsLinux) {
        $Platform = "Linux"
        $platformRuntime = "linux-x64"
        $executableName = "srectl"
    }
    elseif ($IsMacOS) {
        # Detect Apple Silicon vs Intel
        $arch = uname -m 2>$null
        if ($arch -eq "arm64") {
            $Platform = "macOS-AppleSilicon"
            $platformRuntime = "osx-arm64"
        } else {
            $Platform = "macOS-Intel"
            $platformRuntime = "osx-x64"
        }
        $executableName = "srectl"
    }
    else {
        $Platform = "Linux"  # Default fallback
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

Write-Host "Target Platform: $Platform ($platformRuntime)" -ForegroundColor Cyan

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
Write-Host "Installation Type: $(if ($UserInstall) { 'User' } else { 'System' })" -ForegroundColor Cyan

# Create output directory
$outputDir = Join-Path $projectDir "publish"
$buildOutputDir = Join-Path $outputDir $platformRuntime

if (Test-Path $outputDir) {
    Remove-Item $outputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $buildOutputDir -Force | Out-Null

# Build self-contained executable
Write-Host "`nBuilding SRECTL executable..." -ForegroundColor Cyan

$buildArgs = @(
    "publish"
    $projectFile
    "--configuration", "Release"
    "--runtime", $platformRuntime
    "--self-contained", "true"
    "--output", $buildOutputDir
    "--verbosity", "minimal"
    "-p:PackAsTool=false"  # Disable PackAsTool for self-contained builds
    "-p:PublishSingleFile=false"  # Disable single file to avoid Assembly.Location issues
    "-p:PublishTrimmed=false"  # Disable trimming to avoid issues
)

Write-Host "Running: dotnet $($buildArgs -join ' ')" -ForegroundColor Gray
& dotnet @buildArgs

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to build executable. Exit code: $LASTEXITCODE"
    exit $LASTEXITCODE
}

Write-Host "[OK] Build completed successfully" -ForegroundColor Green

# Find the built executable
$sourceExecutable = Join-Path $buildOutputDir $executableName

if (-not (Test-Path $sourceExecutable)) {
    Write-Error "Built executable not found: $sourceExecutable"
    exit 1
}

# Get executable file info
$executableInfo = Get-Item $sourceExecutable
$executableSizeMB = [math]::Round($executableInfo.Length / 1MB, 2)
Write-Host "Built executable: $($executableInfo.Name) ($executableSizeMB MB)" -ForegroundColor Green

# Check if installation directory exists and create if needed
if (-not (Test-Path $InstallPath)) {
    Write-Host "`nCreating installation directory..." -ForegroundColor Cyan
    try {
        New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
        Write-Host "[OK] Installation directory created: $InstallPath" -ForegroundColor Green
    }
    catch {
        Write-Error "Failed to create installation directory: $($_.Exception.Message)"
        if (-not $UserInstall) {
            Write-Host "Hint: Try running as Administrator for system-wide installation" -ForegroundColor Yellow
        }
        exit 1
    }
}

# Check for existing installation
$targetExecutable = Join-Path $InstallPath $executableName

if (Test-Path $targetExecutable) {
    if (-not $Force) {
        Write-Host "`nExisting installation found: $targetExecutable" -ForegroundColor Yellow
        $response = Read-Host "Overwrite existing installation? (Y/n)"
        if ($response -and $response.ToLower() -ne 'y' -and $response.ToLower() -ne 'yes') {
            Write-Host "Installation cancelled." -ForegroundColor Yellow
            exit 0
        }
    }
    
    Write-Host "Removing existing installation..." -ForegroundColor Cyan
    try {
        Remove-Item $targetExecutable -Force
        Write-Host "[OK] Existing installation removed" -ForegroundColor Green
    }
    catch {
        Write-Error "Failed to remove existing installation: $($_.Exception.Message)"
        exit 1
    }
}

# Copy executable to installation directory
Write-Host "`nInstalling SRECTL executable..." -ForegroundColor Cyan

try {
    # Copy all files from build output to installation directory
    Get-ChildItem -Path $buildOutputDir -File | ForEach-Object {
        Copy-Item $_.FullName (Join-Path $InstallPath $_.Name) -Force
    }
    Write-Host "[OK] Executable and dependencies installed: $targetExecutable" -ForegroundColor Green
}
catch {
    Write-Error "Failed to copy executable: $($_.Exception.Message)"
    if (-not $UserInstall) {
        Write-Host "Hint: Try running as Administrator for system-wide installation" -ForegroundColor Yellow
    }
    exit 1
}

# Make executable on Unix-like systems
if ($Platform -ne "Windows") {
    Write-Host "Setting executable permissions..." -ForegroundColor Cyan
    try {
        & chmod +x $targetExecutable 2>$null
        Write-Host "[OK] Executable permissions set" -ForegroundColor Green
    }
    catch {
        Write-Warning "Failed to set executable permissions. You may need to run 'chmod +x $targetExecutable' manually."
    }
}

# Update PATH environment variable
if (-not $NoPathUpdate) {
    Write-Host "`nUpdating PATH environment variable..." -ForegroundColor Cyan
    
    if ($Platform -eq "Windows") {
        # Windows PATH update
        $currentUserPath = [Environment]::GetEnvironmentVariable("PATH", "User")
        $currentSystemPath = [Environment]::GetEnvironmentVariable("PATH", "Machine")
        
        $pathToAdd = $InstallPath
        $alreadyInPath = $false
        
        if ($UserInstall) {
            $alreadyInPath = ($currentUserPath -split ';') -contains $pathToAdd
            if (-not $alreadyInPath) {
                try {
                    $newUserPath = if ($currentUserPath) { "$currentUserPath;$pathToAdd" } else { $pathToAdd }
                    [Environment]::SetEnvironmentVariable("PATH", $newUserPath, "User")
                    Write-Host "[OK] PATH updated for current user" -ForegroundColor Green
                }
                catch {
                    Write-Warning "Failed to update user PATH: $($_.Exception.Message)"
                }
            }
        } else {
            $alreadyInPath = ($currentSystemPath -split ';') -contains $pathToAdd
            if (-not $alreadyInPath) {
                try {
                    $newSystemPath = if ($currentSystemPath) { "$currentSystemPath;$pathToAdd" } else { $pathToAdd }
                    [Environment]::SetEnvironmentVariable("PATH", $newSystemPath, "Machine")
                    Write-Host "[OK] PATH updated system-wide" -ForegroundColor Green
                }
                catch {
                    Write-Warning "Failed to update system PATH: $($_.Exception.Message). Try running as Administrator."
                }
            }
        }
        
        if ($alreadyInPath) {
            Write-Host "[OK] PATH already contains installation directory" -ForegroundColor Green
        }
    } else {
        # Unix-like PATH update
        $shellProfile = ""
        $shell = $env:SHELL
        
        if ($shell -like "*bash*") {
            $shellProfile = Join-Path $env:HOME ".bashrc"
        } elseif ($shell -like "*zsh*") {
            $shellProfile = Join-Path $env:HOME ".zshrc"
        } else {
            $shellProfile = Join-Path $env:HOME ".profile"
        }
        
        $pathExport = "export PATH=`"`$PATH:$InstallPath`""
        
        try {
            if (Test-Path $shellProfile) {
                $profileContent = Get-Content $shellProfile -Raw
                if ($profileContent -notmatch [regex]::Escape($pathExport)) {
                    Add-Content $shellProfile "`n# Added by SRECTL installer`n$pathExport"
                    Write-Host "[OK] PATH updated in $shellProfile" -ForegroundColor Green
                } else {
                    Write-Host "[OK] PATH already configured in $shellProfile" -ForegroundColor Green
                }
            } else {
                Set-Content $shellProfile "# Added by SRECTL installer`n$pathExport"
                Write-Host "[OK] PATH configured in new $shellProfile" -ForegroundColor Green
            }
        }
        catch {
            Write-Warning "Failed to update shell profile: $($_.Exception.Message)"
        }
    }
}

# Create desktop shortcut (Windows only)
if ($AddDesktopShortcut -and $Platform -eq "Windows") {
    Write-Host "`nCreating desktop shortcut..." -ForegroundColor Cyan
    
    try {
        $desktopPath = [Environment]::GetFolderPath("Desktop")
        $shortcutPath = Join-Path $desktopPath "SRECTL.lnk"
        
        $WshShell = New-Object -comObject WScript.Shell
        $Shortcut = $WshShell.CreateShortcut($shortcutPath)
        $Shortcut.TargetPath = "cmd.exe"
        $Shortcut.Arguments = "/k `"$targetExecutable --help`""
        $Shortcut.WorkingDirectory = $env:USERPROFILE
        $Shortcut.Description = "SRECTL - SRE Agent CLI Tool"
        $Shortcut.Save()
        
        Write-Host "[OK] Desktop shortcut created: $shortcutPath" -ForegroundColor Green
    }
    catch {
        Write-Warning "Failed to create desktop shortcut: $($_.Exception.Message)"
    }
}

# Test installation
Write-Host "`nTesting installation..." -ForegroundColor Cyan

try {
    $versionOutput = & $targetExecutable --version 2>$null
    if ($LASTEXITCODE -eq 0 -and $versionOutput) {
        Write-Host "[OK] SRECTL is working: $versionOutput" -ForegroundColor Green
    } else {
        Write-Warning "SRECTL executable responds but version check failed"
    }
}
catch {
    Write-Warning "Could not test SRECTL installation: $($_.Exception.Message)"
}

# Clean up build output
Write-Host "`nCleaning up build files..." -ForegroundColor Cyan
try {
    Remove-Item $outputDir -Recurse -Force
    Write-Host "[OK] Build files cleaned up" -ForegroundColor Green
}
catch {
    Write-Warning "Failed to clean up build files: $($_.Exception.Message)"
}

# Final success message
Write-Host "`n[SUCCESS] SRECTL installation completed!" -ForegroundColor Green

Write-Host "`nInstallation Details:" -ForegroundColor Yellow
Write-Host "  - Executable: $targetExecutable" -ForegroundColor White
Write-Host "  - Size: $executableSizeMB MB" -ForegroundColor White
Write-Host "  - Platform: $Platform ($platformRuntime)" -ForegroundColor White
Write-Host "  - Type: $(if ($UserInstall) { 'User installation' } else { 'System installation' })" -ForegroundColor White

Write-Host "`nNext Steps:" -ForegroundColor Yellow
if ($NoPathUpdate) {
    Write-Host "  - Add to PATH manually: $InstallPath" -ForegroundColor White
}
if ($Platform -ne "Windows") {
    Write-Host "  - Restart your shell or run: source $shellProfile" -ForegroundColor White
} else {
    Write-Host "  - Restart your command prompt or PowerShell" -ForegroundColor White
}
Write-Host "  - Run: srectl --help" -ForegroundColor White
Write-Host "  - Run: srectl init --resource-url <your-server-url>" -ForegroundColor White

Write-Host "`nUninstall Instructions:" -ForegroundColor Yellow
Write-Host "  - Delete: $targetExecutable" -ForegroundColor White
if (-not $NoPathUpdate) {
    Write-Host "  - Remove from PATH: $InstallPath" -ForegroundColor White
}
if ($AddDesktopShortcut -and $Platform -eq "Windows") {
    Write-Host "  - Delete desktop shortcut if created" -ForegroundColor White
}
