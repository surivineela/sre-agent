import { describe, expect, it } from 'vitest';
import { ResourceExtended } from '../../Contracts/Graph';
import { getAppGroupEffectiveType } from '../Utility';

const makeAppGroup = (overrides: Partial<ResourceExtended>): ResourceExtended => ({
    id: 'sub_rg_name',
    name: 'test-app',
    type: 'microsoft.web/sites',
    dashboardUrl: '',
    properties: {
        dashboardUrl: [''],
        resourceType: ['microsoft.web/sites'],
        resourceKind: ['app'],
        resourceName: ['test-app'],
        subscriptionId: ['sub'],
        resourceGroupName: ['rg'],
        location: ['westus'],
        runningStatus: 'Running',
        remarks: [''],
    },
    ...overrides,
});

describe('getAppGroupEffectiveType', () => {
    it('returns functionapp when kind indicates function app', () => {
        const ag = makeAppGroup({ properties: { ...makeAppGroup({}).properties, kind: ['functionapp'] } as any });
        expect(getAppGroupEffectiveType(ag)).toBe('functionapp');
    });

    it('returns functionapp when properties.resourceKind array contains functionapp', () => {
        const ag = makeAppGroup({ properties: { ...makeAppGroup({}).properties, resourceKind: ['functionapp'] } });
        expect(getAppGroupEffectiveType(ag)).toBe('functionapp');
    });

    it('returns original type when not a function app', () => {
        const ag = makeAppGroup({ type: 'microsoft.web/sites', properties: { ...makeAppGroup({}).properties, resourceKind: ['app'] } });
        expect(getAppGroupEffectiveType(ag)).toBe('microsoft.web/sites');
    });

    it('handles non-array kind string', () => {
        const ag: any = makeAppGroup({});
        ag.kind = 'functionApp';
        expect(getAppGroupEffectiveType(ag)).toBe('functionapp');
    });

    it('returns empty string safely when appGroup is undefined', () => {
        // @ts-expect-error testing undefined input
        expect(getAppGroupEffectiveType(undefined)).toBe('');
    });
});
