import { ApiVersions } from '../../../Constants/ApiVersions';
import { ResourceTypes } from '../../../Constants/Arm';
import { PermissionPrincipalType } from '../../../Contracts/Permissions';
import { ArmResourceDependencyResolver } from '../ArmResourceDependencyResolver';
import { ArmTemplateBuilder } from '../ArmTemplateBuilder';
import { ArmTemplateResource } from '../ArmTemplateResource';
import {
    ArmServiceType,
    ArmTemplateParameter,
    ArmTemplateParameterName,
    ArmTemplateResourceFragment,
    DeploymentFragment,
    SreAgentParameterName,
} from '../ArmTemplateTypes';

interface UserRoleAssignmentOptions {
    roleDefinitionId: string;
    deploymentGuid: string;
}

interface RbacFragment {
    roleDefinitionId: string;
    principalId: string;
    principalType: string;
}

/**
 * Dependency resolver for user role assignment
 * Ensures user role assignment depends on the agent being created
 */
class UserRoleAssignmentDependencyResolver implements ArmResourceDependencyResolver {
    constructor(private _template: UserRoleAssignmentTemplateResource) {}

    get typeToResolveDependencyFor(): ArmServiceType {
        return this._template.type as ArmServiceType;
    }

    resolveDependencies(): void {
        this._template.dependsOn.push(`[resourceId('${ArmServiceType.Agents}', parameters('${SreAgentParameterName.AgentName}'))]`);
    }
}

/**
 * ARM template resource for assigning a role to the deploying user
 * This gives the user administrative access to the created SRE Agent
 */
export class UserRoleAssignmentTemplateResource extends ArmTemplateResource<object> {
    get type() {
        return `${ArmServiceType.SiteRbac}-user-${this._options.deploymentGuid}`;
    }

    parameters: Record<string, ArmTemplateParameter> = {
        [SreAgentParameterName.UserObjectId]: {
            type: 'string',
        },
    };

    constructor(
        builder: ArmTemplateBuilder,
        private _options: UserRoleAssignmentOptions
    ) {
        super(builder);
        this.dependencyResolvers = [new UserRoleAssignmentDependencyResolver(this)];
    }

    addResourceToBuilder(): void {
        this._builder.resources.push(this);
    }

    _getTemplateFragmentHelper(): ArmTemplateResourceFragment<DeploymentFragment> {
        return {
            type: ResourceTypes.ResourceDeploymentType,
            apiVersion: ApiVersions.armApiVersion20250301,
            name: `[concat('UserRoleAssignment-', '${this._options.deploymentGuid}')]`,
            dependsOn: this.dependsOn,
            properties: {
                mode: 'Incremental',
                template: {
                    $schema: 'https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#',
                    contentVersion: '1.0.0.0',
                    resources: [this._getRoleAssignmentResource()],
                },
            },
        };
    }

    private _getRoleAssignmentResource(): ArmTemplateResourceFragment<RbacFragment> {
        return {
            apiVersion: ApiVersions.permissionsApiVersion20220401,
            name: `[guid(parameters('${SreAgentParameterName.AgentName}'), '${this._options.roleDefinitionId}', parameters('${SreAgentParameterName.UserObjectId}'), '${this._options.deploymentGuid}')]`,
            type: ArmServiceType.SiteRbac,
            scope: `[resourceId('${ArmServiceType.Agents}', parameters('${SreAgentParameterName.AgentName}'))]`,
            location: `[parameters('${ArmTemplateParameterName.Location}')]`,
            properties: {
                roleDefinitionId: `[subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '${this._options.roleDefinitionId}')]`,
                principalId: `[parameters('${SreAgentParameterName.UserObjectId}')]`,
                principalType: PermissionPrincipalType.user,
            },
        };
    }
}
