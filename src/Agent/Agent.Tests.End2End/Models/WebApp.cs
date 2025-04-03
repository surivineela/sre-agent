// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager.AppService.Models;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager;
using Azure;
using Xunit.Abstractions;
using Agent.Core.Configuration;

namespace E2ETests.Models
{
    internal class WebApp
    {
        private readonly IMessageSink _output;

        TestSettings _testSettings { get; }
        SubscriptionResource subscription;

        public WebApp(TestSettings testSettings, IMessageSink output)
        {
            _output = output;
            _testSettings = testSettings;

            ArmClient client = new ArmClient(new DefaultAzureCredential());
            subscription = client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{testSettings.SubscriptionId}"));

        }

        public async Task<WebSiteResource> EnsureWebAppExists()
        {
            var rg = await EnsureResourceGroupExists();
            var asp = await EnsureAppServicePlanExists(rg);
            var webApp = await EnsureWebAppExists(rg, asp);
            await EnsureBasicAuthEnabled(webApp);

            return webApp;
        }

        public async Task EnsureWebAppDeleted()
        {
            if (_testSettings.SkipResourceCleanupAfterTestRun)
            {
                _output.WriteLine("Skipping cleanup");
                return;
            }

            await EnsureResourceGroupDeleted();
        }

        private async Task<ResourceGroupResource> EnsureResourceGroupExists()
        {
            var rg = await GetResourceGroup();
            return rg == null ? await CreateResourceGroup() : rg;
        }

        private async Task EnsureResourceGroupDeleted()
        {
            var rg = await GetResourceGroup();

            if (rg != null)
            {
                await DeleteResourceGroup(rg);
            }
        }

        private async Task<ResourceGroupResource> CreateResourceGroup()
        {
            _output.WriteLine("Creating resource group...");
            var createResourceGroupOperation = await subscription.GetResourceGroups().CreateOrUpdateAsync(WaitUntil.Completed, Consts.RgName, new ResourceGroupData(AzureLocation.WestUS));
            _output.WriteLine("Resource group created");
            return createResourceGroupOperation.Value;
        }

        private async Task<ResourceGroupResource?> GetResourceGroup()
        {
            var getResourceGroupOperation = await subscription.GetResourceGroups().GetIfExistsAsync(Consts.RgName);
            var resourceGroup = getResourceGroupOperation.HasValue ? getResourceGroupOperation.Value : null;

            _output.WriteLine(resourceGroup != null ? "Resource group exists" : "Resource group does not exist");

            return resourceGroup;
        }

        private async Task<AppServicePlanResource> EnsureAppServicePlanExists(ResourceGroupResource resourceGroup)
        {
            var asp = await GetIfExistsAsync(resourceGroup, (rg) => rg.GetAppServicePlanAsync(Consts.AppServicePlanName)); ;
            return asp == null ? await CreateAppServicePlan(resourceGroup) : asp;
        }

        private async Task<AppServicePlanResource> CreateAppServicePlan(ResourceGroupResource resourceGroup)
        {
            _output.WriteLine("Creating app service plan...");

            var appServicePlanData = new AppServicePlanData(new AzureLocation(AzureLocation.WestUS))
            {
                Sku = new AppServiceSkuDescription
                {
                    Name = "F1",
                    Tier = "Free",
                    Capacity = 1
                },
                Kind = "App",
            };
            var createAppServicePlanOperation = await resourceGroup.GetAppServicePlans().CreateOrUpdateAsync(WaitUntil.Completed, Consts.AppServicePlanName, appServicePlanData);

            _output.WriteLine("App service plan created");

            return createAppServicePlanOperation.Value;
        }

        private async Task<AppServicePlanResource?> GetAppServicePlan(ResourceGroupResource resourceGroup)
        {
            var appServicePlan = await GetIfExistsAsync(resourceGroup, (rg) => rg.GetAppServicePlanAsync(Consts.AppServicePlanName));

            _output.WriteLine(appServicePlan != null ? "App service plan group exists" : "App service plan does not exist");

            return appServicePlan;
        }

        private async Task<WebSiteResource> EnsureWebAppExists(ResourceGroupResource resourceGroup, AppServicePlanResource appServicePlan)
        {
            var webApp = await GetWebApp(resourceGroup);
            return webApp == null ? await CreateWebApp(resourceGroup, appServicePlan) : webApp;
        }

