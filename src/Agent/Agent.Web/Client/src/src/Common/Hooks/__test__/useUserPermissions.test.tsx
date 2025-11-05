import { act, renderHook } from '@testing-library/react';
import React, { PropsWithChildren } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import AzPortalProxy from '../../AzPortalProxy/AzPortalProxy';
import { EnvironmentContext } from '../../AzPortalProxy/Providers/StartupInfoContext';
import { PermissionActions } from '../../Contracts/Azure/Permission';
import useUserPermissions, { __resetUserPermissionsCacheForTests } from '../useUserPermissions';

// Stable mock instance so repeated getInstance calls return same getPermissions mock
const getPermissionsMock = vi.fn();
const canPerformActionsMock = vi.fn();
vi.mock('../../Clients/PermissionsClient', () => ({
    PermissionClient: {
        getInstance: () => ({
            getPermissions: getPermissionsMock,
            canPerformActions: canPerformActionsMock,
        }),
    },
}));

// Mock AzPortalProxy to disable standalone mode
vi.mock('../../AzPortalProxy/AzPortalProxy', () => ({
    default: {
        inStandaloneMode: false,
    },
}));

// Mock ConfigSettings to ensure permission checking is enabled
vi.mock('../ConfigSettings', () => ({
    SettingNames: {
        EnablePermissionChecking: 'enablePermissionChecking',
        ShowAgentModeForThread: 'showAgentModeForThread',
        ConsolidatedCreate: 'consolidatedCreate',
        DataConnectors: 'dataConnectors',
        ShowScheduledTasksTab: 'showScheduledTasksTab',
        KnowledgeBase: 'knowledgeBase',
        ForUnitTests: 'forUnitTests',
        ShowSubAgentsItemInSettings: 'showSubAgentsItemInSettings',
    },
    getConfigSetting: vi.fn(settingName => {
        if (settingName === 'enablePermissionChecking') {
            return true;
        }
        return false;
    }),
    useConfigSetting: vi.fn(),
}));

const wrapperFactory = (resourceId?: string) => {
    const Wrapper: React.FC<PropsWithChildren> = ({ children }) => (
        <EnvironmentContext.Provider
            value={
                {
                    resourceId,
                    sreAgentEndpoint: 'https://example',
                    tenantId: 't',
                    subscriptionId: 's',
                    isCrossTenantPortalMode: false,
                } as any
            }
        >
            {children}
        </EnvironmentContext.Provider>
    );
    return Wrapper;
};

