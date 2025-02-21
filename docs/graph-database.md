# Graph Database Guide

## Prerequisites
- Azure CLI
- PowerShell environment

## Setup Steps

1. **Set Environment Variables**
   ```powershell
   resourceGroupName="msdocs-cosmos-gremlin-quickstart"
   location="westus"
   let suffix=$RANDOM*$RANDOM
   accountName="msdocs-gremlin-$suffix"
   ```

2. **Create Resources**
   - Login to Azure CLI: `az login`
   - Create resource group:
     ```powershell
     az group create --name $resourceGroupName --location $location
     ```
   - Create Cosmos DB account:
     ```powershell
     az cosmosdb create \
         --resource-group $resourceGroupName \
         --name $accountName \
         --capabilities "EnableGremlin" \
         --locations regionName=$location \
         --enable-free-tier true
     ```

3. **Get Credentials**
   - Get API endpoint name:
     ```powershell
     az cosmosdb show --resource-group $resourceGroupName --name $accountName --query "name"
     ```
   - Get primary key:
     ```powershell
     az cosmosdb keys list --resource-group $resourceGroupName --name $accountName --type "keys" --query "primaryMasterKey"
     ```

4. **Create Database and Graph**
   ```powershell
   az cosmosdb gremlin database create \
       --resource-group $resourceGroupName \
       --account-name $accountName \
       --name "resourcegraph"

   az cosmosdb gremlin graph create \
       --resource-group $resourceGroupName \
       --account-name $accountName \
       --database-name "resourcegraph" \
       --name "resources" \
       --partition-key-path "/resourceType" \
       --throughput 400
   ```

5. **Update Configuration**  
   Add to `appsettings.Development.json`:
   ```json
   "Gremlin": {
     "AccountName": "<<ACCOUNTNAME>>",
     "AccountKey": "<<<ACCOUNTKEY>>",
     "Database": "resourcegraph",
     "Collection": "resources"
   }
   ```

[Back to Running the App](running-the-app.md) | [Next: Graph Visualization](graph-visualization.md) 