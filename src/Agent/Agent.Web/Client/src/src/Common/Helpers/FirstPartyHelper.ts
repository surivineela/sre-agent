import { AntUxStringComparison, equals } from './Strings';

enum TenantType {
    AME,
    Corp,
    PME,
    TORUS,
    Other,
}

export class FirstPartyHelper {
    public static getTenantType(tenantId: string): TenantType {
        if (equals(tenantId, '33e01921-4d64-4f8c-a055-5bdaffd5e33d', AntUxStringComparison.IgnoreCase)) {
            return TenantType.AME;
        }
        if (equals(tenantId, '72f988bf-86f1-41af-91ab-2d7cd011db47', AntUxStringComparison.IgnoreCase)) {
            return TenantType.Corp;
        }
        if (equals(tenantId, '975f013f-7f24-47e8-a7d3-abc4752bf346', AntUxStringComparison.IgnoreCase)) {
            return TenantType.PME;
        }
        if (equals(tenantId, 'cdc5aeea-15c5-4db6-b079-fcadd2505dc2', AntUxStringComparison.IgnoreCase)) {
            return TenantType.TORUS;
        }
        return TenantType.Other;
    }

    public static shouldEnableForIcm(tenantId: string): boolean {
        // For local development, always enable
        if (import.meta.env.MODE === 'development') {
            return true;
        }

        const type = FirstPartyHelper.getTenantType(tenantId);
        return type === TenantType.AME || type === TenantType.Corp || type === TenantType.PME || type === TenantType.TORUS;
    }
}
