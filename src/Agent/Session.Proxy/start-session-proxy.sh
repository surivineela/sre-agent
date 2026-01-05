#!/bin/bash

# Set environment variables if not already set
if [ -z "$IDENTITY_ENDPOINT" ]; then
    export IDENTITY_ENDPOINT="${IdentityProvider__BaseUrl}/msi/token"
fi
if [ -z "$IDENTITY_HEADER" ]; then
    export IDENTITY_HEADER="dummy"
fi

# Start Session.Proxy in background
cd /app/session-proxy && dotnet Session.Proxy.dll &
cd /app

# Execute the original entrypoint from the session pool code interpreter base image
exec /app/entrypoint.sh "$@"