        private async Task<WebSiteResource?> GetWebApp(ResourceGroupResource resourceGroup)
        {
            var webApp = await GetIfExistsAsync(resourceGroup, (rg) => rg.GetWebSiteAsync(Helper.GetWebAppName(_testSettings.SubscriptionId)));

            _output.WriteLine(webApp != null ? "Web app exists" : "Web app does not exist");

            return webApp;
        }

        private async Task<WebSiteResource> CreateWebApp(ResourceGroupResource resourceGroup, AppServicePlanResource appServicePlan)
        {
            _output.WriteLine("Creating web app...");

            var siteData = new WebSiteData(AzureLocation.WestUS)
            {
                AppServicePlanId = appServicePlan.Id,
                SiteConfig = new SiteConfigProperties()
                {
                    PythonVersion = "3.9",
                },
            };

            var webappoperation = await resourceGroup.GetWebSites().CreateOrUpdateAsync(WaitUntil.Completed, Helper.GetWebAppName(_testSettings.SubscriptionId), siteData);
            var webapp = webappoperation.Value;

            _output.WriteLine("Web app created");

            return webapp;
        }

        private async Task EnsureBasicAuthEnabled(WebSiteResource webApp)
        {
            if (!await BasicAuthEnabled(webApp))
            {
                await EnableBasicAuth(webApp);
            }
        }

        private async Task<bool> BasicAuthEnabled(WebSiteResource webApp)
        {
            var getScmPublishingPolicyOperation = webApp.GetScmSiteBasicPublishingCredentialsPolicy().GetAsync();
            var getFtpPublishingPolicyOperation = webApp.GetWebSiteFtpPublishingCredentialsPolicy().GetAsync();
            var scmPublishingPolicy = (await getScmPublishingPolicyOperation).Value;
            var ftpPublishingPolicy = (await getFtpPublishingPolicyOperation).Value;
            bool ret = scmPublishingPolicy.Data.Allow.GetValueOrDefault() && ftpPublishingPolicy.Data.Allow.GetValueOrDefault();

            _output.WriteLine(ret ? "Basic auth was already enabled" : "Basic auth is not enabled");
            return ret;
        }

        private async Task EnableBasicAuth(WebSiteResource webApp)
        {
            _output.WriteLine("Enabling basic auth on web app...");

            var getScmPublishingPolicyOperation = webApp.GetScmSiteBasicPublishingCredentialsPolicy().GetAsync();
            var getFtpPublishingPolicyOperation = webApp.GetWebSiteFtpPublishingCredentialsPolicy().GetAsync();

            var scmPublishingPolicy = (await getScmPublishingPolicyOperation).Value;
            var ftpPublishingPolicy = (await getFtpPublishingPolicyOperation).Value;

            scmPublishingPolicy.Data.Allow = true;
            ftpPublishingPolicy.Data.Allow = true;

            var updateScmPolicyOperation = scmPublishingPolicy.CreateOrUpdateAsync(WaitUntil.Completed, scmPublishingPolicy.Data);
            var updateFtpPolicyOperation = ftpPublishingPolicy.CreateOrUpdateAsync(WaitUntil.Completed, ftpPublishingPolicy.Data);

            await Task.WhenAll(updateFtpPolicyOperation, updateScmPolicyOperation);

            _output.WriteLine("Basic auth enabled");
        }

        private async Task DeleteWebApp(WebSiteResource webApp)
        {
            _output.WriteLine("Deleting web app...");

            await webApp.DeleteAsync(WaitUntil.Completed);

            _output.WriteLine("Web app deleted");
        }

        private async Task DeleteResourceGroup(ResourceGroupResource resourceGroup)
        {
            _output.WriteLine("Deleting resource group...");
            await resourceGroup.DeleteAsync(WaitUntil.Completed);
            _output.WriteLine("Resource group deleted");
        }

        private async Task DeleteAppServicePlan(AppServicePlanResource appServicePlan)
        {
            _output.WriteLine("Deleting app service plan...");
            await appServicePlan.DeleteAsync(WaitUntil.Completed);
            _output.WriteLine("App service plan deleted");
        }

        public static async Task<TResource?> GetIfExistsAsync<TResource>(
            ResourceGroupResource resourceGroup,
            Func<ResourceGroupResource, Task<Response<TResource>>> getResourceFunc
        )
        where TResource : class
        {
            try
            {
                // Attempt to retrieve the resource using the provided function
                var resourceResponse = await getResourceFunc(resourceGroup);
                return resourceResponse.Value;
            }
            catch (RequestFailedException e) when (e.ErrorCode == "ResourceNotFound")
            {
                // Return null if the resource is not found
                return null;
            }
        }
    }
}

