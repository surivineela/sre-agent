import { ArmServiceType } from './ArmTemplateTypes';

// Used to fix up any dependent resources that one resource may have on another.
// This could include setting up dependency orders, or adding app settings, etc...

export interface ArmResourceDependencyResolver {
    // The service type that this resolver was meant to be attached to.
    readonly typeToResolveDependencyFor: ArmServiceType | string;
    resolveDependencies(): void;
}
