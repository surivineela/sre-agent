# PowerShell script to process YAML files and extract directCommands
param(
    [Parameter(Mandatory=$true)]
    [string]$Source,
    
    [Parameter(Mandatory=$true)]
    [string]$OutputFolder,
    
    [Parameter(Mandatory=$false)]
    [int]$MaxChunkSize = 1800
)

# Install powershell-yaml module if not already installed
if (-not (Get-Module -ListAvailable -Name powershell-yaml)) {
    Write-Host "Installing powershell-yaml module..."
    Install-Module -Name powershell-yaml -Force -Scope CurrentUser
}

Import-Module powershell-yaml

function Process-Command {
    param(
        [hashtable]$Command,
        [int]$MaxChunkSize
    )
    
    # Extract the name property as commandGroup
    $commandGroup = ""
    if ($Command.ContainsKey('name')) {
        $commandGroup = $Command['name']
    }
    
    # Log error if commandGroup is empty
    if ([string]::IsNullOrEmpty($commandGroup)) {
        Write-Error "Command is missing 'name' property or has empty name value"
    }
    
    # Convert command to YAML
    $commandYaml = ConvertTo-Yaml $Command
    
    # Split YAML content into segments of less than MaxChunkSize characters    
    $segments = @()
    $lines = $commandYaml -split "`r?`n"
    $currentSegment = ""
    
    foreach ($line in $lines) {
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }

        $testSegment = if ($currentSegment -eq "") { $line } else { "$currentSegment`n$line" }
        
        if ($testSegment.Length -gt $MaxChunkSize -and $currentSegment -ne "") {
            # Add command group to current segment and save it
            $segmentWithGroup = "Syntax: $commandGroup [optional parameters]`n$currentSegment"
            $segments += $segmentWithGroup
            $currentSegment = $line
        }
        else {
            $currentSegment = $testSegment
        }
    }
    
    # Add the last segment if it exists
    if ($currentSegment -ne "") {
        $segmentWithGroup = "Syntax: $commandGroup [optional parameters]`n$currentSegment"
        $segments += $segmentWithGroup
    }
    
    return $segments
}

function Process-YamlFile {
    param(
        [string]$FilePath,
        [string]$RelativePath,
        [string]$OutputRoot,
        [int]$MaxChunkSize
    )
    
    try {
        Write-Host "Processing: $FilePath"
        
        # Use -LiteralPath for Test-Path and Get-Content when the path might contain special characters.
        if (-not (Test-Path -LiteralPath $FilePath)) {
            Write-Error "File does not exist or cannot be accessed literally: $FilePath"
            return # Exit this function for this file
        }
        
        $yamlContent = Get-Content -LiteralPath $FilePath -Raw

        if ([string]::IsNullOrWhiteSpace($yamlContent)) {
            Write-Warning "Skipping empty YAML file: $FilePath"
            return
        }
        $yamlObject = ConvertFrom-Yaml $yamlContent
        
        # Check if directCommands property exists
        if (-not $yamlObject.ContainsKey('directCommands')) {
            Write-Warning "No 'directCommands' property found in $FilePath"
            return
        }
        
        $directCommands = $yamlObject['directCommands']
        
        if ($directCommands -isnot [System.Collections.IEnumerable] -or $directCommands -is [string]) {
            Write-Warning "'directCommands' is not an array in $FilePath"
            return
        }
        
        # Create output directory maintaining folder structure
        # Sanitize the relative directory path (remove invalid characters like brackets)
        $relativeDir = Split-Path $RelativePath -Parent -ErrorAction SilentlyContinue
        $sanitizedDir = $relativeDir -replace '[<>:"/\\|?*\[\]]', '_'
        $outputDir = Join-Path $OutputRoot $sanitizedDir

        if (-not (Test-Path $outputDir)) {
            New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
        }
          # Process each command in directCommands array
        foreach ($command in $directCommands) {
            if ($command -is [hashtable] -and $command.ContainsKey('uid')) {
                $uid = $command['uid']
                  # Process command using Process-Command function
                $segments = Process-Command -Command $command -MaxChunkSize $MaxChunkSize
                
                # Write each segment to a separate file
                for ($i = 0; $i -lt $segments.Count; $i++) {
                    # Sanitize filename (remove invalid characters)
                    $safeFileName = $uid -replace '[<>:"/\\|?*\[\]]', '_'
                    $outputFile = Join-Path $outputDir "$safeFileName-$i.yaml"
                    
                    Set-Content -Path $outputFile -Value $segments[$i] -Encoding UTF8
                    Write-Host "  -> Created: $outputFile"
                }
            }
            else {
                Write-Warning "Command without 'uid' property found in $FilePath"
            }
        }
    }
    catch {
        Write-Error "Error processing $FilePath`: $($_.Exception.Message)"
    }
}

# Main script execution
if (-not (Test-Path $Source)) {
    Write-Error "Source '$Source' does not exist."
    exit 1
}

# Create output folder if it doesn't exist
if (-not (Test-Path $OutputFolder)) {
    New-Item -ItemType Directory -Path $OutputFolder -Force | Out-Null
}

# Check if Source is a file or folder
$sourceItem = Get-Item $Source

if ($sourceItem.PSIsContainer) {
    # Processing folder
    Write-Host "Processing YAML files from folder '$Source' to '$OutputFolder'..."
    
    # Get all YAML files recursively
    $yamlFiles = Get-ChildItem -Path $Source -Filter "*.yaml" -Recurse
    $yamlFiles += Get-ChildItem -Path $Source -Filter "*.yml" -Recurse
    
    if ($yamlFiles.Count -eq 0) {
        Write-Warning "No YAML files found in '$Source'"
        exit 0
    }
    
    Write-Host "Found $($yamlFiles.Count) YAML file(s) to process."
    
    foreach ($file in $yamlFiles) {
    # Calculate relative path to maintain folder structure
    $relativePath = $file.FullName.Substring($Source.Length).TrimStart('\', '/')
    
    try {
        Process-YamlFile -FilePath $file.FullName -RelativePath $relativePath -OutputRoot $OutputFolder -MaxChunkSize $MaxChunkSize
    }
    catch {
        Write-Error "Failed to process file '$($file.FullName)': $($_.Exception.Message)"
        continue # Skip to next file
    }
}
}
else {
    # Processing single file
    if ($sourceItem.Extension -notin @('.yaml', '.yml')) {
        Write-Error "Source file '$Source' is not a YAML file (.yaml or .yml extension required)."
        exit 1
    }
    
    Write-Host "Processing single YAML file '$Source' to '$OutputFolder'..."
      # Use just the filename for single file processing
    $relativePath = $sourceItem.Name
    
    Process-YamlFile -FilePath $Source -RelativePath $relativePath -OutputRoot $OutputFolder -MaxChunkSize $MaxChunkSize
}

Write-Host "Processing complete!"