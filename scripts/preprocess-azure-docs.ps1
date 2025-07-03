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

# Install powershell-yaml module if not already installed
if (-not (Get-Module -ListAvailable -Name powershell-yaml)) {
    Write-Host "Installing powershell-yaml module..."
    Install-Module -Name powershell-yaml -Force -Scope CurrentUser
}

Import-Module powershell-yaml

function ProcessDocument {
    param(
        [string]$Name,
        [string]$FilePath,
        [int]$MaxChunkSize,
        [int]$OverlapSize
    )
    
    try {
        if (-not (Test-Path $FilePath)) {
            Write-Warning "File not found: $FilePath"
            return
        }
        
        # Read file content
        $content = Get-Content -Path $FilePath -Raw -Encoding UTF8
        
        # Extract frontmatter
        $description = ""
        if ($content -match '(?s)^---\s*\r?\n(.*?)\r?\n---\s*\r?\n(.*)$') {
            $frontmatter = $matches[1]
            $markdownContent = $matches[2]
            
            # Extract description from frontmatter
            if ($frontmatter -match '(?m)^description:\s*(.+)$') {
                $description = $matches[1].Trim('"', "'")
            }
        } else {
            $markdownContent = $content
        }
        
        # Split content into lines for processing
        $lines = $markdownContent -split '\r?\n'
        
        # Track heading hierarchy
        $headingStack = @()
        $currentChunkHeadings = @()
        $chunkCount = 0
        $currentChunk = ""
        
        for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($currentChunk.Length -eq 0){
                $currentChunk = BuildContextHeader -Description $description -HeadingStack $headingStack
            }
            $line = $lines[$i]
            $lineWithNewline = $line + "`n"
            
            # Write-Host "Processing line $($i + 1)/$($lines.Count): $line"
            # Check if line is a heading
            if ($line -match '^(#{1,6})\s+(.+)$') {
                $headingLevel = $matches[1].Length
                $headingText = $matches[2]
                
                # Track headings in current chunk
                $currentChunkHeadings += @{ Level = $headingLevel; Text = $headingText }
                
                # Add heading to current chunk
                $currentChunk += $lineWithNewline
            } else {
                # Regular content line - check if adding it would exceed size
                $potentialChunk = $currentChunk + $lineWithNewline
                
                if ($potentialChunk.Length -gt $MaxChunkSize -and $currentChunk.Length -gt 0) {
                    $fullChunk = $currentChunk.Trim()
                    SaveChunk -Name $Name -ChunkNumber ($chunkCount++) -Content $fullChunk
                    
                    # add current headings to heading stack
                    foreach ($heading in $currentChunkHeadings) {
                        $headingStack = UpdateHeadingStack -HeadingStack $headingStack -Level $heading.Level -Text $heading.Text
                    }

                    # Get overlap start index and update heading stack
                    $overlapStartIndex = RollbackForOverlap -Lines $lines -EndIndex ($i - 1) -MaxSize $OverlapSize -HeadingStack ([ref]$headingStack)

                    $currentChunk = ""
                    $i = $overlapStartIndex - 1 # Adjust index to start from overlap
                } else {
                    # Add line to current chunk
                    $currentChunk += $lineWithNewline
                }
            }
        }
        
        # Process final chunk if any content remains
        if ($currentChunk.Trim().Length -gt 0) {
            $fullChunk = $currentChunk.Trim()
            SaveChunk -Name $Name -ChunkNumber ($chunkCount++) -Content $fullChunk
        }
        
        Write-Host "    -> Created $chunkCount chunk(s) for $Name"
    }
    catch {
        Write-Error "Error processing document $Name from $FilePath`: $($_.Exception.Message)"
    }
}

function RollbackForOverlap {
    param(
        [array]$Lines,
        [int]$EndIndex,
        [int]$MaxSize,
        [ref]$HeadingStack
    )
    
    if ($EndIndex -lt 0 -or $Lines.Count -eq 0) {
        return -1
    }
    
    $currentSize = 0
    
    # Start from the end index and work backwards
    for ($i = $EndIndex; $i -ge 0; $i--) {
        $line = $Lines[$i]
        $lineLength = $line.Length + 1 # +1 for newline
        if ($currentSize + $lineLength -le $MaxSize) {
            $currentSize += $lineLength
            if ($line -match '^(#{1,6})\s+(.+)$') {
                $headingLevel = $matches[1].Length
                $headingText = $matches[2]
                $HeadingStack.Value = UpdateHeadingStack -HeadingStack $HeadingStack.Value -Level $headingLevel -Text $headingText -AddCurrentHeading $false
            }
        } else {
            return $i + 1  # Return the index where we can start without exceeding size
        }
    }
    
    return 0  # Can include all lines from beginning
}

function BuildContextHeader {
    param(
        [string]$Description,
        [array]$HeadingStack
    )
    
    $context = ""
    
    if ($Description) {
        $context += "Description: $Description`n"
    }
    
    if ($HeadingStack.Count -gt 0) {
        # Get headings that are not in current chunk
        foreach ($heading in $HeadingStack) {
            $headingText = if ($heading -is [hashtable] -and $heading.ContainsKey('Text')) { $heading['Text'] } else { $heading }
            $headingLevel = if ($heading -is [hashtable] -and $heading.ContainsKey('Level')) { $heading['Level'] } else { 1 }
            $markdownPrefix = "#" * $headingLevel
            $context += "$markdownPrefix $($headingText)`n"
        }
    }
    
    return $context
}

