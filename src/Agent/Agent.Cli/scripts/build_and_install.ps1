# Build, package, and install SREAgent CLI
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

dotnet pack $projectFile --configuration Release --output $fullPackageDir

dotnet tool uninstall sreagent.cli --global 2>$null
dotnet tool install sreagent.cli --global --add-source $fullPackageDir
