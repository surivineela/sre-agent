## FAQs for Overcoming Blockers in Setting Up the Azure SRE Agent Locally

1. **How do I set the subscription for the deployment?**
   - You can pass `-s <subscriptionId>` to the deploy3p command: `just deploy3p -n <stamp_prefix> -s <subscriptionId>`.
   - Alternatively, use the command: `az account set --subscription "your-subscription-id"` before running the deployment. 

2. **What should I do if the deployment is stuck on feature registration?**
   - Run the command to check the registration status: `az feature show --name PrivatePreview --namespace Microsoft.DurableTask --output table`. 
   - Run the command for the respective feature registration that is failing.
   - If it is still registering, wait for it to complete. 

3. **How do I handle quota issues with OpenAI deployment?**
   - If you encounter quota issues, change the location for OpenAI in the `openai.bicep` file to a region where you have available quota, such as `francecentral`. 
   - Additionally, if you already have an SRE agent resource group deployed on your subscription that is *no longer being used*, you can delete it to free up your quota. (Delete the resource group, and then go `Azure OpenAI` -> `Manage deleted resources` -> `Purge` the OpenAI resource of the resource group you just deleted.) Afterwards, you should be able to deploy a new instance on your subscription.

4. **What should I do if I see an error related to the DurableTask feature?**
   - Ensure that the DurableTask feature is registered by running: `az provider register --namespace Microsoft.DurableTask`. 

5. **How do I resolve the error "The subscription doesn't exist in cloud 'AzureCloud'"?**
   - Verify that you are using the correct subscription ID and that it is available in the AzureCloud. 

6. **What if the deployment fails with an error related to the Alerts Management namespace?**
   - Register the Alerts Management provider using: `az provider register --namespace Microsoft.AlertsManagement`. 

7. **How do I run the deployment script again if it fails?**
   - Simply rerun the `deploy3p` script. It will attempt to redeploy only the failed components. 

8. **What should I do if the deployment script is not progressing?**
   - Ensure that you have the latest version of the Azure CLI by running: `az upgrade`. 

9. **How do I set up the environment prefix in the app settings?**
   - Update the `appsettings.json` file in the `Agent.Web` project with your deployment alias in the `EnvPrefix` field. 

10. **What if I encounter an error related to the TaskHub in the connection string?**
    - Ensure that the connection string includes `TaskHub=taskhub1`. 

11. **How do I monitor the deployment status?**
    - You can check the deployment status using the link provided in the console output, which typically looks like: https://ms.portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/<SUBSCRIPTION_ID>/resourceGroups/<STAMP NAME>-operations-agent-3p-rg/deployments. 

12. **What should I do if the deployment fails due to a bad request related to the database account name?**
    - Ensure that the Stamp name you're using with deploy3p command does not contain any invalid characters, such as uppercase letters. 

These guidelines should help you overcome common blockers and issues when setting up the Azure SRE Agent locally.
