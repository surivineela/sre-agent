#!/bin/bash

echo "Starting Durable Task Emulator..." 
echo "Connection string: Endpoint=http://localhost:14280;TaskHub=default;Authentication=None"
echo "Dashboard url: http://localhost:14282"

docker run --rm -it --name dts-emulator -p 14280:8080 -p 14282:8082 -e ClientAuth__DisableAuthentication=true mcr.microsoft.com/dts/dts-emulator:v0.0.6