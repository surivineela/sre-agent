import { describe, expect, it } from 'vitest';
import { PermissionClient } from '../PermissionsClient';

// We test the private behavior indirectly by invoking canPerformActions with constructed permission sets.
// Patterns of the form X/*/Y should now REQUIRE an intermediate segment and NOT match X/Y after tightening.

describe('PermissionsClient wildcard tightening', () => {
    const client = PermissionClient.getInstance();
    const resourceId = '/subscriptions/sub/resourceGroups/rg/providers/Microsoft.App/agents/myAgent';

    function can(actions: string[], available: string[]): boolean {
        return client.canPerformActions(actions, [{ actions: available, notActions: [] }], resourceId);
    }

    it('matches with intermediate segment for pattern with /*/ segment', () => {
        expect(can(['Microsoft.App/agents/foo/delete'], ['Microsoft.App/agents/*/delete'])).toBe(true);
    });

    it('does NOT match without intermediate segment for pattern with /*/ segment', () => {
        expect(can(['Microsoft.App/agents/delete'], ['Microsoft.App/agents/*/delete'])).toBe(false);
    });

    it('still matches prefix pattern ending in /* with or without trailing segment', () => {
        // Existing behavior for endings with /* remains: X/* matches X and X/anything
        expect(can(['Microsoft.App/agents'], ['Microsoft.App/agents/*'])).toBe(true);
        expect(can(['Microsoft.App/agents/foo'], ['Microsoft.App/agents/*'])).toBe(true);
    });
});
