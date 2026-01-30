import { ArmObj } from '../Contracts/Azure/ArmObj';
import { Agent, AgentAccessLevel, IncidentManagementType } from '../Contracts/Azure/SreAgent';
import { ArmResourceDescriptor } from '../Helpers/ResourceDescriptors';

export type KnowledgeSourceType = 'repository' | 'file' | 'webpage';

export interface KnowledgeSource {
    id: string;
    type: KnowledgeSourceType;
    name: string;
    url?: string;
    lastModified?: string;
}

export interface AgentFormValues {
    selectedSubscriptionIds: string[];
    selectedResourceGroupIds: string[];
    resourceGroupLocations: Record<string, string>;
    incidentPlatformType: IncidentManagementType | undefined;
    pagerDutyApiKey: string;
    serviceNowEndpoint: string;
    serviceNowUsername: string;
    serviceNowPassword: string;
    permissionsLevel: AgentAccessLevel;
    knowledgeSources: KnowledgeSource[];
}

export const getAgentFormInitialValues = (agentObj: ArmObj<Agent> | undefined, resourceId: string): AgentFormValues => {
    const agent = agentObj?.properties;
    const existingManagedResources = agent?.knowledgeGraphConfiguration?.managedResources ?? [];
    const existingIncidentConfig = agent?.incidentManagementConfiguration;
    const existingAccessLevel = agent?.actionConfiguration?.accessLevel ?? AgentAccessLevel.low;

    const existingSubscriptionIds = existingManagedResources
        .filter((r: string) => !r.includes('/resourceGroups/'))
        .map((r: string) => {
            const match = r.match(/\/subscriptions\/([^/]+)/i);
            return match ? match[1] : '';
        })
        .filter((id: string) => id.length > 0);

    const existingResourceGroupIds = existingManagedResources.filter((r: string) => r.includes('/resourceGroups/'));

    const descriptor = new ArmResourceDescriptor(resourceId);
    const currentSubscriptionId = descriptor.subscription;

    return {
        selectedSubscriptionIds: existingSubscriptionIds.length > 0 ? existingSubscriptionIds : [currentSubscriptionId],
        selectedResourceGroupIds: existingResourceGroupIds,
        resourceGroupLocations: {},
        incidentPlatformType: existingIncidentConfig?.type,
        pagerDutyApiKey: '',
        serviceNowEndpoint: existingIncidentConfig?.connectionUrl ?? '',
        serviceNowUsername: '',
        serviceNowPassword: '',
        permissionsLevel: existingAccessLevel,
        knowledgeSources: [],
    };
};
