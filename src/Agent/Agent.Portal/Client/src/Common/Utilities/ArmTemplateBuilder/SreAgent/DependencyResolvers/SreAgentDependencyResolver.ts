import { ArmResourceDependencyResolver } from '../../ArmResourceDependencyResolver';
import { AppInsightsParameterName, ArmServiceType, SreAgentParameterName } from '../../ArmTemplateTypes';
import { SreAgentTemplateResource } from '../SreAgentTemplateResource';

interface SREAgentDependencyOptions {
    dependencyArray: string[];
    createNewAppInsights: boolean;
}

/**
 * Dependency resolver for SRE Agent resource
 * Ensures agent depends on identity, app insights, and role assignments
 */
export class SreAgentDependencyResolver implements ArmResourceDependencyResolver {
    get typeToResolveDependencyFor(): ArmServiceType {
        return ArmServiceType.Agents;
    }

    constructor(
        private _sreAgentTemplateResource: SreAgentTemplateResource,
        private _options: SREAgentDependencyOptions
    ) {}

    resolveDependencies(): void {
        const dependencies = [
            `[concat('${ArmServiceType.UserIdentity}/', parameters('${SreAgentParameterName.OidcUserIdentityName}'))]`,
            ...this._options.dependencyArray,
        ];

        if (this._options.createNewAppInsights) {
            dependencies.push(`[resourceId('${ArmServiceType.AppInsights}', parameters('${AppInsightsParameterName.AppInsightsName}'))]`);
        }

        this._sreAgentTemplateResource.dependsOn.push(...dependencies);
    }
}
