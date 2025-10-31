import { getCloudEndpoints } from '../Auth/cloudConfig';
import { ApiVersions } from '../Constants/ApiVersions';
import { TelemetrySource } from '../Constants/Telemetry';
import { ARGRequestContent, ARGResponse } from '../Contracts/Arg';
import { ArmObj, ResponseArray } from '../Contracts/Arm';
import { Response } from '../Contracts/Response';
import { parseArmId } from '../Utilities/ArmId';
import { ArmClient } from './ArmClient';
import { Client } from './Client';

export interface ResourceGroup {
    readonly id: string;
    readonly location: string;
    readonly managedBy?: string;
    readonly name: string;
    readonly properties?: {
        readonly provisioningState: string;
    };
    readonly tags?: Record<string, string>;
    readonly type?: string;
}

export class ResourceGroupClient extends Client {
    private static _instance: ResourceGroupClient | null = null;
    private armClient: ArmClient;

    private constructor(telemetrySource: TelemetrySource) {
        super(telemetrySource);
        this.armClient = ArmClient.getInstance(telemetrySource);
    }

    public static getInstance(telemetrySource: TelemetrySource): ResourceGroupClient {
        if (!ResourceGroupClient._instance) {
            ResourceGroupClient._instance = new ResourceGroupClient(telemetrySource);
        }
        return ResourceGroupClient._instance;
    }

    /**
     * Execute an Azure Resource Graph (ARG) query
     */
    private async executeArg(
        content: ARGRequestContent,
        commandName: string,
        apiVersion = ApiVersions.argQueryApiVersion20240401
    ): Promise<ARGResponse[]> {
        const tokenResponse = await this.getAccessToken('arm');

        if (!tokenResponse.isSuccessful) {
            throw new Error('Failed to acquire ARM token for ARG query');
        }

        const endpoints = getCloudEndpoints();
        const argUrl = `${endpoints.arm}/providers/Microsoft.ResourceGraph/resources?api-version=${apiVersion}`;

        const response = await fetch(argUrl, {
            method: 'POST',
            headers: {
                Authorization: `Bearer ${tokenResponse.content}`,
                'Content-Type': 'application/json',
                'x-ms-command-name': commandName,
            },
            body: JSON.stringify(content),
        });

        if (!response.ok) {
            throw new Error(`ARG query failed: ${response.status} ${response.statusText}`);
        }

        const data: ARGResponse = await response.json();
        return [data]; // Return as array to match old executeArg behavior
    }

    private extractResourceGroupNamesAndSubscriptionIds(resourceGroupIds: string[]): {
        resourceGroupNames: string[];
        subscriptionIds: string[];
    } {
        const resourceGroupNames: string[] = [];
        const subscriptionIds: string[] = [];

        resourceGroupIds.forEach(resourceGroupId => {
            const armId = parseArmId(resourceGroupId);
            const subscriptionId = armId.subscription;
            const resourceGroupName = armId.resourceGroup;

            if (subscriptionId && resourceGroupName) {
                resourceGroupNames.push(resourceGroupName);
                if (!subscriptionIds.includes(subscriptionId)) {
                    subscriptionIds.push(subscriptionId);
                }
            }
        });

        return { resourceGroupNames, subscriptionIds };
    }

    public async createResourceGroup(
        subscriptionId: string,
        resourceGroupName: string,
        location: string,
        tags = {},
        apiVersion = ApiVersions.resourceGroupApiVersion20200601
    ): Promise<Response<ArmObj<ResourceGroup>>> {
        return this.armClient.makeArmCall<ArmObj<ResourceGroup>>({
            method: 'PUT',
            resourceId: `/subscriptions/${subscriptionId}/resourceGroups/${resourceGroupName}`,
            body: {
                location,
                tags,
            } as any,
            apiVersion,
            commandName: 'createResourceGroup',
        });
    }

    public async updateResourceGroup(
        resourceId: string,
        updatedProps: any,
        apiVersion = ApiVersions.resourceGroupApiVersion20200601
    ): Promise<Response<ArmObj<ResourceGroup>>> {
        return this.armClient.makeArmCall<ArmObj<ResourceGroup>>({
            method: 'PATCH',
            resourceId,
            body: updatedProps,
            apiVersion,
            commandName: 'updateResourceGroup',
        });
    }

