import { describe, expect, it } from 'vitest';
import { ArmResourceDescriptor } from '../ResourceDescriptors';

describe('ArmResourceDescriptor', () => {
    it('parses subscription, resourceGroup, resourceName', () => {
        const id = '/subscriptions/sub123/resourceGroups/rg1/providers/Microsoft.Web/sites/siteA';
        const d = new ArmResourceDescriptor(id);
        expect(d.subscription).toBe('sub123');
        expect(d.resourceGroup).toBe('rg1');
        expect(d.resourceName).toBe('siteA');
        expect(d.getTrimmedResourceId()).toBe(id);
    });

    it('throws on malformed id', () => {
        expect(() => new ArmResourceDescriptor('/wrong/format')).toThrow();
    });
});
