[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory = $false)]
    [string]$Location = "northcentralus",
    
    [Parameter(Mandatory = $false)]
    [string]$SchedulerName = "scheduler1",
    
    [Parameter(Mandatory = $false)]
    [string]$TaskHubName = "taskhub1"
)

# Function to check if a command was successful
function Test-CommandSuccess {
    param (
        [string]$CommandName
    )
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Error executing $CommandName. Exit code: $LASTEXITCODE"
        exit $LASTEXITCODE
    }
}

# Display script information
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "  Azure Durable Task Scheduler Setup Script" -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "Resource Group: $ResourceGroupName"
Write-Host "Location: $Location"
Write-Host "Scheduler Name: $SchedulerName"
Write-Host "Task Hub Name: $TaskHubName"
Write-Host "======================================================" -ForegroundColor Cyan

# Step 1: Register resource providers for Microsoft.DurableTask
Write-Host "Step 1: Registering Microsoft.DurableTask resource provider..." -ForegroundColor Green
Write-Host "Registering feature 'PrivatePreview' for Microsoft.DurableTask namespace..."
az feature register --namespace Microsoft.DurableTask --name PrivatePreview
Test-CommandSuccess "az feature register"

Write-Host "Registering Microsoft.DurableTask provider..."
az provider register -n Microsoft.DurableTask
Test-CommandSuccess "az provider register"

# Step 2: Upgrade Azure CLI and add the durabletask extension
Write-Host "Step 2: Upgrading Azure CLI and adding Durable Task extension..." -ForegroundColor Green
Write-Host "NOTE: The az upgrade command will launch the MSI installer which requires user interaction. Please respond to any prompts that appear." -ForegroundColor Yellow
Write-Host "Press Enter to continue with the upgrade or Ctrl+C to cancel..." -ForegroundColor Yellow
$null = Read-Host

az upgrade --yes
Test-CommandSuccess "az upgrade"

Write-Host "Registering Microsoft.DurableTask provider again after upgrade..."
az provider register -n Microsoft.DurableTask
Test-CommandSuccess "az provider register"

Write-Host "Checking Microsoft.DurableTask provider status..."
az provider show -n Microsoft.DurableTask
Test-CommandSuccess "az provider show"

Write-Host "Checking if durabletask extension is already installed..."
$extension = az extension list --query "[?name=='durabletask']" | ConvertFrom-Json
if ($null -eq $extension -or $extension.Count -eq 0) {
    Write-Host "Installing durabletask extension..."
    az extension add --name durabletask --allow-preview true
    Test-CommandSuccess "az extension add"
} else {
    Write-Host "durabletask extension is already installed."
}

# Step 3: Create resource group if it doesn't exist
Write-Host "Step 3: Creating resource group if it doesn't exist..." -ForegroundColor Green
$resourceGroup = az group list --query "[?name=='$ResourceGroupName']" | ConvertFrom-Json
if ($null -eq $resourceGroup -or $resourceGroup.Count -eq 0) {
    Write-Host "Creating resource group '$ResourceGroupName' in '$Location'..."
    az group create --name $ResourceGroupName --location $Location
    Test-CommandSuccess "az group create"
} else {
    Write-Host "Resource group '$ResourceGroupName' already exists."
}

# Step 4: Create Durable Task Scheduler resource
Write-Host "Step 4: Creating Durable Task Scheduler resource..." -ForegroundColor Green
$scheduler = az durabletask scheduler list --resource-group $ResourceGroupName --query "[?name=='$SchedulerName']" | ConvertFrom-Json
if ($null -eq $scheduler -or $scheduler.Count -eq 0) {
    Write-Host "Creating Durable Task Scheduler '$SchedulerName'..."
    az durabletask scheduler create `
        --resource-group $ResourceGroupName `
        --name $SchedulerName `
        --ip-allowlist '["0.0.0.0/0"]' `
        --sku-name "Dedicated" `
        --sku-capacity 1
    Test-CommandSuccess "az durabletask scheduler create"
} else {
    Write-Host "Scheduler '$SchedulerName' already exists."
}

# Step 5: Create Task Hub within the scheduler resource
Write-Host "Step 5: Creating Task Hub within the scheduler resource..." -ForegroundColor Green
$taskHub = az durabletask taskhub list --resource-group $ResourceGroupName --scheduler-name $SchedulerName --query "[?name=='$TaskHubName']" | ConvertFrom-Json
if ($null -eq $taskHub -or $taskHub.Count -eq 0) {
    Write-Host "Creating Task Hub '$TaskHubName'..."
    az durabletask taskhub create `
        --resource-group $ResourceGroupName `
        --scheduler-name $SchedulerName `
        --name $TaskHubName
    Test-CommandSuccess "az durabletask taskhub create"
} else {
    Write-Host "Task Hub '$TaskHubName' already exists."
}

# Step 6: Grant the current user permission to connect to the task hub
Write-Host "Step 6: Granting current user permission to connect to the task hub..." -ForegroundColor Green
$subscriptionId = az account show --query "id" -o tsv
$loggedInUser = az account show --query "user.name" -o tsv

Write-Host "Current user: $loggedInUser"
Write-Host "Subscription ID: $subscriptionId"

$scope = "/subscriptions/$subscriptionId/resourceGroups/$ResourceGroupName/providers/Microsoft.DurableTask/schedulers/$SchedulerName/taskHubs/$TaskHubName"
Write-Host "Assigning 'Durable Task Data Contributor' role to current user..."
az role assignment create `
    --assignee $loggedInUser `
    --role "Durable Task Data Contributor" `
    --scope $scope
Test-CommandSuccess "az role assignment create"

Write-Host "Role assignment created. Note: it may take a minute for the role assignment to take effect." -ForegroundColor Yellow

# Step 7: Generate a connection string and set environment variable
Write-Host "Step 7: Generating connection string and setting environment variable..." -ForegroundColor Green
$endpoint = az durabletask scheduler show `
    --resource-group $ResourceGroupName `
    --name $SchedulerName `
    --query "properties.endpoint" `
    --output tsv
$env:DURABLE_TASK_SCHEDULER_CONNECTION_STRING = "Endpoint=$endpoint;TaskHub=$TaskHubName;Authentication=DefaultAzure"

Write-Host "Connection string set to environment variable DURABLE_TASK_SCHEDULER_CONNECTION_STRING" -ForegroundColor Green
Write-Host $env:DURABLE_TASK_SCHEDULER_CONNECTION_STRING

# Step 8: Get and open the dashboard URL
Write-Host "Step 8: Getting and opening the dashboard URL..." -ForegroundColor Green
$dashboardUrl = az durabletask taskhub show `
    --resource-group $ResourceGroupName `
    --scheduler-name $SchedulerName `
    --name $TaskHubName `
    --query "properties.dashboardUrl" `
    --output tsv

Write-Host "Dashboard URL: $dashboardUrl" -ForegroundColor Cyan

# Open the dashboard URL in the default browser
Write-Host "Opening dashboard in default browser..." -ForegroundColor Green
Start-Process $dashboardUrl

Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "  Setup Complete!" -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan
