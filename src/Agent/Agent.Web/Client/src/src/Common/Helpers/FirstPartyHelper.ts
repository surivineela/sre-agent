import { AntUxStringComparison, equals } from './Strings';

export class FirstPartyHelper {
    private static ameTenantId = '33e01921-4d64-4f8c-a055-5bdaffd5e33d';
    private static corpTenantId = '72f988bf-86f1-41af-91ab-2d7cd011db47';
    public static isFirstPartyTenant(tenantId: string): boolean {
        return FirstPartyHelper.isAmeTenant(tenantId) || FirstPartyHelper.isCorpTenant(tenantId);
    }

    public static isAmeTenant(tenantId: string): boolean {
        return equals(tenantId, FirstPartyHelper.ameTenantId, AntUxStringComparison.IgnoreCase);
    }

    public static isCorpTenant(tenantId: string): boolean {
        return equals(tenantId, FirstPartyHelper.corpTenantId, AntUxStringComparison.IgnoreCase);
    }
}
