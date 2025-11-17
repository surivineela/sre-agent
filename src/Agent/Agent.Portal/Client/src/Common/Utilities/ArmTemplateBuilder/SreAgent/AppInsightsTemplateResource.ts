import { ArmResourceDependencyResolver } from '../ArmResourceDependencyResolver';
import { ArmTemplateBuilder } from '../ArmTemplateBuilder';
import { ArmTemplateResource } from '../ArmTemplateResource';
import {
    AppInsightsParameterName,
    ArmServiceType,
    ArmTemplateParameter,
    ArmTemplateParameterName,
    ArmTemplateResourceFragment,
} from '../ArmTemplateTypes';

interface AppInsightsFragment {
    ApplicationId: string;
    Application_Type: string;
    Flow_Type: string;
    Request_Source: string;
    WorkspaceResourceId: string;
}

interface AppInsightsResourceOptions {
    subscription: string;
    resourceGroup: string;
    workspaceId?: string;
    dependencyResolvers?: ArmResourceDependencyResolver[];
}

/**
 * ARM template resource for creating Application Insights
 */
export class AppInsightsTemplateResource extends ArmTemplateResource<AppInsightsFragment> {
    get type() {
        return ArmServiceType.AppInsights;
    }

    parameters: Record<string, ArmTemplateParameter> = {
        [AppInsightsParameterName.AppInsightsName]: {
            type: 'string',
            defaultValue: "[format('app-insights-{0}', uniqueString(resourceGroup().id, deployment().name))]",
        },
        [AppInsightsParameterName.AppInsightsApplicationType]: {
            type: 'string',
            defaultValue: 'web',
        },
        [AppInsightsParameterName.AppInsightsRequestSource]: {
            type: 'string',
            defaultValue: 'IbizaAIExtension',
        },
    };

    constructor(
        builder: ArmTemplateBuilder,
        private _options: AppInsightsResourceOptions
    ) {
        super(builder);

        this.dependencyResolvers = this._options?.dependencyResolvers ? this._options.dependencyResolvers : [];
    }

    addResourceToBuilder(): void {
        this._builder.resources.push(this);
    }

    _getTemplateFragmentHelper(): ArmTemplateResourceFragment<AppInsightsFragment> {
        return {
            apiVersion: '2020-02-02-preview',
            name: `[parameters('${AppInsightsParameterName.AppInsightsName}')]`,
            type: ArmServiceType.AppInsights,
            location: `[parameters('${ArmTemplateParameterName.Location}')]`,
            dependsOn: this.dependsOn,
            properties: {
                ApplicationId: `[parameters('${AppInsightsParameterName.AppInsightsName}')]`,
                Application_Type: `[parameters('${AppInsightsParameterName.AppInsightsApplicationType}')]`,
                Flow_Type: 'Redfield',
                Request_Source: `[parameters('${AppInsightsParameterName.AppInsightsRequestSource}')]`,
                WorkspaceResourceId: `[resourceId('${ArmServiceType.Workspace}', parameters('${AppInsightsParameterName.WorkspaceName}'))]`,
            },
        };
    }
}
