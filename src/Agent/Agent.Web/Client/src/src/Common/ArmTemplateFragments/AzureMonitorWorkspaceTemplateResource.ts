import { ApiVersions } from '../ApiVersions';
import { ArmTemplateBuilder } from '../ArmTemplateBuilder/ArmTemplateBuilder';
import { ArmTemplateResource } from '../ArmTemplateBuilder/ArmTemplateResource';
import {
    ArmServiceType,
    ArmTemplateParameter,
    ArmTemplateParameterName,
    ArmTemplateResourceFragment,
    AzureMonitorWorkspaceParameterName as ParamName,
} from '../ArmTemplateBuilder/ArmTemplateTypes';

export interface AzureMonitorWorkspaceTemplateResourceOptions {
    namePrefix: string;
}

export class AzureMonitorWorkspaceTemplateResource extends ArmTemplateResource<{}> {
    get type(): string {
        return ArmServiceType.AzureMonitorWorkspace;
    }

    parameters: Record<string, ArmTemplateParameter> = {
        [ParamName.WorkspaceName]: {
            type: 'string',
            defaultValue: "[format('{0}-amw', parameters('namePrefix'))]",
        },
    };

    constructor(builder: ArmTemplateBuilder) {
        super(builder);
    }

    addResourceToBuilder(): void {
        this._builder.resources.push(this);
    }

    _getTemplateFragmentHelper(): ArmTemplateResourceFragment<{}> {
        return {
            apiVersion: ApiVersions.azureMonitorWorkspace20230403,
            name: `[parameters('${ParamName.WorkspaceName}')]`,
            type: ArmServiceType.AzureMonitorWorkspace,
            location: `[parameters('${ArmTemplateParameterName.Location}')]`,
            properties: {},
        };
    }
}