    public async getResourceGroup(
        subscriptionId: string,
        resourceGroupName: string,
        apiVersion = ApiVersions.resourceGroupApiVersion20200601
    ): Promise<Response<ArmObj<ResourceGroup>>> {
        return this.armClient.makeArmCall<ArmObj<ResourceGroup>>({
            method: 'GET',
            resourceId: `/subscriptions/${subscriptionId}/resourceGroups/${resourceGroupName}`,
            apiVersion,
            commandName: 'getResourceGroup',
        });
    }

    public async deleteResourceGroup(
        subscriptionId: string,
        resourceGroupName: string,
        apiVersion = ApiVersions.resourceGroupApiVersion20200601
    ): Promise<Response<void>> {
        return this.armClient.makeArmCall<void>({
            method: 'DELETE',
            resourceId: `/subscriptions/${subscriptionId}/resourceGroups/${resourceGroupName}`,
            apiVersion,
            commandName: 'deleteResourceGroup',
        });
    }

    public async getResourceGroups(
        subscriptionId: string,
        apiVersion = ApiVersions.resourceGroupApiVersion20200601
    ): Promise<Response<ResourceGroup[]>> {
        const response = await this.armClient.makeArmCall<ResponseArray<ResourceGroup>>({
            method: 'GET',
            resourceId: `/subscriptions/${subscriptionId}/resourceGroups`,
            apiVersion,
            commandName: 'getResourceGroups',
        });

        if (response.isSuccessful && response.content) {
            return {
                isSuccessful: true,
                content: response.content.value,
            };
        }

        return {
            isSuccessful: false,
            error: response.error,
        };
    }

    public async getResourcesInSubscriptionsByResourceGroup(
        subscriptions: string[],
        resourceGroupName: string,
        showProperties = false
    ): Promise<ARGResponse[]> {
        const content: ARGRequestContent = {
            query: `where resourceGroup =~ '${resourceGroupName}' | project id, type, kind, location, sku${
                showProperties ? ', identity, properties, tags' : ''
            }`,
            subscriptions,
        };

        return this.executeArg(content, 'getResourcesUsingTypeAndKind');
    }

    public async getResourcesInSubscriptionsByTypeAndKind(subscriptions: string[], type: string, kind?: string): Promise<ARGResponse[]> {
        const content: ARGRequestContent = {
            query: `where type =~ '${type}'${kind ? ` and kind has '${kind}'` : ''} | project id, name, sku`,
            subscriptions,
        };

        return this.executeArg(content, 'getResourcesUsingTypeAndKind');
    }

    public async getResourcesInResourceGroup(resourceGroupId: string, apiVersion: string): Promise<Response<ResponseArray<any>>> {
        return this.armClient.makeArmCall<ResponseArray<any>>({
            method: 'GET',
            resourceId: `${resourceGroupId}/resources`,
            apiVersion,
            commandName: 'getResourcesInResourceGroup',
        });
    }

    public async getAllResourceGroupsFromSubscriptions(subscriptionIds: string[]): Promise<Response<ResourceGroup[]>> {
        if (subscriptionIds.length === 0) {
            return {
                isSuccessful: true,
                content: [],
            };
        }

        const cleanedSubscriptionIds = subscriptionIds.filter(str => str !== '');
        const query = `
            resourcecontainers
            | where type == "microsoft.resources/subscriptions/resourcegroups"
            | project id, name, type, location, subscriptionId, properties, tags, managedBy
        `;

        const content: ARGRequestContent = {
            query,
            subscriptions: cleanedSubscriptionIds,
        };

        try {
            const response = await this.executeArg(content, 'getAllResourceGroupsFromSubscriptions');
            const resourceGroups: ResourceGroup[] = [];

            if (response && response.length > 0) {
                // TODO: Add proper type - may differ because againt resourcecontainers table ?? or cause I'm using new API version?
                response.forEach((argResponse: any) => {
                    argResponse.data.forEach((item: any) => {
                        resourceGroups.push({
                            id: item.id,
                            name: item.name,
                            type: item.type,
                            location: item.location,
                            properties: item.properties,
                            tags: item.tags,
                            managedBy: item.managedBy,
                        });
                    });
                });
            }

            return {
                isSuccessful: true,
                content: resourceGroups,
            };
        } catch (error) {
            return {
                isSuccessful: false,
                error: error instanceof Error ? error : new Error(String(error)),
            };
        }
    }

