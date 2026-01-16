import { ApiVersions } from '../../../Constants/ApiVersions';
import { GenevaActionsConfiguration } from '../../../Contracts/AgentSpace';
import { ArmTemplateBuilder } from '../ArmTemplateBuilder';
import { ArmTemplateResource } from '../ArmTemplateResource';
import {
    AgentSpaceParameterName,
    ArmServiceType,
    ArmTemplateParameter,
    ArmTemplateParameterName,
    ArmTemplateResourceFragment,
} from '../ArmTemplateTypes';

export interface AgentSpaceResourceOptions {
    /** Optional Geneva Actions configuration */
    genevaActionsConfiguration?: GenevaActionsConfiguration;
}

/**
 * ARM template resource for creating an Agent Space
 */
export class AgentSpaceTemplateResource extends ArmTemplateResource<object> {
    get type(): ArmServiceType {
        return ArmServiceType.AgentSpaces;
    }

    parameters: Record<string, ArmTemplateParameter> = {
        [AgentSpaceParameterName.AgentSpaceName]: { type: 'string' },
        [AgentSpaceParameterName.AgentSpaceDescription]: {
            type: 'string',
            defaultValue: '',
        },
        [AgentSpaceParameterName.AgentSpaceMaxCount]: {
            type: 'int',
            defaultValue: 10,
        },
    };

    constructor(
        builder: ArmTemplateBuilder,
        private _options: AgentSpaceResourceOptions
    ) {
        super(builder);
    }

    addResourceToBuilder(): void {
        this._builder.resources.push(this);
    }

    _getTemplateFragmentHelper(): ArmTemplateResourceFragment<object> {
        const properties: Record<string, unknown> = {
            description: `[parameters('${AgentSpaceParameterName.AgentSpaceDescription}')]`,
            maxAgentCount: `[parameters('${AgentSpaceParameterName.AgentSpaceMaxCount}')]`,
        };

        // Add Geneva Actions policies if configured
        if (this._options.genevaActionsConfiguration) {
            properties.policies = {
                genevaActionsConfiguration: this._options.genevaActionsConfiguration,
            };
        }

        return {
            apiVersion: ApiVersions.microsoftAppApiVersion20250501Preview,
            name: `[parameters('${AgentSpaceParameterName.AgentSpaceName}')]`,
            type: ArmServiceType.AgentSpaces,
            location: `[parameters('${ArmTemplateParameterName.Location}')]`,
            dependsOn: this.dependsOn,
            properties,
            identity: {
                type: 'SystemAssigned',
            },
        };
    }
}
