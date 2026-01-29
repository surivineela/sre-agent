import { useCallback, useContext, useMemo, useState } from 'react';
import { MessageDescriptor, useIntl } from 'react-intl';
import { AzPortalContext } from '../../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ExtendedAgentClient } from '../../../../Common/Clients/ExtendedAgentClient';
import { SreAgentResources } from '../../../../Strings/SREAgentResources';
import { ExtendedTool } from '../../../Contracts/ExtendedAgentGraph';

export type ToolType = 'kusto' | 'python';

interface ToolDeleteMessages {
    title: MessageDescriptor;
    inProgress: MessageDescriptor;
    success: MessageDescriptor;
    failure: MessageDescriptor;
}

const TOOL_DELETE_MESSAGES: Record<ToolType, ToolDeleteMessages> = {
    kusto: {
        title: SreAgentResources.deleteKustoToolNotificationTitle,
        inProgress: SreAgentResources.deleteKustoToolNotificationInProgress,
        success: SreAgentResources.deleteKustoToolNotificationSuccess,
        failure: SreAgentResources.deleteKustoToolNotificationFailure,
    },
    python: {
        title: SreAgentResources.deletePythonToolNotificationTitle,
        inProgress: SreAgentResources.deletePythonToolNotificationInProgress,
        success: SreAgentResources.deletePythonToolNotificationSuccess,
        failure: SreAgentResources.deletePythonToolNotificationFailure,
    },
};

interface UseToolTableDeleteProps {
    toolType: ToolType;
    selectedTools: ExtendedTool[];
    refresh: () => void;
}

interface UseToolTableDeleteResult {
    isDeleting: boolean;
    isDeleteDisabled: boolean;
    showDeleteConfirmationDialog: boolean;
    setShowDeleteConfirmationDialog: (show: boolean) => void;
    handleDelete: () => Promise<void>;
}

export const useToolTableDelete = ({ toolType, selectedTools, refresh }: UseToolTableDeleteProps): UseToolTableDeleteResult => {
    const intl = useIntl();
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const azPortalContext = useContext(AzPortalContext);
    const [showDeleteConfirmationDialog, setShowDeleteConfirmationDialog] = useState(false);
    const [isDeleting, setIsDeleting] = useState(false);
    const agentClient = useMemo(() => ExtendedAgentClient.getInstance(sreAgentEndpoint), [sreAgentEndpoint]);

    const messages = TOOL_DELETE_MESSAGES[toolType];
    const actionName = toolType === 'kusto' ? 'delete-tools' : 'delete-python-tools';

    const isDeleteDisabled = useMemo(() => selectedTools.length === 0 || isDeleting, [isDeleting, selectedTools.length]);

    const handleDelete = useCallback(async () => {
        setIsDeleting(true);
        setShowDeleteConfirmationDialog(false);
        const toolNames = selectedTools.map(tool => tool.name);

        azPortalContext.log({
            action: actionName,
            actionModifier: 'start',
            logLevel: 'info',
            data: { toolNames },
        });

        const notificationId = azPortalContext.startNotification(
            intl.formatMessage(messages.title, { count: selectedTools.length }),
            intl.formatMessage(messages.inProgress, {
                count: selectedTools.length,
                name: toolNames[0],
            })
        );

        const responses = await Promise.all(selectedTools.map(tool => agentClient.deleteTool(tool.name)));
        if (responses.some(response => response.isSuccessful)) {
            azPortalContext.log({
                action: actionName,
                actionModifier: 'success',
                logLevel: 'info',
                data: { toolNames },
            });

            await refresh();
            azPortalContext.stopNotification(
                notificationId,
                true,
                intl.formatMessage(messages.success, {
                    count: selectedTools.length,
                    name: toolNames[0],
                })
            );
        } else {
            const errorMessage = responses.find(r => !r.isSuccessful)?.error;
            azPortalContext.log({
                action: actionName,
                actionModifier: 'failure',
                logLevel: 'error',
                data: { toolNames, errorMessage },
            });

            azPortalContext.stopNotification(
                notificationId,
                false,
                intl.formatMessage(messages.failure, {
                    count: selectedTools.length,
                    name: toolNames[0],
                    errorMessage,
                })
            );
        }
        setIsDeleting(false);
    }, [agentClient, azPortalContext, intl, refresh, selectedTools, actionName, messages]);

    return {
        isDeleting,
        isDeleteDisabled,
        showDeleteConfirmationDialog,
        setShowDeleteConfirmationDialog,
        handleDelete,
    };
};
