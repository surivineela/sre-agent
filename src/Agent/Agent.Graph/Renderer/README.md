# Mermaid API Docker Deployment

This folder contains scripts to build and push a Docker image to Azure Container Registry (ACR) named **dailyreportacr**. The Docker image runs a FastAPI service that generates Mermaid diagrams.

## Build and Push the Docker Image

### Using Bash (push.sh)
1. Make sure you are logged into Azure CLI:

    `az login`

2. Make the script executable and run it:

    `chmod +x push.sh ./push.sh`

### Using PowerShell (push.ps1)
  `.\push.ps1`


Both scripts build the image, tag it as `dailyreportacr.azurecr.io/mermaid-api:latest`, log into ACR, and push the image.

### Optionally, you can also deploy to Container Apps for testing
```bash
az containerapp create
--name mermaid-api
--resource-group my-resource-group
--environment my-container-env
--image dailyreportacr.azurecr.io/mermaid-api:latest
--target-port 8000
--ingress external
--registry-server dailyreportacr.azurecr.io
--registry-username $(az acr credential show --name dailyreportacr --query "username" -o tsv)
--registry-password $(az acr credential show --name dailyreportacr --query "passwords[0].value" -o tsv)
```


## Call the API

Once deployed (for example, on Azure Container Apps), you can call the API's `/render` endpoint.

### Sample Payload
The payload needs to follow the mermaid spec:

```json
{
"spec": "graph LR; A-->B; B-->C; C-->A;"
}


### Making a POST request
```bash
curl -X POST "http://<host>:8000/render" \
     -H "Content-Type: application/json" \
     -d '{"spec": "graph LR; A-->B; B-->C; C-->A;"}'
```

The API returns a base64 encoded png that we can render on the chat client.