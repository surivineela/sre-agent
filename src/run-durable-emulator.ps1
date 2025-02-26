Write-Host "Starting Durable Task Emulator..." 
Write-Host "Connection string: Endpoint=http://localhost:14280;TaskHub=default;Authentication=None"
Write-Host "Dashboard url: http://localhost:14282"
# Open the dashboard URL in the default browser
Start-Process "http://localhost:14282"
docker run --rm -it --name dts-emulator -p 14280:8080 -p 14282:8082 -e ClientAuth__DisableAuthentication=true durabletaskspublic.azurecr.io/dts-emulator:v0.0.2-amd64