    public async getResourceGroupsInSubscriptionWithSreAgentKinds(subscriptionIds: string[]): Promise<Set<string>> {
        const cleanedSubscriptionIds = subscriptionIds.filter(str => str !== '');
        const query = `
            where type in~ ('microsoft.web/sites', 'microsoft.app/containerapps', 'microsoft.compute/virtualmachines', 'microsoft.containerservice/managedclusters', 'microsoft.cache/redis', 'microsoft.dbforpostgresql/flexibleservers', 'microsoft.dbforpostgresql/servers', 'microsoft.documentdb/databaseaccounts', 'microsoft.sql/servers', 'microsoft.sql/servers/databases', 'microsoft.storage/storageaccounts')
            | summarize by resourceGroup
          `;
        const content: ARGRequestContent = {
            query,
            subscriptions: cleanedSubscriptionIds,
        };

        const response = await this.executeArg(content, 'getSreAgentFilteredResourceGroups');
        const resourceGroupsWithApps = new Set<string>();
        if (response && response.length > 0) {
            response.forEach((argResponse: any) => {
                argResponse.data.forEach((item: any) => {
                    resourceGroupsWithApps.add(item.resourceGroup);
                });
            });
        }
        return resourceGroupsWithApps;
    }

    public async listResourceKindsInResourceGroups(resourceGroupIds: string[]): Promise<Record<string, string[]>> {
        const { resourceGroupNames, subscriptionIds } = this.extractResourceGroupNamesAndSubscriptionIds(resourceGroupIds);

        const resourceGroupNamesLower = resourceGroupNames.map(name => `'${name.toLowerCase()}'`).join(', ');
        const query = `
            where type != ""
            | where tolower(resourceGroup) in~ (${resourceGroupNamesLower})
            | project type, resourceGroupId = strcat('/subscriptions/', subscriptionId, '/resourceGroups/', resourceGroup)
            | summarize by resourceGroupId, type
          `;

        const content: ARGRequestContent = {
            query,
            subscriptions: subscriptionIds,
        };

        const response = await this.executeArg(content, 'listResourceKindsInResourceGroups');
        const results: Record<string, string[]> = {};
        if (response && response.length > 0) {
            response.forEach((argResponse: ARGResponse) => {
                argResponse.data.rows.forEach((row: any[]) => {
                    const resourceGroupId = row[0];
                    const type = row[1];

                    if (!results[resourceGroupId]) {
                        results[resourceGroupId] = [];
                    }
                    results[resourceGroupId].push(type);
                });
            });
        }
        return results;
    }

    public async listResourceTypeAndKindsInResourceGroups(resourceGroupIds: string[]): Promise<string[]> {
        const { resourceGroupNames, subscriptionIds } = this.extractResourceGroupNamesAndSubscriptionIds(resourceGroupIds);

        const resourceGroupNamesLower = resourceGroupNames.map(name => `'${name.toLowerCase()}'`).join(', ');
        const query = `
            where type != ""
            | where tolower(resourceGroup) in~ (${resourceGroupNamesLower})
            | summarize by type, kind
          `;

        const content: ARGRequestContent = {
            query,
            subscriptions: subscriptionIds,
        };

        const response = await this.executeArg(content, 'listResourceKindsInResourceGroups');
        const results: Record<string, string> = {};
        if (response && response.length > 0) {
            response.forEach((argResponse: ARGResponse) => {
                argResponse.data.rows.forEach((row: any[]) => {
                    let type = row[0];
                    const kind = row[1];

                    if (type === 'microsoft.web/sites' && kind === 'functionapp') {
                        type = 'microsoft.web/functionapp';
                    }

                    if (!results[type]) {
                        results[type] = '';
                    }
                    results[type] = type;
                });
            });
        }
        return Object.keys(results);
    }

    public async listAllResourcesInResourceGroups(resourceGroupIds: string[]): Promise<string[]> {
        const { resourceGroupNames, subscriptionIds } = this.extractResourceGroupNamesAndSubscriptionIds(resourceGroupIds);

        const resourceGroupNamesLower = resourceGroupNames.map(name => `'${name.toLowerCase()}'`).join(', ');
        const query = `
            where tolower(resourceGroup) in~ (${resourceGroupNamesLower})
            | summarize by type
          `;
        const content: ARGRequestContent = {
            query,
            subscriptions: subscriptionIds,
        };

        const response = await this.executeArg(content, 'listResourceKindsInResourceGroups');
        const resourceTypes: string[] = [];
        if (response && response.length > 0) {
            response.forEach((argResponse: ARGResponse) => {
                argResponse.data.rows.forEach((row: any[]) => {
                    resourceTypes.push(row[0]);
                });
            });
        }
        return resourceTypes;
    }
}
