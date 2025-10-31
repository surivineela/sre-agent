/**
 * Interface for resolving dependencies between ARM resources.
 * Used to fix up dependent resources, set up dependency orders, or add configuration.
 */
export interface ArmResourceDependencyResolver {
    /**
     * The service type that this resolver was meant to be attached to.
     */
    readonly typeToResolveDependencyFor: string;

    /**
     * Called to resolve and add dependencies for the resource
     */
    resolveDependencies(): void;
}
