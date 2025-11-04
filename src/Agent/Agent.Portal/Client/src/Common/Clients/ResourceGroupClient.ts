import { getCloudEndpoints } from '../Auth/cloudConfig';
import { ApiVersions } from '../Constants/ApiVersions';
import { TelemetrySource } from '../Constants/Telemetry';
import { ARGRequestContent, ARGResponseObjectArray } from '../Contracts/Arg';
import { ArmObj, ResponseArray } from '../Contracts/Arm';
import { Response } from '../Contracts/Response';
import { parseArmId } from '../Utilities/ArmId';
import { ArmClient } from './ArmClient';
import { Client } from './Client';
import { tokenCache } from './TokenCache';

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
     * Note: Uses default 'objectArray' format for simpler, more readable code
     * Returns Response<T[]> format consistent with ARM calls
     */
    private async executeArg<T = any>(
        content: ARGRequestContent,
        commandName: string,
        apiVersion = ApiVersions.argQueryApiVersion20240401
    ): Promise<Response<T[]>> {
        try {
            const tokenResponse = await tokenCache.getAccessToken('arm');

            const endpoints = getCloudEndpoints();
            const argUrl = `${endpoints.arm}/providers/Microsoft.ResourceGraph/resources?api-version=${apiVersion}`;

            // Use default objectArray format (simpler to work with than table format)
            // Can be overridden via content.options.resultFormat if needed
            const requestContent: ARGRequestContent = {
                ...content,
                options: {
                    ...content.options,
                    resultFormat: content.options?.resultFormat ?? 'objectArray',
                },
            };

            const response = await fetch(argUrl, {
                method: 'POST',
                headers: {
                    Authorization: `Bearer ${tokenResponse.raw}`,
                    'Content-Type': 'application/json',
                    'x-ms-command-name': commandName,
                },
                body: JSON.stringify(requestContent),
            });

            if (!response.ok) {
                const errorText = await response.text();
                return {
                    isSuccessful: false,
                    error: new Error(`ARG query failed: ${response.status} ${response.statusText}. ${errorText}`),
                };
            }

            const data = (await response.json()) as ARGResponseObjectArray<T>;

            return {
                isSuccessful: true,
                content: data.data,
            };
        } catch (error) {
            return {
                isSuccessful: false,
                error: error instanceof Error ? error : new Error(String(error)),
            };
        }
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
        apiVersion = ApiVersions.armApiVersion20250301
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
        apiVersion = ApiVersions.armApiVersion20250301
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
        apiVersion = ApiVersions.armApiVersion20250301
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
        apiVersion = ApiVersions.armApiVersion20250301
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
        apiVersion = ApiVersions.armApiVersion20250301
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
    ): Promise<Response<any[]>> {
        const content: ARGRequestContent = {
            query: `where resourceGroup =~ '${resourceGroupName}' | project id, type, kind, location, sku${
                showProperties ? ', identity, properties, tags' : ''
            }`,
            subscriptions,
        };

        return this.executeArg(content, 'getResourcesUsingTypeAndKind');
    }

    public async getResourcesInSubscriptionsByTypeAndKind(subscriptions: string[], type: string, kind?: string): Promise<Response<any[]>> {
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

    public async getAllResourceGroupsFromSubscriptions(subscriptionIds: string[], searchText?: string): Promise<Response<ResourceGroup[]>> {
        if (subscriptionIds.length === 0) {
            return {
                isSuccessful: true,
                content: [],
            };
        }

        const cleanedSubscriptionIds = subscriptionIds.filter(str => str !== '');

        // Build WHERE clause with search filter
        const whereConditions = ['type == "microsoft.resources/subscriptions/resourcegroups"'];
        if (searchText && searchText.trim()) {
            whereConditions.push(`name contains "${searchText.trim()}"`);
        }
        const whereClause = whereConditions.join(' and ');

        const query = `
            resourcecontainers
            | where ${whereClause}
            | project id, name, type, location, subscriptionId, properties, tags, managedBy
            | take 100
        `;

        const content: ARGRequestContent = {
            query,
            subscriptions: cleanedSubscriptionIds,
        };

        const response = await this.executeArg<ResourceGroup>(content, 'getAllResourceGroupsFromSubscriptions');

        if (!response.isSuccessful) {
            return response;
        }

        const resourceGroups: ResourceGroup[] =
            response.content?.map(item => ({
                id: item.id,
                name: item.name,
                type: item.type,
                location: item.location,
                properties: item.properties,
                tags: item.tags,
                managedBy: item.managedBy,
            })) ?? [];

        return {
            isSuccessful: true,
            content: resourceGroups,
        };
    }

    public async getResourceGroupsInSubscriptionWithSreAgentKinds(subscriptionIds: string[]): Promise<Response<Set<string>>> {
        const cleanedSubscriptionIds = subscriptionIds.filter(str => str !== '');
        const query = `
            where type in~ ('microsoft.web/sites', 'microsoft.app/containerapps', 'microsoft.compute/virtualmachines', 'microsoft.containerservice/managedclusters', 'microsoft.cache/redis', 'microsoft.dbforpostgresql/flexibleservers', 'microsoft.dbforpostgresql/servers', 'microsoft.documentdb/databaseaccounts', 'microsoft.sql/servers', 'microsoft.sql/servers/databases', 'microsoft.storage/storageaccounts')
            | summarize by resourceGroup
          `;
        const content: ARGRequestContent = {
            query,
            subscriptions: cleanedSubscriptionIds,
        };

        const response = await this.executeArg<{ resourceGroup: string }>(content, 'getSreAgentFilteredResourceGroups');

        if (!response.isSuccessful) {
            return {
                isSuccessful: false,
                error: response.error,
            };
        }

        const resourceGroupsWithApps = new Set<string>();
        response.content?.forEach(item => {
            resourceGroupsWithApps.add(item.resourceGroup);
        });

        return {
            isSuccessful: true,
            content: resourceGroupsWithApps,
        };
    }

    public async listResourceKindsInResourceGroups(resourceGroupIds: string[]): Promise<Response<Record<string, string[]>>> {
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

        const response = await this.executeArg<{ resourceGroupId: string; type: string }>(content, 'listResourceKindsInResourceGroups');

        if (!response.isSuccessful) {
            return {
                isSuccessful: false,
                error: response.error,
            };
        }

        const results: Record<string, string[]> = {};
        response.content?.forEach(item => {
            if (!results[item.resourceGroupId]) {
                results[item.resourceGroupId] = [];
            }
            results[item.resourceGroupId].push(item.type);
        });

        return {
            isSuccessful: true,
            content: results,
        };
    }

    public async listResourceTypeAndKindsInResourceGroups(resourceGroupIds: string[]): Promise<Response<string[]>> {
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

        const response = await this.executeArg<{ type: string; kind: string }>(content, 'listResourceKindsInResourceGroups');

        if (!response.isSuccessful) {
            return {
                isSuccessful: false,
                error: response.error,
            };
        }

        const results: Record<string, string> = {};
        response.content?.forEach(item => {
            let type = item.type;
            const kind = item.kind;

            if (type === 'microsoft.web/sites' && kind === 'functionapp') {
                type = 'microsoft.web/functionapp';
            }

            if (!results[type]) {
                results[type] = '';
            }
            results[type] = type;
        });

        return {
            isSuccessful: true,
            content: Object.keys(results),
        };
    }

    public async listAllResourcesInResourceGroups(resourceGroupIds: string[]): Promise<Response<string[]>> {
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

        const response = await this.executeArg<{ type: string }>(content, 'listResourceKindsInResourceGroups');

        if (!response.isSuccessful) {
            return {
                isSuccessful: false,
                error: response.error,
            };
        }

        const resourceTypes: string[] = response.content?.map(item => item.type) ?? [];

        return {
            isSuccessful: true,
            content: resourceTypes,
        };
    }
}
