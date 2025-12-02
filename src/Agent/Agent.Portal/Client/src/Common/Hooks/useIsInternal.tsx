import { useAuth } from '../Contexts/AuthContext';

const devTenantId = 'c2b1aad6-a137-43d7-af13-b21ce1904be9';
const internalTenantId = '72f988bf-86f1-41af-91ab-2d7cd011db47'; // MSFT/CORP

const internalProdTenantIds = [
    '33e01921-4d64-4f8c-a055-5bdaffd5e33d', // AME
    '975f013f-7f24-47e8-a7d3-abc4752bf346', // PME
    'cdc5aeea-15c5-4db6-b079-fcadd2505dc2', // Torus
];

export const useIsInternal = () => {
    const { user } = useAuth();

    return {
        userTenantId: user?.tenantId,
        isInternalDevTenant: user?.tenantId === devTenantId,
        isInternalTenant: user?.tenantId === internalTenantId || internalProdTenantIds.includes(user?.tenantId ?? ''),
        isInternalProdTenant: internalProdTenantIds.includes(user?.tenantId ?? ''),
        isInternalPortal: window.location.hostname.endsWith('int.sre.azure.com'),
    };
};
