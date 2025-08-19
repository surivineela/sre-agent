# Use the official .NET 9 runtime image to run the app
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY . .

# Set environment variables (optional)
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV AppSettings:Core:External:IncidentManagement:Type=Icm 
ENV AppSettings:Core:External:IncidentManagement:ICMAPI:ReadOnly=true
ENV AppSettings:Core:External:IncidentManagement:AutomatedRCA:Enabled=false
ENV AGENT_TYPE_NAME=RCARouterAgent

# Expose the port the app runs on
EXPOSE 8080

# Run the application
ENTRYPOINT ["dotnet", "Agent.Web.dll", "--session"]
