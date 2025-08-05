# Build, package, and install SREAgent CLI
param (
    [string]$PackageDir = "./nupkg"
)

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

# Uninstall existing tool and install the new one
Write-Host "Uninstalling existing tool (if any)..."
dotnet tool uninstall sreagent.cli --global 2>$null

Write-Host "Installing tool from local package..."
dotnet tool install sreagent.cli --global --add-source $fullPackageDir

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ SREAgent CLI tool installed successfully!"
    Write-Host "You can now use 'srectl' command globally."
} else {
    Write-Error "Failed to install the tool. Exit code: $LASTEXITCODE"
    exit $LASTEXITCODE
}
