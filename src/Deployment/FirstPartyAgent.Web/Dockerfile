# --------------------------------------------------------------------------------
# Runtime image only — uses published output from external build
# --------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Container listens on port 80 by default
EXPOSE 80

# Copy all of the published output into /app
# (build-and-run.cmd should publish into the context root)
COPY . .

# Start the app
ENTRYPOINT ["dotnet", "FirstPartyAgent.Web.dll"]