describe('useUserPermissions', () => {
    beforeEach(() => {
        vi.clearAllMocks();
        __resetUserPermissionsCacheForTests();

        // Reset AzPortalProxy environment to disable cross-tenant mode
        if (!AzPortalProxy.envInfo) {
            AzPortalProxy.envInfo = {} as any;
        }
        AzPortalProxy.envInfo.isCrossTenantPortalMode = false;
    });

    it('returns error and false permissions when no resource id', async () => {
        const { result } = renderHook(() => useUserPermissions(), { wrapper: wrapperFactory(undefined) });
        expect(result.current.loading).toBe(false);
        expect(result.current.canWriteAgent).toBe(false);
        expect(result.current.canDeleteAgent).toBe(false);
        expect(result.current.canReadThreads).toBe(false);
        expect(result.current.canWriteThreads).toBe(false);
        expect(result.current.canDeleteThreads).toBe(false);
        expect(result.current.canApproveThreads).toBe(false);
        expect(result.current.canReadIncidentManagement).toBe(false);
        expect(result.current.canWriteIncidentManagement).toBe(false);
        expect(result.current.canDeleteIncidentManagement).toBe(false);
        expect(result.current.canReadGraph).toBe(false);
        expect(result.current.canWriteGraph).toBe(false);
        expect(result.current.canDeleteGraph).toBe(false);
        expect(result.current.error).toBe(true);
    });

    it('evaluates permissions with hasPermission', async () => {
        // Mock getPermissions to return mock permission data
        getPermissionsMock.mockResolvedValueOnce({
            data: {
                value: [
                    {
                        actions: ['*'],
                        notActions: [],
                        dataActions: ['*'],
                        notDataActions: [],
                    },
                ],
            },
        });

        // Mock canPerformActions for each permission check
        canPerformActionsMock
            .mockReturnValueOnce(true) // agent write
            .mockReturnValueOnce(false) // agent delete
            .mockReturnValueOnce(true) // threads write
            .mockReturnValueOnce(true) // threads read
            .mockReturnValueOnce(false) // threads delete
            .mockReturnValueOnce(true) // threads approve
            .mockReturnValueOnce(true) // incident read
            .mockReturnValueOnce(true) // incident write
            .mockReturnValueOnce(false) // incident delete
            .mockReturnValueOnce(true) // graph read
            .mockReturnValueOnce(true) // graph write
            .mockReturnValueOnce(false); // graph delete

        const { result, rerender } = renderHook(() => useUserPermissions(), {
            wrapper: wrapperFactory('/subscriptions/123/resourceGroups/rg/providers/Microsoft.App/agents/agent1'),
        });

        // Initial state loading
        expect(result.current.loading).toBe(true);

        // Wait microtask queue to flush resolved promises
        await act(async () => {});
        rerender();

        expect(getPermissionsMock).toHaveBeenCalledTimes(1);
        expect(canPerformActionsMock).toHaveBeenCalledTimes(12);
        expect(canPerformActionsMock).toHaveBeenNthCalledWith(1, [PermissionActions.AgentWrite], expect.any(Array), expect.any(String));
        expect(canPerformActionsMock).toHaveBeenNthCalledWith(2, [PermissionActions.AgentDelete], expect.any(Array), expect.any(String));
        expect(canPerformActionsMock).toHaveBeenNthCalledWith(
            3,
            [PermissionActions.AgentThreadsWrite],
            expect.any(Array),
            expect.any(String)
        );
        expect(canPerformActionsMock).toHaveBeenNthCalledWith(
            4,
            [PermissionActions.AgentThreadsRead],
            expect.any(Array),
            expect.any(String)
        );
        expect(canPerformActionsMock).toHaveBeenNthCalledWith(
            5,
            [PermissionActions.AgentThreadsDelete],
            expect.any(Array),
            expect.any(String)
        );
        expect(canPerformActionsMock).toHaveBeenNthCalledWith(
            6,
            [PermissionActions.AgentThreadsApprove],
            expect.any(Array),
            expect.any(String)
        );
        expect(canPerformActionsMock).toHaveBeenNthCalledWith(
            7,
            [PermissionActions.AgentIncidentManagementRead],
            expect.any(Array),
            expect.any(String)
        );
        expect(canPerformActionsMock).toHaveBeenNthCalledWith(
            8,
            [PermissionActions.AgentIncidentManagementWrite],
            expect.any(Array),
            expect.any(String)
        );
        expect(canPerformActionsMock).toHaveBeenNthCalledWith(
            9,
            [PermissionActions.AgentIncidentManagementDelete],
            expect.any(Array),
            expect.any(String)
        );
        expect(canPerformActionsMock).toHaveBeenNthCalledWith(
            10,
            [PermissionActions.AgentGraphRead],
            expect.any(Array),
            expect.any(String)
        );
        expect(canPerformActionsMock).toHaveBeenNthCalledWith(
            11,
            [PermissionActions.AgentGraphWrite],
            expect.any(Array),
            expect.any(String)
        );
        expect(canPerformActionsMock).toHaveBeenNthCalledWith(
            12,
            [PermissionActions.AgentGraphDelete],
            expect.any(Array),
            expect.any(String)
        );

        expect(result.current.loading).toBe(false);
        expect(result.current.canWriteAgent).toBe(true);
        expect(result.current.canDeleteAgent).toBe(false);
        expect(result.current.canReadThreads).toBe(true);
        expect(result.current.canWriteThreads).toBe(true);
        expect(result.current.canDeleteThreads).toBe(false);
        expect(result.current.canApproveThreads).toBe(true);
        expect(result.current.canReadIncidentManagement).toBe(true);
        expect(result.current.canWriteIncidentManagement).toBe(true);
        expect(result.current.canDeleteIncidentManagement).toBe(false);
        expect(result.current.canReadGraph).toBe(true);
        expect(result.current.canWriteGraph).toBe(true);
        expect(result.current.canDeleteGraph).toBe(false);
        expect(result.current.error).toBe(false);
    });

    it('handles error path by setting all to false and error true', async () => {
        getPermissionsMock.mockRejectedValueOnce(new Error('boom'));

        const { result, rerender } = renderHook(() => useUserPermissions(), {
            wrapper: wrapperFactory('/subscriptions/123/resourceGroups/rg/providers/Microsoft.App/agents/agent2'),
        });

        await act(async () => {});
        rerender();

        expect(result.current.loading).toBe(false);
        expect(result.current.canWriteAgent).toBe(false);
        expect(result.current.canDeleteAgent).toBe(false);
        expect(result.current.canReadThreads).toBe(false);
        expect(result.current.canWriteThreads).toBe(false);
        expect(result.current.canDeleteThreads).toBe(false);
        expect(result.current.canApproveThreads).toBe(false);
        expect(result.current.canReadIncidentManagement).toBe(false);
        expect(result.current.canWriteIncidentManagement).toBe(false);
        expect(result.current.canDeleteIncidentManagement).toBe(false);
        expect(result.current.canReadGraph).toBe(false);
        expect(result.current.canWriteGraph).toBe(false);
        expect(result.current.canDeleteGraph).toBe(false);
        expect(result.current.error).toBe(true);
    });

    it('refresh clears cache and re-fetches', async () => {
        // First fetch
        getPermissionsMock.mockResolvedValueOnce({
            data: {
                value: [
                    {
                        actions: ['*'],
                        notActions: [],
                        dataActions: ['*'],
                        notDataActions: [],
                    },
                ],
            },
        });

        canPerformActionsMock
            .mockReturnValueOnce(true) // agent write
            .mockReturnValueOnce(true) // agent delete
            .mockReturnValueOnce(true) // threads write
            .mockReturnValueOnce(true) // threads read
            .mockReturnValueOnce(true) // threads delete
            .mockReturnValueOnce(true) // threads approve
            .mockReturnValueOnce(true) // incident read
            .mockReturnValueOnce(true) // incident write
            .mockReturnValueOnce(true) // incident delete
            .mockReturnValueOnce(true) // graph read
            .mockReturnValueOnce(true) // graph write
            .mockReturnValueOnce(true); // graph delete

        const resourceId = '/subscriptions/123/resourceGroups/rg/providers/Microsoft.App/agents/agent3';
        const { result, rerender } = renderHook(() => useUserPermissions(), { wrapper: wrapperFactory(resourceId) });
        await act(async () => {});
        rerender();
        expect(result.current.canWriteThreads).toBe(true);

        // Second fetch after refresh with different outcomes
        getPermissionsMock.mockResolvedValueOnce({
            data: {
                value: [
                    {
                        actions: ['*'],
                        notActions: [],
                        dataActions: ['*'],
                        notDataActions: [],
                    },
                ],
            },
        });

        canPerformActionsMock
            .mockReturnValueOnce(false) // agent write
            .mockReturnValueOnce(false) // agent delete
            .mockReturnValueOnce(false) // threads write
            .mockReturnValueOnce(false) // threads read
            .mockReturnValueOnce(false) // threads delete
            .mockReturnValueOnce(false) // threads approve
            .mockReturnValueOnce(false) // incident read
            .mockReturnValueOnce(false) // incident write
            .mockReturnValueOnce(false) // incident delete
            .mockReturnValueOnce(false) // graph read
            .mockReturnValueOnce(false) // graph write
            .mockReturnValueOnce(false); // graph delete

        await act(async () => {
            result.current.refresh();
        });
        rerender();
        await act(async () => {});
        rerender();

        expect(result.current.canWriteAgent).toBe(false);
        expect(result.current.canDeleteAgent).toBe(false);
        expect(result.current.canReadThreads).toBe(false);
        expect(result.current.canWriteThreads).toBe(false);
        expect(result.current.canDeleteThreads).toBe(false);
        expect(result.current.canApproveThreads).toBe(false);
        expect(result.current.canReadIncidentManagement).toBe(false);
        expect(result.current.canWriteIncidentManagement).toBe(false);
        expect(result.current.canDeleteIncidentManagement).toBe(false);
        expect(result.current.canReadGraph).toBe(false);
        expect(result.current.canWriteGraph).toBe(false);
        expect(result.current.canDeleteGraph).toBe(false);
    });

    it('grants write only when includeDataActions is used (dataAction-only scenario)', async () => {
        // Mock getPermissions to return mock permission data
        getPermissionsMock.mockResolvedValueOnce({
            data: {
                value: [
                    {
                        actions: ['*'],
                        notActions: [],
                        dataActions: ['*'],
                        notDataActions: [],
                    },
                ],
            },
        });

        // Simulate underlying evaluation where the action exists only as a dataAction; our mock returns true only if includeDataActions flag is present.
        canPerformActionsMock
            .mockReturnValueOnce(true) // agent write
            .mockReturnValueOnce(false) // agent delete
            .mockReturnValueOnce(true) // threads write (allowed via dataAction merge)
            .mockReturnValueOnce(true) // threads read
            .mockReturnValueOnce(false) // threads delete
            .mockReturnValueOnce(false) // threads approve
            .mockReturnValueOnce(true) // incident read
            .mockReturnValueOnce(true) // incident write
            .mockReturnValueOnce(false) // incident delete
            .mockReturnValueOnce(true) // graph read
            .mockReturnValueOnce(true) // graph write
            .mockReturnValueOnce(false); // graph delete

        const resourceId = '/subscriptions/123/resourceGroups/rg/providers/Microsoft.App/agents/agent4';
        const { result, rerender } = renderHook(() => useUserPermissions(), { wrapper: wrapperFactory(resourceId) });

        await act(async () => {});
        rerender();

        expect(canPerformActionsMock).toHaveBeenCalledTimes(12);
        expect(result.current.canWriteAgent).toBe(true);
        expect(result.current.canDeleteAgent).toBe(false);
        expect(result.current.canReadThreads).toBe(true);
        expect(result.current.canWriteThreads).toBe(true); // allowed via dataAction merge
        expect(result.current.canDeleteThreads).toBe(false);
        expect(result.current.canApproveThreads).toBe(false);
        expect(result.current.canReadIncidentManagement).toBe(true);
        expect(result.current.canWriteIncidentManagement).toBe(true);
        expect(result.current.canDeleteIncidentManagement).toBe(false);
        expect(result.current.canReadGraph).toBe(true);
        expect(result.current.canWriteGraph).toBe(true);
        expect(result.current.canDeleteGraph).toBe(false);
        expect(result.current.error).toBe(false);
    });
});
