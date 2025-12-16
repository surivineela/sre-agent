import { createContext, ReactNode, useMemo } from 'react';
import { TelemetrySource } from '../Constants/Telemetry';
import { IncompleteAmplitudeData, ProductName } from '../Contracts/Amplitude';
import { parseArmId } from '../Utilities/ArmId';
import { useIsInternal } from '../Hooks/useIsInternal';

interface IAmplitudeContext {
    amplitudeMetadata: IncompleteAmplitudeData;
    telemetrySource: TelemetrySource;
}

export const AmplitudeContext = createContext<IAmplitudeContext>({
    amplitudeMetadata: {
        productName: ProductName.Unknown,
        resourceId: '',
        subscriptionId: '',
        resourceGroup: '',
        resourceName: '',
        isInternal: false,
        isInternalTenant: false,
    },
    telemetrySource: TelemetrySource.Unknown,
});

const getAmplitudeProductName = (resourceType: string) => {
    const lowerResourceType = resourceType.toLowerCase();

    switch (lowerResourceType) {
        case 'agents':
            return ProductName.SreAgent;
        case 'agentspaces':
            return ProductName.SreAgentSpace;
        default:
            return ProductName.Unknown;
    }
};

interface AmplitudeContextProviderProps {
    resourceId: string;
    /** Should only be passed in if in a scenario that doesn't have a single `resourceId` (such as browse, create, etc.) */
    productName?: ProductName;
    telemetrySource: TelemetrySource;
    children: ReactNode;
}

export const AmplitudeContextProvider = (props: AmplitudeContextProviderProps) => {
    const { resourceId, productName: providedProductName, telemetrySource, children } = props;

    const { isInternalTenant, isInternalProdTenant } = useIsInternal();

    const resourceDescriptor = useMemo(() => parseArmId(resourceId), [resourceId]);

    const productName = useMemo(
        () => providedProductName || getAmplitudeProductName(resourceDescriptor.resourceType),
        [providedProductName, resourceDescriptor.resourceType]
    );


    const amplitudeMetadata = useMemo<IncompleteAmplitudeData>(() => {
        const metadata: IncompleteAmplitudeData = {
            productName,
            resourceId,
            subscriptionId: resourceDescriptor.subscription,
            resourceGroup: resourceDescriptor.resourceGroup,
            resourceName: resourceDescriptor.resourceName,
            isInternal: isInternalTenant,
            isInternalTenant: isInternalProdTenant,
        };

        return metadata;
    }, [productName, resourceId, resourceDescriptor, isInternalProdTenant, isInternalTenant]);

    return <AmplitudeContext.Provider value={{ telemetrySource, amplitudeMetadata }}>{children}</AmplitudeContext.Provider>;
};
