# SRE Agent (1P)

SRE Agent applied for First Party (Internal Microsoft) scenarios.

---

## Getting Started

**Agent Factory**

- [What is the SRE Icm Agent Factory](https://eng.ms/docs/cloud-ai-platform/devdiv/serverless-paas-balam/serverless-paas-vikr/app-service-web-apps/app-service-team-documents/applensteamdocs/sreicmagentfactory/home)
- [Onboarding to the factory](https://eng.ms/docs/cloud-ai-platform/devdiv/serverless-paas-balam/serverless-paas-vikr/app-service-web-apps/app-service-team-documents/applensteamdocs/sreicmagentfactory/onboarding)
- [Try out here](https://sre-icm-agent-factory.azurewebsites.net)

---

## Plugins Supported

- [AzureDevOpsPlugin](/docs/FirstPartyAgent/AzureDevOpsPlugin.md)
- [ICMPlugin](/docs/FirstPartyAgent/ICMPlugin.md)
- [GenevaActionsPlugin](/docs/FirstPartyAgent/GenevaActionsPlugin.md)
- [KustoPlugin](/docs/FirstPartyAgent/KustoPlugin.md)
- ObserverPlugin
- TimePlugin
- AzureAlertingPlugin
- TeamsPlugin
- HttpRequestsPlugin
- ChartPlugin

---

## Developing Locally

### Focus on these two projects for FirstPartyAgent
- [FirstPartyAgent.Core](/src/Agent/FirstPartyAgent.Core)
- [FirstPartyAgent.Web](/src/Agent/FirstPartyAgent.Web)

### Setting up configuration for Local Development

Under the Project [FirstPartyAgent.Web](/src/Agent/FirstPartyAgent.Web)
- Copy appsettings.json into appsettings.Development.json.
- Fill your appsettings.Development.json as below:
  - Fill up the Azure:OpenAI section using your Azure OpenAI credentials (you will create an Azure Open AI resource, go to Azure Foundry and ensure there is a deployment of gpt-4o model in your endpoint)
  - Fill the ICMAPI section:
    - Add your ICM user token (taken from opening ICM portal in the browser).
    - Ensure ReadOnly is set to true in ICMAPI section.
    - Ensure Enabled is set to true (if you prefer to use ICM API Client).
  - In the ICMWorkflows section:
    - Add the UserToken (taken from opening ICM automation in the browser)
    - Ensure ReadOnly is set to true in ICMWorkflows section.
    - Ensure Enabled is set to true.
  - Set the Kusto:AuthenticationType setting to "User".

Once done, simply run the project in Visual Studio or using dotnet run on the command line.

Important Note: If you do not want to alter the ICM incidents during local development/testing, ensure you've set the ICMAPI:ReadOnly and ICMWorkflows:ReadOnly to true in appsettings.Development.json.

Run the project [FirstPartyAgent.Web](/src/Agent/FirstPartyAgent.Web)

---

## Agent Deployment and Graduation Process

- For Deployment - Contact us (saziz, ajsharm, nmallick, shgup)
- Review [SRE Agent for Incident Management](/docs/FirstPartyAgent/SREAgentForICM.md) for a quick glance into the various stages of graduating the Agent to automatically handle Incidents.

---