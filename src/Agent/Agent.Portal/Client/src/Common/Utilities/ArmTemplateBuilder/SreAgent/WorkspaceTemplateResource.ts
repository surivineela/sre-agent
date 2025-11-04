import { ApiVersions } from '../../../Constants/ApiVersions';
import { ArmTemplateBuilder } from '../ArmTemplateBuilder';
import { ArmTemplateResource } from '../ArmTemplateResource';
import { ArmServiceType, ArmTemplateParameter, ArmTemplateResourceFragment, SreAgentParameterName } from '../ArmTemplateTypes';

interface WorkspaceFragment {
    sku: any;
    retentionInDays: number;
    workspaceCapping: any;
}

/**
 * ARM template resource for creating a Log Analytics Workspace
 */
export class WorkspaceTemplateResource extends ArmTemplateResource<WorkspaceFragment> {
    get type() {
        return ArmServiceType.Workspace;
    }

    parameters: Record<string, ArmTemplateParameter> = {
        [SreAgentParameterName.WorkspaceName]: {
            type: 'string',
            defaultValue: "[format('workspace{0}', uniqueString(resourceGroup().id, deployment().name))]",
        },
        [SreAgentParameterName.WorkspaceSku]: {
            type: 'object',
            defaultValue: {
                name: 'PerGB2018',
            },
        },
    };

    constructor(
        builder: ArmTemplateBuilder,
        private _location: string
    ) {
        super(builder);

        if (!this._location) {
            throw Error('No supported location for creating a workspace');
        }
    }

    addResourceToBuilder(): void {
        this._builder.resources.push(this);
    }

    _getTemplateFragmentHelper(): ArmTemplateResourceFragment<WorkspaceFragment> {
        return {
            apiVersion: ApiVersions.workspacesApiVersion20250201,
            name: `[parameters('${SreAgentParameterName.WorkspaceName}')]`,
            type: ArmServiceType.Workspace,
            location: this._location,
            dependsOn: this.dependsOn,
            properties: {
                sku: `[parameters('${SreAgentParameterName.WorkspaceSku}')]`,
                retentionInDays: 30,
                workspaceCapping: {},
            },
        };
    }
}
