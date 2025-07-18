import { useCallback, useContext, useEffect, useMemo, useState } from 'react';
import useIntl from 'react-intl/src/components/useIntl';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ThreadClient } from '../../Common/Clients/ThreadClient';
import { AgentMode } from '../../Common/Contracts/Azure/SreAgent';
import { getAgentModeDescription, getAgentModeDisplayName } from '../../Common/Helpers/AgentMode';
import { AntUxStringComparison, equals } from '../../Common/Helpers/Strings';
import { AgentModeResources } from '../../Strings/SREAgentResources';
import { IAgentModeInfo, IAgentModeSelectorProps } from '../Contracts/Activities';
import { ThreadAgentModeContext } from '../Contracts/Context';

export interface AgentModesInfo {
    canEditAgentMode: boolean;
    info?: string;
}

export const useAgentModeSelector = ({ threadId, disabled }: IAgentModeSelectorProps) => {
    const [availableAgentModes, setAvailableAgentModes] = useState<string[]>([]);
    const [isLoadingAgentModes, setIsLoadingAgentModes] = useState<boolean>(false);
    const [isUpdatingAgentMode, setIsUpdatingAgentMode] = useState<boolean>(false);
    const [loadingAgentModesError, setLoadingAgentModesError] = useState<string | null>(null);
    const [updatingAgentModeError, setUpdatingAgentModeError] = useState<string | null>(null);

    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const threadClient = ThreadClient.getInstance(sreAgentEndpoint);

    const portalContext = useContext(AzPortalContext);
    const intl = useIntl();

    const {
        threadAgentMode,
        isLoadingThreadAgentMode,
        isFetchingThreadAgentMode,
        fetchThreadAgentModeError,
        invalidateThreadAgentModeDataCache,
    } = useContext(ThreadAgentModeContext);

    const agentModes = useMemo(() => {
        return availableAgentModes || [];
    }, [availableAgentModes]);

    // Get agent modes information and editable state based on global restrictions
    const agentModesInfo = useMemo<AgentModesInfo>(() => {
        if (isLoadingAgentModes) {
            return {
                canEditAgentMode: false,
            };
        }

        if (!availableAgentModes || availableAgentModes.length === 0) {
            return {
                canEditAgentMode: false,
                info: intl.formatMessage(AgentModeResources.agentsModesUnavailableMessage),
            };
        }

        // If only one mode is available (ReadOnly), button should be disabled
        if (availableAgentModes.length === 1 && equals(availableAgentModes[0], AgentMode.readonly, AntUxStringComparison.IgnoreCase)) {
            return {
                canEditAgentMode: false,
                info: intl.formatMessage(AgentModeResources.agentModeRestrictionMessage),
            };
        }

        return {
            canEditAgentMode: true,
        };
    }, [availableAgentModes, isLoadingAgentModes]);

    const isButtonDisabled = useMemo(() => {
        return (
            !agentModesInfo.canEditAgentMode || isLoadingAgentModes || !!loadingAgentModesError || !!fetchThreadAgentModeError || disabled
        );
    }, [agentModesInfo, isLoadingAgentModes, loadingAgentModesError, fetchThreadAgentModeError, disabled]);

    const buttonTooltipText = useMemo(() => {
        return (
            loadingAgentModesError ||
            (fetchThreadAgentModeError ? intl.formatMessage(AgentModeResources.fetchAgentModeFailureMessage) : undefined) ||
            agentModesInfo.info ||
            intl.formatMessage(AgentModeResources.agentModeTooltip)
        );
    }, [loadingAgentModesError, agentModesInfo, fetchThreadAgentModeError]);

    const showButtonLoadingSpinner = useMemo(() => {
        return isLoadingAgentModes || isLoadingThreadAgentMode || isFetchingThreadAgentMode;
    }, [isLoadingAgentModes, isLoadingThreadAgentMode, isFetchingThreadAgentMode]);

    // Fetch available agent modes from the server
    const fetchAvailableAgentModes = useCallback(async () => {
        setIsLoadingAgentModes(true);
        setLoadingAgentModesError(null);

        const response = await threadClient.getAvailableAgentModes();
        if (response.isSuccessful && response.content) {
            setAvailableAgentModes(response.content);
        } else {
            setLoadingAgentModesError(response.error?.message || intl.formatMessage(AgentModeResources.fetchAgentModesFailureMessage));
        }

        setIsLoadingAgentModes(false);
    }, [threadClient]);

    // Update thread agent mode
    const updateThreadAgentMode = useCallback(
        async (agentMode: string) => {
            if (!threadId || agentMode === threadAgentMode || isUpdatingAgentMode) {
                return;
            }

            setIsUpdatingAgentMode(true);

            const response = await threadClient.updateThreadAgentMode(threadId, agentMode);

            if (response.isSuccessful) {
                invalidateThreadAgentModeDataCache();
                setUpdatingAgentModeError(null);
            } else {
                portalContext.log({
                    action: 'updateAgentMode',
                    actionModifier: 'failed',
                    logLevel: 'error',
                    data: {
                        message: `Failed to update agent mode`,
                    },
                });
                setUpdatingAgentModeError(intl.formatMessage(AgentModeResources.updateAgentModeFailureDescription));
            }

            setIsUpdatingAgentMode(false);
        },
        [threadId, threadAgentMode, isUpdatingAgentMode]
    );

    // Get agent mode information with display names and descriptions
    const getAgentModeInfo = useCallback(
        (mode: string): IAgentModeInfo => {
            const agentModeDescriptions: Record<string, IAgentModeInfo> = {
                [AgentMode.readonly]: {
                    mode: AgentMode.readonly,
                    displayName: getAgentModeDisplayName(AgentMode.readonly, intl),
                    description: getAgentModeDescription(AgentMode.readonly, intl),
                },
                [AgentMode.review]: {
                    mode: AgentMode.review,
                    displayName: getAgentModeDisplayName(AgentMode.review, intl),
                    description: getAgentModeDescription(AgentMode.review, intl),
                },
                [AgentMode.autonomous]: {
                    mode: AgentMode.autonomous,
                    displayName: getAgentModeDisplayName(AgentMode.autonomous, intl),
                    description: getAgentModeDescription(AgentMode.autonomous, intl),
                },
            };

            return (
                agentModeDescriptions[mode] || {
                    mode,
                    displayName: mode,
                    description: getAgentModeDescription('', intl),
                }
            );
        },
        [intl]
    );

    // Fetch available modes on component mount
    useEffect(() => {
        fetchAvailableAgentModes();
    }, [fetchAvailableAgentModes]);

    return {
        threadAgentMode,
        agentModes,
        agentModesInfo,
        isUpdatingAgentMode,
        isButtonDisabled,
        buttonTooltipText,
        showButtonLoadingSpinner,
        updateThreadAgentMode,
        updatingAgentModeError,
        getAgentModeInfo,
    };
};
