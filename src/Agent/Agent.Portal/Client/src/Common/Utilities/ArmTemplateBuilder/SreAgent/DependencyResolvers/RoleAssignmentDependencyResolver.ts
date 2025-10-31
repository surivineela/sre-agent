import { ArmResourceDependencyResolver } from '../../ArmResourceDependencyResolver';
import { ArmServiceType, SreAgentParameterName } from '../../ArmTemplateTypes';
import { RoleAssignmentTemplateResource } from '../RoleAssignmentTemplateResource';

/**
 * Dependency resolver for Role Assignment resource
 * Ensures role assignment depends on the user identity being created
 */
export class RoleAssignmentDependencyResolver implements ArmResourceDependencyResolver {
    get typeToResolveDependencyFor(): ArmServiceType {
        return this._roleAssignmentTemplateResource.type as ArmServiceType;
    }

    constructor(private _roleAssignmentTemplateResource: RoleAssignmentTemplateResource) {}

    resolveDependencies(): void {
        this._roleAssignmentTemplateResource.dependsOn.push(
            `[concat('${ArmServiceType.UserIdentity}/', parameters('${SreAgentParameterName.OidcUserIdentityName}'))]`
        );
    }
}