function UpdateHeadingStack {
    param(
        [array]$HeadingStack,
        [int]$Level,
        [string]$Text,
        [bool]$AddCurrentHeading = $true
    )
    
    # Remove headings at the same level or deeper
    $newStack = @()
    for ($i = 0; $i -lt $HeadingStack.Count; $i++) {
        if ($HeadingStack[$i].Level -lt $Level) {
            $newStack += $HeadingStack[$i]
        }
    }
    
    # Add current heading
    if ($AddCurrentHeading) {
        $newStack += @{ Level = $Level; Text = $Text }
    }
    
    return $newStack
}

function SaveChunk {
    param(
        [string]$Name,
        [int]$ChunkNumber,
        [string]$Content
    )
    
    # Create safe filename
    $safeFileName = $Name -replace '[^\w\-_\.]', '_'
    $chunkFileName = "$safeFileName`_$ChunkNumber.md"
    $chunkFilePath = Join-Path $OutputFolder $chunkFileName
    
    # Save chunk to file
    $Content | Out-File -FilePath $chunkFilePath -Encoding UTF8
    Write-Host "    -> Saved chunk $ChunkNumber for $Name to $chunkFilePath"
}

function ProcessTocItems {
    param(
        [System.Collections.IEnumerable]$Items,
        [string]$FolderPath
    )
    
    foreach ($item in $Items) {
        if ($item -is [hashtable]) {
            # Check if item has 'href' property
            if ($item.ContainsKey('href')) {
                $name = if ($item.ContainsKey('name')) { $item['name'] } else { "" }
                $href = $item['href']
                
                # Skip if href is not a .md file
                if (-not $href.EndsWith('.md', [System.StringComparison]::OrdinalIgnoreCase)) {
                    Write-Host "  -> Skipping non-markdown file: $name ($href)"
                    continue
                }
                
                # Construct full file path
                $filePath = Join-Path $FolderPath $href
                
                Write-Host "  -> Processing document: $name ($href)"
                ProcessDocument -Name $name -FilePath $filePath -MaxChunkSize $MaxChunkSize -OverlapSize $OverlapSize
            }
            
            # Check if item has 'items' property and recursively process
            if ($item.ContainsKey('items')) {
                $nestedItems = $item['items']
                if ($nestedItems -is [System.Collections.IEnumerable] -and $nestedItems -isnot [string]) {
                    ProcessTocItems -Items $nestedItems -FolderPath $FolderPath
                }
            }
        }
    }
}

function ProcessProduct {
    param(
        [string]$ProductName,
        [string]$FolderPath
    )
    
    try {
        Write-Host "Processing product: $ProductName from folder: $FolderPath"
        
        # Construct path to toc.yml file
        $tocFilePath = Join-Path $FolderPath "toc.yml"
        
        if (-not (Test-Path $tocFilePath)) {
            Write-Warning "toc.yml file not found in $FolderPath"
            return
        }
        
        # Read and parse YAML file
        $yamlContent = Get-Content -Path $tocFilePath -Raw -Encoding UTF8
        $yamlObject = ConvertFrom-Yaml $yamlContent
        
        # Determine the items to process
        $itemsToProcess = $null
        
        if ($yamlObject -is [System.Collections.IEnumerable] -and $yamlObject -isnot [string]) {
            # YAML content is already an array
            $itemsToProcess = $yamlObject
            Write-Host "  Found array with $($yamlObject.Count) items"
        }
        elseif ($yamlObject -is [hashtable] -and $yamlObject.ContainsKey('items')) {
            # YAML content is an object with 'items' property
            $itemsToProcess = $yamlObject['items']
            if ($itemsToProcess -is [System.Collections.IEnumerable] -and $itemsToProcess -isnot [string]) {
                Write-Host "  Found 'items' property with $($itemsToProcess.Count) items"
            }
            else {
                Write-Warning "'items' property is not an array in $tocFilePath"
                return
            }
        }
        else {
            Write-Warning "Unable to find items array or 'items' property in $tocFilePath"
            return
        }
        
        # Process the items
        if ($itemsToProcess) {
            ProcessTocItems -Items $itemsToProcess -FolderPath $FolderPath
        }
    }
    catch {
        Write-Error "Error processing product $ProductName from $FolderPath`: $($_.Exception.Message)"
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

Write-Host "Processing Azure documentation from folder '$Source' to '$OutputFolder'..."

# Check if the source folder itself contains toc.yml
$tocInSource = Join-Path $Source "toc.yml"
if (Test-Path $tocInSource) {
    Write-Host "Found toc.yml in source folder itself."
    $sourceItem = Get-Item $Source
    $productName = $sourceItem.Name
    ProcessProduct -ProductName $productName -FolderPath $Source
}
else {
    # Process each subfolder directly
    $subfolders = Get-ChildItem -Path $Source -Directory
    
    if ($subfolders.Count -eq 0) {
        Write-Warning "No subfolders found in '$Source'"
        exit 0
    }
    
    Write-Host "Found $($subfolders.Count) subfolder(s) to process."
    
    foreach ($folder in $subfolders) {
        ProcessProduct -ProductName $folder.Name -FolderPath $folder.FullName
    }
}

Write-Host "Processing complete!"