## Configuring FirstPartyAgent.Web to run locally

- Copy appsettings.json into appsettings.Development.json.
- Fill your appsettings.Development.json as below:
  - [**Required**]: Fill up the Azure:OpenAI section using your Azure OpenAI credentials.
  - [**Required**]: Fill the ICMAPI section using your ICM user token (taken from opening [ICM portal](https://portal.microsofticm.com/) in the browser).
  - In the ICMWorkflows section
    - [**Required**]: Either set the UserToken (taken from opening [ICM automation](https://portal.microsofticm.com/imp/v5/automation/workflows) in the browser)
    - [Optional]: Or set the UseFunctionApp to true with providing the FunctionAppUrl and FunctionAppKey taken from [this Keyvault](https://ms.portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/5abde51d-cc72-4bcc-b0d7-3c86b4db2a7c/resourceGroups/appservices-sre-demo/providers/Microsoft.KeyVault/vaults/devkeyvault-ajsharm/secrets)
  - For using the Kusto Plugin, set the Kusto:AuthenticationType setting to "User" in appsettings.Development.json.
Once done, simply run the project in Visual Studio or using dotnet run on the command line.

**Important Note**: If you do not want to alter the ICM incidents during local development/testing, set the ICMAPI:ReadOnly and ICMWorkflows:ReadOnly to true in appsettings.Development.json.