import { ArmResourceDependencyResolver } from './ArmResourceDependencyResolver';
import { ArmTemplateBuilder } from './ArmTemplateBuilder';
import { ArmServiceType, ArmTemplateParameter, ArmTemplateResourceFragment } from './ArmTemplateTypes';

export abstract class ArmTemplateResource<T> {
    // The "type" for the resource that's identified within the template
    abstract get type(): ArmServiceType | string;

    // Handles the logic to place the resource in its appropriate place within
    // a template. It can be either a top-level resource or a nested resource.
    // Regardless it's up to each resource to figure out its place in the template.
    abstract addResourceToBuilder(): void;

    // Returns the JSON fragment for the given resource.  It's not expected that this
    // fragment needs to take care of child resources since the base class will take
    // care of this for you.
    protected abstract _getTemplateFragmentHelper(): ArmTemplateResourceFragment<T>;

    // A list of child resources
    resources: ArmTemplateResource<any>[] = [];

    // List of ARM template dependency rules
    dependsOn: string[] = [];

    // Before the final template gets generated, the builder will call this
    // to fix up any dependencies within the template that may be necessary
    // in order to ensure that provisioning occurs in a specific order.
    dependencyResolvers: ArmResourceDependencyResolver[] = [];

    // Template input parameters that are required for this resource
    parameters: Record<string, ArmTemplateParameter> = {};

    constructor(protected _builder: ArmTemplateBuilder) {}

    resolveDependencies(): void {
        if (this.dependencyResolvers) {
            this.dependencyResolvers.forEach(dr => {
                if (dr.typeToResolveDependencyFor !== this.type) {
                    throw Error(
                        `This resolver is expecting type '${dr.typeToResolveDependencyFor}' but is instead attached to type '${this.type}'`
                    );
                }

                dr.resolveDependencies();
            });
        }
    }

    getTemplateFragment(): ArmTemplateResourceFragment<T> {
        const fragment = this._getTemplateFragmentHelper();
        this._builder.mergeParameters(this.parameters);
        const childResources: ArmTemplateResourceFragment<any>[] = [];

        this.resources.forEach(r => {
            childResources.push(r.getTemplateFragment());
        });

        if (childResources.length > 0) {
            fragment.resources = childResources;
        }

        return fragment;
    }
}
