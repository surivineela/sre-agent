# Variables
$RepoName = "keda-docs" # Repository name
$Branch = "main" # Branch name
$FolderPath = "content/docs/2.16/scalers" # Replace with the relative folder path in the repository
$OutputFile = "main.zip" # Output zip filename

# Construct the download URL
$DownloadUrl = "https://github.com/kedacore/$RepoName/archive/$Branch.zip"

# Download the zip file
Write-Host "Downloading repository as zip..." -ForegroundColor Green
Invoke-WebRequest -Uri $DownloadUrl -OutFile $OutputFile

# Extract the zip file
Write-Host "Extracting zip file..." -ForegroundColor Green
Expand-Archive -Path $OutputFile -DestinationPath "."

# Move the folder to the current directory
$ExtractedFolder = "$RepoName-$Branch\$FolderPath"
Write-Host "Moving folder to the current directory..." -ForegroundColor Green
Move-Item -Path $ExtractedFolder -Destination .

# Clean up
Write-Host "Cleaning up temporary files..." -ForegroundColor Green
Remove-Item -Path $OutputFile
Remove-Item -Recurse -Force -Path "$RepoName-$Branch"

Write-Host "Folder downloaded and extracted to the current directory." -ForegroundColor Green
