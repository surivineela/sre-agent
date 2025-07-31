# PowerShell script to process Azure documentation YAML files
param(
    [Parameter(Mandatory=$true)]
    [string]$Source,
    
    [Parameter(Mandatory=$true)]
    [string]$OutputFolder,
    
    [Parameter(Mandatory=$false)]
    [int]$MaxChunkSize = 1900,
    
    [Parameter(Mandatory=$false)]
    [int]$OverlapSize = 200
)

# Main script execution
if (-not (Test-Path $Source)) {
    Write-Error "Source '$Source' does not exist."
    exit 1
}

# Create output folder if it doesn't exist
if (-not (Test-Path $OutputFolder)) {
    New-Item -ItemType Directory -Path $OutputFolder -Force | Out-Null
}

Write-Host "Processing Kubernetes runbooks from folder '$Source' to '$OutputFolder'..."
    
# Get all Markdown files recursively
$markdownFiles = Get-ChildItem -Path $Source -Filter "*.md" -Recurse

if ($markdownFiles.Count -eq 0) {
    Write-Warning "No Markdown files found in '$Source'"
    exit 0
}

Write-Host "Found $($markdownFiles.Count) Markdown file(s) to process."

foreach ($file in $markdownFiles) {
    if ($file.Name -eq "README.md") {
        Write-Host "Skipping README.md file: $($file.FullName)"
        continue
    }

    # Calculate relative path to maintain folder structure
    $relativePath = $file.FullName.Substring($Source.Length).TrimStart('\', '/')
    
    try {
        # No real logic. Just copying the file
        $relativeDir = Split-Path $RelativePath -Parent -ErrorAction SilentlyContinue
        $outputDir = Join-Path $OutputFolder $relativeDir

        if (-not (Test-Path $outputDir)) {
            New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
        }
        Copy-Item -Path $file.FullName -Destination (Join-Path -Path $OutputFolder -ChildPath $relativePath) -Force
        Write-Host "Processed file: $relativePath"
    }
    catch {
        Write-Error "Failed to process file '$($file.FullName)': $($_.Exception.Message)"
        continue # Skip to next file
    }
}

Write-Host "Processing complete!"