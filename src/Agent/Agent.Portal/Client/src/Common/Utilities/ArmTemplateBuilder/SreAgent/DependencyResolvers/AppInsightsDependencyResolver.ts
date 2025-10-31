import { ArmResourceDependencyResolver } from '../../ArmResourceDependencyResolver';
import { ArmTemplateBuilder } from '../../ArmTemplateBuilder';
import { AppInsightsParameterName, ArmServiceType, SreAgentParameterName } from '../../ArmTemplateTypes';

/**
 * Dependency resolver for Application Insights
 * Ensures App Insights depends on the workspace
 */
export class AppInsightsDependencyResolver implements ArmResourceDependencyResolver {
    constructor(
        private _builder: ArmTemplateBuilder,
        private _isSreAgent: boolean = false
    ) {}

    get typeToResolveDependencyFor(): ArmServiceType {
        return ArmServiceType.AppInsights;
    }

    resolveDependencies(): void {
        const appInsights = this._builder.findResourceById(ArmServiceType.AppInsights);
        if (appInsights) {
            const workspaceParamName = this._isSreAgent ? SreAgentParameterName.WorkspaceName : AppInsightsParameterName.WorkspaceName;
            appInsights.dependsOn.push(`[concat('${ArmServiceType.Workspace}/', parameters('${workspaceParamName}'))]`);
        }
    }
}
