#!/bin/bash

# Start Session.Proxy in background
cd /app/session-proxy && dotnet Session.Proxy.dll &
cd /app

# Execute the original entrypoint from the session pool code interpreter base image
exec /app/entrypoint.sh "$@"
