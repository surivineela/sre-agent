import { ApiVersions } from '../../../Constants/ApiVersions';
import { ArmTemplateBuilder } from '../ArmTemplateBuilder';
import { ArmTemplateResource } from '../ArmTemplateResource';
import { ArmServiceType, ArmTemplateParameter, ArmTemplateResourceFragment, SreAgentParameterName } from '../ArmTemplateTypes';

interface UserIdentityResourceOptions {
    location: string;
    id?: string;
}

/**
 * ARM template resource for creating a User Assigned Managed Identity
 */
export class UserIdentityTemplateResource extends ArmTemplateResource<object> {
    id: string;

    get type() {
        return ArmServiceType.OidcUserIdentity + this.id;
    }

    parameters: Record<string, ArmTemplateParameter> = {
        [SreAgentParameterName.OidcUserIdentityName]: {
            type: 'string',
            defaultValue: `[concat(substring(parameters('${SreAgentParameterName.AgentName}'), 0, min(length(parameters('${SreAgentParameterName.AgentName}')), sub(128, 33))), '-', uniqueString(resourceGroup().id, deployment().name))]`,
        },
    };

    constructor(
        builder: ArmTemplateBuilder,
        private _options: UserIdentityResourceOptions
    ) {
        super(builder);
        this.id = this._options.id ?? '';
        const { location } = this._options;

        if (!location) {
            throw Error('No supported location for creating a user identity');
        }
    }

    addResourceToBuilder(): void {
        this._builder.resources.push(this);
    }

    _getTemplateFragmentHelper(): ArmTemplateResourceFragment<object> {
        const { location } = this._options;
        const oidcUserIdentityName = SreAgentParameterName.OidcUserIdentityName;

        return {
            apiVersion: ApiVersions.identityApiVersion20241130,
            name: `[parameters('${oidcUserIdentityName}')]`,
            type: ArmServiceType.UserIdentity,
            location: location,
            dependsOn: this.dependsOn,
            properties: {
                isolationScope: 'Regional',
            },
        };
    }
}
