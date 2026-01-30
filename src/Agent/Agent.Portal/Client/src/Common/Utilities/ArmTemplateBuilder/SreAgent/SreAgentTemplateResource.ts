import { ApiVersions } from '../../../Constants/ApiVersions';
import { AgentAccessLevel, AgentMode } from '../../../Contracts/SreAgent';
import { ArmTemplateBuilder } from '../ArmTemplateBuilder';
import { ArmTemplateResource } from '../ArmTemplateResource';
import {
    AppInsightsParameterName,
    ArmServiceType,
    ArmTemplateParameter,
    ArmTemplateParameterName,
    ArmTemplateResourceFragment,
    SreAgentParameterName,
} from '../ArmTemplateTypes';
import { SreAgentDependencyResolver } from './DependencyResolvers/SreAgentDependencyResolver';

interface SreAgentResourceOptions {
    managedResourceIds: string[];
    managedResourceNames: string[];
    mode: AgentMode;
    deploymentGuid: string;
    agentSpaceId?: string;
    createNewAppInsights: boolean;
    defaultModelProvider?: string;
}

/**
 * ARM template resource for creating an SRE Agent
 */
export class SreAgentTemplateResource extends ArmTemplateResource<object> {
    get type() {
        return ArmServiceType.Agents;
    }

    parameters: Record<string, ArmTemplateParameter> = {
        [SreAgentParameterName.AgentName]: {
            type: 'string',
            defaultValue: "[format('sreagent-{0}', uniqueString(resourceGroup().id, deployment().name))]",
        },
        [SreAgentParameterName.AccessLevel]: {
            type: 'string',
            defaultValue: AgentAccessLevel.low,
        },
        [AppInsightsParameterName.AppInsightsResourceId]: {
            type: 'string',
        },
    };

    constructor(
        builder: ArmTemplateBuilder,
        private _options: SreAgentResourceOptions
    ) {
        super(builder);

        if (this._options.defaultModelProvider) {
            this.parameters[SreAgentParameterName.DefaultModelProvider] = {
                type: 'string',
            };
        }

        const dependencyArray = this._options.managedResourceNames.map(resource => {
            const safeName = resource.length > 34 ? resource.substring(0, 34) : resource;
            return `[concat('Microsoft.Resources/deployments/', '${safeName}', '-roleAssignments-', '${this._options.deploymentGuid}')]`;
        });

        this.dependencyResolvers = [
            new SreAgentDependencyResolver(this, {
                dependencyArray,
                createNewAppInsights: this._options.createNewAppInsights,
            }),
        ];
    }

    addResourceToBuilder(): void {
        this._builder.resources.push(this);
    }

    _getTemplateFragmentHelper(): ArmTemplateResourceFragment<object> {
        const location = ArmTemplateParameterName.Location;

        return {
            apiVersion: ApiVersions.microsoftAppApiVersion20250501Preview,
            name: `[parameters('${SreAgentParameterName.AgentName}')]`,
            type: ArmServiceType.Agents,
            location: `[parameters('${location}')]`,
            dependsOn: this.dependsOn,
            properties: {
                ...(this._options.agentSpaceId ? { agentSpaceId: this._options.agentSpaceId } : {}),
                knowledgeGraphConfiguration: {
                    managedResources: this._options.managedResourceIds,
                    identity: `[resourceId('Microsoft.ManagedIdentity/userAssignedIdentities', parameters('${SreAgentParameterName.OidcUserIdentityName}'))]`,
                },
                actionConfiguration: {
                    mode: this._options.mode,
                    identity: `[resourceId('Microsoft.ManagedIdentity/userAssignedIdentities', parameters('${SreAgentParameterName.OidcUserIdentityName}'))]`,
                    accessLevel: `[parameters('${SreAgentParameterName.AccessLevel}')]`,
                },
                mcpServers: [],
                logConfiguration: {
                    applicationInsightsConfiguration: {
                        appId: `[reference(parameters('${AppInsightsParameterName.AppInsightsResourceId}'), '${ApiVersions.appInsightsApiVersion20200202}').AppId]`,
                        connectionString: `[reference(parameters('${AppInsightsParameterName.AppInsightsResourceId}'), '${ApiVersions.appInsightsApiVersion20200202}').ConnectionString]`,
                        applicationInsightsResourceId: `[parameters('${AppInsightsParameterName.AppInsightsResourceId}')]`,
                    },
                },
                ...(this._options.defaultModelProvider
                    ? {
                          defaultModel: {
                              provider: `[parameters('${SreAgentParameterName.DefaultModelProvider}')]`,
                              name: "Automatic",
                          },
                      }
                    : {}),
            },
            identity: {
                type: 'SystemAssigned, UserAssigned',
                userAssignedIdentities: {
                    "[resourceId('Microsoft.ManagedIdentity/userAssignedIdentities', parameters('oidcUserIdentity'))]": {},
                },
            },
        };
    }
}
