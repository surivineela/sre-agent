import { describe, expect, it } from 'vitest';
import {
    ArmIdKind,
    getResourceGroup,
    getSubscriptionId,
    isValidArmId,
    parseArmId,
    stringifyArmId,
} from '../ArmId';

describe('ArmId utilities', () => {
    describe('parseArmId', () => {
        it('parses subscription ID', () => {
            const result = parseArmId('/subscriptions/sub-123');
            expect(result.kind).toBe(ArmIdKind.Subscription);
            expect(result.subscription).toBe('sub-123');
            expect(result.resourceType).toBe('Microsoft.Resources/subscriptions');
        });

        it('parses resource group ID', () => {
            const result = parseArmId('/subscriptions/sub-123/resourceGroups/rg-name');
            expect(result.kind).toBe(ArmIdKind.ResourceGroup);
            expect(result.subscription).toBe('sub-123');
            expect(result.resourceGroup).toBe('rg-name');
            expect(result.resourceType).toBe('Microsoft.Resources/resourceGroups');
        });

        it('parses simple resource ID', () => {
            const result = parseArmId(
                '/subscriptions/sub-123/resourceGroups/rg-name/providers/Microsoft.Compute/virtualMachines/vm-name'
            );
            expect(result.kind).toBe(ArmIdKind.Resource);
            expect(result.subscription).toBe('sub-123');
            expect(result.resourceGroup).toBe('rg-name');
            expect(result.provider).toBe('Microsoft.Compute');
            expect(result.resourceType).toBe('Microsoft.Compute/virtualMachines');
            expect(result.resourceName).toBe('vm-name');
        });

        it('parses nested resource ID', () => {
            const result = parseArmId(
                '/subscriptions/sub-123/resourceGroups/rg-name/providers/Microsoft.Compute/virtualMachines/vm-name/extensions/ext-name'
            );
            expect(result.kind).toBe(ArmIdKind.Resource);
            expect(result.resourceType).toBe('Microsoft.Compute/virtualMachines/extensions');
            expect(result.resourceName).toBe('vm-name/ext-name');
        });

        it('parses location ID', () => {
            const result = parseArmId('/subscriptions/sub-123/locations/eastus');
            expect(result.kind).toBe(ArmIdKind.Location);
            expect(result.subscription).toBe('sub-123');
            expect(result.location).toBe('eastus');
        });

        it('parses subscription tag name', () => {
            const result = parseArmId('/subscriptions/sub-123/tagNames/Environment');
            expect(result.kind).toBe(ArmIdKind.SubscriptionTagName);
            expect(result.subscription).toBe('sub-123');
            expect(result.tagName).toBe('Environment');
        });

        it('parses subscription tag value', () => {
            const result = parseArmId('/subscriptions/sub-123/tagNames/Environment/tagValues/Production');
            expect(result.kind).toBe(ArmIdKind.SubscriptionTagValue);
            expect(result.subscription).toBe('sub-123');
            expect(result.tagName).toBe('Environment');
            expect(result.tagValue).toBe('Production');
        });

        it('strips query strings from ID', () => {
            const result = parseArmId('/subscriptions/sub-123?api-version=2021-01-01');
            expect(result.kind).toBe(ArmIdKind.Subscription);
            expect(result.subscription).toBe('sub-123');
        });

        it('returns invalid for non-string input', () => {
            const result = parseArmId(null as any);
            expect(result.kind).toBe(ArmIdKind.Invalid);
            expect(result.reason).toBe('not a string');
        });

        it('returns invalid for empty string', () => {
            const result = parseArmId('');
            expect(result.kind).toBe(ArmIdKind.Invalid);
            expect(result.reason).toBe('invalid number of segments');
        });

        it('returns invalid for ID not starting with /', () => {
            const result = parseArmId('subscriptions/sub-123');
            expect(result.kind).toBe(ArmIdKind.Invalid);
            expect(result.reason).toBe('invalid number of segments');
        });

        it('returns invalid for even number of segments', () => {
            const result = parseArmId('/subscriptions/sub-123/resourceGroups');
            expect(result.kind).toBe(ArmIdKind.Invalid);
            expect(result.reason).toBe('invalid number of segments');
        });

        it('returns invalid for empty segment', () => {
            const result = parseArmId('/subscriptions//resourceGroups/rg');
            expect(result.kind).toBe(ArmIdKind.Invalid);
            expect(result.reason).toBe('empty segment');
        });

        it('returns invalid for unknown token', () => {
            const result = parseArmId('/subscriptions/sub-123/unknown/value');
            expect(result.kind).toBe(ArmIdKind.Invalid);
            expect(result.reason).toContain('invalid token');
        });
    });

    describe('stringifyArmId', () => {
        it('reconstructs subscription ID', () => {
            const armId = parseArmId('/subscriptions/sub-123');
            const result = stringifyArmId(armId);
            expect(result).toBe('/subscriptions/sub-123');
        });

        it('reconstructs resource group ID', () => {
            const armId = parseArmId('/subscriptions/sub-123/resourceGroups/rg-name');
            const result = stringifyArmId(armId);
            expect(result).toBe('/subscriptions/sub-123/resourceGroups/rg-name');
        });

        it('reconstructs resource ID', () => {
            const armId = parseArmId(
                '/subscriptions/sub-123/resourceGroups/rg-name/providers/Microsoft.Compute/virtualMachines/vm-name'
            );
            const result = stringifyArmId(armId);
            expect(result).toBe(
                '/subscriptions/sub-123/resourceGroups/rg-name/providers/Microsoft.Compute/virtualMachines/vm-name'
            );
        });

        it('reconstructs nested resource ID', () => {
            const armId = parseArmId(
                '/subscriptions/sub-123/resourceGroups/rg-name/providers/Microsoft.Compute/virtualMachines/vm-name/extensions/ext-name'
            );
            const result = stringifyArmId(armId);
            expect(result).toBe(
                '/subscriptions/sub-123/resourceGroups/rg-name/providers/Microsoft.Compute/virtualMachines/vm-name/extensions/ext-name'
            );
        });

        it('reconstructs location ID', () => {
            const armId = parseArmId('/subscriptions/sub-123/locations/eastus');
            const result = stringifyArmId(armId);
            expect(result).toBe('/subscriptions/sub-123/locations/eastus');
        });

        it('reconstructs tag IDs', () => {
            const armId = parseArmId('/subscriptions/sub-123/tagNames/Environment/tagValues/Production');
            const result = stringifyArmId(armId);
            expect(result).toBe('/subscriptions/sub-123/tagNames/Environment/tagValues/Production');
        });

        it('returns empty string for invalid ARM ID', () => {
            const result = stringifyArmId({ kind: ArmIdKind.Invalid, subscription: '', resourceGroup: '', resourceName: '', resourceType: '', location: '', provider: '', tagName: '', tagValue: '' });
            expect(result).toBe('');
        });

        it('returns empty string for null input', () => {
            const result = stringifyArmId(null as any);
            expect(result).toBe('');
        });
    });

    describe('getSubscriptionId', () => {
        it('extracts subscription ID from subscription ARM ID', () => {
            const result = getSubscriptionId('/subscriptions/sub-123');
            expect(result).toBe('sub-123');
        });

        it('extracts subscription ID from resource group ARM ID', () => {
            const result = getSubscriptionId('/subscriptions/sub-456/resourceGroups/rg-name');
            expect(result).toBe('sub-456');
        });

        it('extracts subscription ID from resource ARM ID', () => {
            const result = getSubscriptionId(
                '/subscriptions/sub-789/resourceGroups/rg/providers/Microsoft.Compute/virtualMachines/vm'
            );
            expect(result).toBe('sub-789');
        });

        it('returns empty string for invalid ARM ID', () => {
            const result = getSubscriptionId('not-an-arm-id');
            expect(result).toBe('');
        });
    });

    describe('getResourceGroup', () => {
        it('extracts resource group from resource group ARM ID', () => {
            const result = getResourceGroup('/subscriptions/sub-123/resourceGroups/my-rg');
            expect(result).toBe('my-rg');
        });

        it('extracts resource group from resource ARM ID', () => {
            const result = getResourceGroup(
                '/subscriptions/sub-123/resourceGroups/test-rg/providers/Microsoft.Storage/storageAccounts/myaccount'
            );
            expect(result).toBe('test-rg');
        });

        it('returns empty string for subscription-level ARM ID', () => {
            const result = getResourceGroup('/subscriptions/sub-123');
            expect(result).toBe('');
        });

        it('returns empty string for invalid ARM ID', () => {
            const result = getResourceGroup('invalid');
            expect(result).toBe('');
        });
    });

    describe('isValidArmId', () => {
        it('returns true for valid subscription ID', () => {
            expect(isValidArmId('/subscriptions/sub-123')).toBe(true);
        });

        it('returns true for valid resource group ID', () => {
            expect(isValidArmId('/subscriptions/sub-123/resourceGroups/rg-name')).toBe(true);
        });

        it('returns true for valid resource ID', () => {
            expect(
                isValidArmId(
                    '/subscriptions/sub-123/resourceGroups/rg/providers/Microsoft.Compute/virtualMachines/vm'
                )
            ).toBe(true);
        });

        it('returns false for empty string', () => {
            expect(isValidArmId('')).toBe(false);
        });

        it('returns false for malformed ID', () => {
            expect(isValidArmId('not-an-arm-id')).toBe(false);
        });

        it('returns false for ID with missing segments', () => {
            expect(isValidArmId('/subscriptions/sub-123/resourceGroups')).toBe(false);
        });
    });

    describe('round-trip parsing', () => {
        const testCases = [
            '/subscriptions/sub-123',
            '/subscriptions/sub-123/resourceGroups/rg-name',
            '/subscriptions/sub-123/resourceGroups/rg/providers/Microsoft.Compute/virtualMachines/vm',
            '/subscriptions/sub-123/resourceGroups/rg/providers/Microsoft.Compute/virtualMachines/vm/extensions/ext',
            '/subscriptions/sub-123/locations/westus',
            '/subscriptions/sub-123/tagNames/Environment/tagValues/Dev',
        ];

        testCases.forEach((testCase) => {
            it(`parse and stringify round-trip: ${testCase}`, () => {
                const parsed = parseArmId(testCase);
                const stringified = stringifyArmId(parsed);
                expect(stringified).toBe(testCase);
            });
        });
    });
});
