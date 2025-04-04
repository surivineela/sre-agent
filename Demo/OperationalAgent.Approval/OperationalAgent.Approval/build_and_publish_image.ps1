dotnet publish -c Release -o publish
docker build -t sreagent.azurecr.io/approval-service:latest -f Dockerfile publish
az acr login -n sreagent.azurecr.io
docker push sreagent.azurecr.io/approval-service:latest