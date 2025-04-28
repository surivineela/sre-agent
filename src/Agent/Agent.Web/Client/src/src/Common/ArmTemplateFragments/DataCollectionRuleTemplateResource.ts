import { ApiVersions } from '../ApiVersions';
import { ArmTemplateBuilder } from '../ArmTemplateBuilder/ArmTemplateBuilder';
import { ArmTemplateResource } from '../ArmTemplateBuilder/ArmTemplateResource';
import {
    ArmServiceType,
    ArmTemplateParameter,
    ArmTemplateParameterName,
    ArmTemplateResourceFragment,
    DataCollectionRuleParameterName as ParamName,
} from '../ArmTemplateBuilder/ArmTemplateTypes';

export interface DataCollectionRuleTemplateResourceOptions {
    namePrefix: string;
}

export class DataCollectionRuleTemplateResource extends ArmTemplateResource<{}> {
    get type(): string {
        return ArmServiceType.DataCollectionRule;
    }

    parameters: Record<string, ArmTemplateParameter> = {
        [ParamName.DataCollectionRuleName]: {
            type: 'string',
            defaultValue: "[format('{0}-dcr', parameters('namePrefix'))]",
        },
        [ParamName.AzureMonitorWorkspaceId]: {
            type: 'string',
            defaultValue:
                "[format('{0}/providers/Microsoft.OperationalInsights/workspaces/{1}', parameters('workspaceResourceId'), parameters('workspaceName'))]",
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
            apiVersion: ApiVersions.dataCollection20230311,
            name: `[parameters('${ParamName.DataCollectionRuleName}')]`,
            type: ArmServiceType.DataCollectionRule,
            location: `[parameters('${ArmTemplateParameterName.Location}')]`,
            properties: {
                destinations: {
                    monitoringAccounts: [
                        {
                            accountResourceId: `[parameters('${ParamName.AzureMonitorWorkspaceId}')]`,
                            name: 'MonitoringAccountDestination',
                        },
                    ],
                },
                dataFlows: [
                    {
                        streams: ['Microsoft-PrometheusMetrics'],
                        destinations: ['MonitoringAccountDestination'],
                    },
                ],
            },
        };
    }
}
