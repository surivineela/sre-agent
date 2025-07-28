import { useCallback, useContext, useMemo, useState } from 'react';
import useIntl from 'react-intl/src/components/useIntl';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ThreadClient } from '../../Common/Clients/ThreadClient';
import { AgentMode } from '../../Common/Contracts/Azure/SreAgent';
import { getAgentModeDescription, getAgentModeDisplayName } from '../../Common/Helpers/AgentMode';
import { AgentModeResources } from '../../Strings/SREAgentResources';
import { IAgentModeInfo, IAgentModeSelectorProps } from '../Contracts/Activities';
import { ThreadAgentModeContext } from '../Contracts/Context';

export const useAgentModeSelector = ({ threadId, disabled }: IAgentModeSelectorProps) => {
    const [isUpdatingAgentMode, setIsUpdatingAgentMode] = useState<boolean>(false);
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

    const isButtonDisabled = useMemo(() => {
        return isLoadingThreadAgentMode || isFetchingThreadAgentMode || !!fetchThreadAgentModeError || disabled;
    }, [isLoadingThreadAgentMode, isFetchingThreadAgentMode, fetchThreadAgentModeError, disabled]);

    const buttonTooltipText = useMemo(() => {
        return (
            (fetchThreadAgentModeError ? intl.formatMessage(AgentModeResources.fetchAgentModeFailureMessage) : undefined) ||
            intl.formatMessage(AgentModeResources.agentModeTooltip)
        );
    }, [fetchThreadAgentModeError]);

    const showButtonLoadingSpinner = useMemo(() => {
        return isLoadingThreadAgentMode || isFetchingThreadAgentMode;
    }, [isLoadingThreadAgentMode, isFetchingThreadAgentMode]);

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

    const agentModes = useMemo(
        (): IAgentModeInfo[] => [
            {
                name: AgentMode.readonly,
                displayName: getAgentModeDisplayName(AgentMode.readonly, intl),
                description: getAgentModeDescription(AgentMode.readonly, intl),
            },
            {
                name: AgentMode.review,
                displayName: getAgentModeDisplayName(AgentMode.review, intl),
                description: getAgentModeDescription(AgentMode.review, intl),
            },
            {
                name: AgentMode.autonomous,
                displayName: getAgentModeDisplayName(AgentMode.autonomous, intl),
                description: getAgentModeDescription(AgentMode.autonomous, intl),
            },
        ],
        [intl]
    );

    return {
        agentModes,
        threadAgentMode,
        isUpdatingAgentMode,
        isButtonDisabled,
        buttonTooltipText,
        showButtonLoadingSpinner,
        updateThreadAgentMode,
        updatingAgentModeError,
    };
};
