# SRE Agent

## Introduction

Azure SRE Agent is a unified agentic platform for monitoring and troubleshooting Azure applications and services. Get started quickly with the `Agent.Web` project and extend functionality using the plugins and helpers in `Agent.Core`.

![Component Diagram](docs/images/sre_components.jpg)

## Getting Started

1. **Configure the Agent.Web Project**  
   In the `Agent.Web` project, add an `appsettings.Development.json` file with the following configuration:

   ```json
   {
     "Azure": {
       "OpenAI": {
         "DeploymentName": "gpt-4o",
         "Endpoint": "<open-ai-endpoint>",
         "ApiKey": "<azure-openai-key>"
       }
     }
   }
   ```

2. **Launch the Solution**  
   Navigate to the directory containing the solution file (`Agent.sln`) and open it with your preferred IDE (e.g., Visual Studio). For example, in a PowerShell prompt:

   ```powershell
   .\AAPT-Antares-OperationalAgent\src\Agent>Agent.sln
   ```

3. **Run the Application**  
   Build and run the solution. The `Agent.Web` project will start a test chat client that will use your identity to access Azure resources.
   
   ![Project Demo](docs/images/Project.gif)

   Happy monitoring and troubleshooting!

## Build and Test

TODO: Describe and show how to build your code and run the tests.