import { Card, makeStyles, MessageBar, MessageBarBody, MessageBarTitle, Spinner, Text, tokens } from '@fluentui/react-components';
import { useCallback, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { useNavigate, useParams } from 'react-router-dom';
import { AgentSpaceClient } from '../../Common/Clients/AgentSpaceClient';
import { ViewResourceJsonDialog } from '../../Common/Components/ViewResourceJsonDialog/ViewResourceJsonDialog';
import { TelemetrySource } from '../../Common/Constants/Telemetry';
import { AmplitudeContextProvider } from '../../Common/Contexts/AmplitudeContext';
import { useNotifications } from '../../Common/Contexts/NotificationContext';
import { ProductName } from '../../Common/Contracts/Amplitude';
import { useIsInternal } from '../../Common/Hooks/useIsInternal';
import { parseArmId } from '../../Common/Utilities/ArmId';
import { getArmErrorMessage } from '../../Common/Utilities/Client';
import { PortalResources } from '../../Strings/Resources';
import { AgentSpaceConfiguration } from './AgentSpaceConfiguration';
import { AgentSpaceOverview } from './AgentSpaceOverview';
import { AddAgentDialog } from './Components/AddAgentDialog/AddAgentDialog';
import { AgentSpaceNav } from './Components/AgentSpaceNav';
import { Connectors } from './Connectors/Connectors';
import { DeleteAgentSpaceDetailDialog } from './DeleteAgentSpaceDetailDialog';
import { GenevaActionPolicies } from './GenevaActionPolicies';
import { useAgentSpace } from './Hooks/useAgentSpace';
import { AgentSpaceNavItem, useAgentSpaceNav } from './Hooks/useAgentSpaceNav';

const useStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'row',
        height: '100%',
        backgroundColor: tokens.colorNeutralBackground2,
    },
    content: {
        flex: 1,
        overflow: 'auto',
        padding: '24px 32px',
    },
    loadingContainer: {
        display: 'flex',
        justifyContent: 'center',
        alignItems: 'center',
        height: '100%',
        width: '100%',
    },
    errorContainer: {
        padding: '24px 32px',
        width: '100%',
    },
});

const AgentSpaceViewContent = ({ resourceId }: { resourceId: string }) => {
    const intl = useIntl();
    const styles = useStyles();
    const navigate = useNavigate();
    const { isInternalTenant, isInternalDevTenant } = useIsInternal();
    const { start, succeed, fail } = useNotifications();

    const { isNavOpen, selectedView, setSelectedView, toggleNav } = useAgentSpaceNav();
    const [showDeleteDialog, setShowDeleteDialog] = useState(false);
    const [showJsonDialog, setShowJsonDialog] = useState(false);
    const [showAddAgentDialog, setShowAddAgentDialog] = useState(false);

    const {
        agentSpace,
        memberAgents,
        connectors,
        isLoading,
        isLoadingAgents,
        isLoadingConnectors,
        error,
        refresh,
        refreshAgents,
        refreshConnectors,
        startAgents,
        stopAgents,
        removeAgentsFromSpace,
        createConnector,
        updateConnector,
        deleteConnectors,
    } = useAgentSpace(resourceId);

    const showInternalTabs = useMemo(() => isInternalTenant || isInternalDevTenant, [isInternalTenant, isInternalDevTenant]);

    const parsedId = useMemo(() => parseArmId(resourceId), [resourceId]);
    const spaceName = parsedId.resourceName || 'Agent Space';

    const agentSpaceClient = useMemo(() => AgentSpaceClient.getInstance(TelemetrySource.AgentSpaceView), []);

    const handleDelete = useCallback(async () => {
        const notificationId = start(
            intl.formatMessage(PortalResources.deleteAgentSpace),
            intl.formatMessage(PortalResources.deleteAgentSpaceInProgress)
        );

        const response = await agentSpaceClient.deleteAgentSpace(resourceId);

        if (response.isSuccessful) {
            succeed(
                notificationId,
                intl.formatMessage(PortalResources.deleteAgentSpace),
                intl.formatMessage(PortalResources.deleteAgentSpaceSuccess, { name: spaceName })
            );
            navigate('/');
        } else {
            const errorDetail = getArmErrorMessage(response.error);
            fail(
                notificationId,
                intl.formatMessage(PortalResources.deleteAgentSpace),
                errorDetail
                    ? intl.formatMessage(PortalResources.deleteAgentSpaceErrorDetail, {
                          name: spaceName,
                          error: errorDetail,
                      })
                    : intl.formatMessage(PortalResources.deleteAgentSpaceError)
            );
        }
    }, [agentSpaceClient, resourceId, spaceName, intl, start, succeed, fail, navigate]);

    if (isLoading) {
        return (
            <div className={styles.loadingContainer}>
                <Spinner size="large" label={intl.formatMessage(PortalResources.loading)} />
            </div>
        );
    }

    if (error) {
        return (
            <div className={styles.errorContainer}>
                <MessageBar intent="error">
                    <MessageBarBody>
                        <MessageBarTitle>{intl.formatMessage(PortalResources.requestError)}</MessageBarTitle>
                        <Text>{error}</Text>
                    </MessageBarBody>
                </MessageBar>
            </div>
        );
    }

    return (
        <div className={styles.container}>
            <AgentSpaceNav
                isNavOpen={isNavOpen}
                selectedView={selectedView}
                onSelectView={setSelectedView}
                onToggle={toggleNav}
                showInternalTabs={showInternalTabs}
                resourceId={resourceId}
            />

            <div className={styles.content}>
                <Card>
                    {selectedView === AgentSpaceNavItem.Overview && (
                        <AgentSpaceOverview
                            agentSpace={agentSpace}
                            memberAgents={memberAgents}
                            isLoadingAgents={isLoadingAgents}
                            refreshAgents={refreshAgents}
                            onRefresh={refresh}
                            onViewJson={() => setShowJsonDialog(true)}
                            onDelete={() => setShowDeleteDialog(true)}
                            onAddAgent={() => setShowAddAgentDialog(true)}
                            onStartAgents={startAgents}
                            onStopAgents={stopAgents}
                            onRemoveAgents={removeAgentsFromSpace}
                        />
                    )}

                    {selectedView === AgentSpaceNavItem.Configuration && (
                        <AgentSpaceConfiguration agentSpace={agentSpace} refresh={refresh} />
                    )}

                    {selectedView === AgentSpaceNavItem.GenevaActionPolicies && showInternalTabs && (
                        <GenevaActionPolicies agentSpace={agentSpace} refresh={refresh} />
                    )}

                    {selectedView === AgentSpaceNavItem.Connectors && (
                        <Connectors
                            spaceResourceId={resourceId}
                            agentSpace={agentSpace}
                            connectors={connectors}
                            isLoading={isLoadingConnectors}
                            refresh={refreshConnectors}
                            createConnector={createConnector}
                            updateConnector={updateConnector}
                            deleteConnectors={deleteConnectors}
                        />
                    )}
                </Card>
            </div>

            <DeleteAgentSpaceDetailDialog open={showDeleteDialog} onClose={() => setShowDeleteDialog(false)} onConfirm={handleDelete} />

            <ViewResourceJsonDialog
                open={showJsonDialog}
                resourceId={resourceId}
                telemetrySource={TelemetrySource.AgentSpaceView}
                onClose={() => setShowJsonDialog(false)}
            />

            <AddAgentDialog
                isOpen={showAddAgentDialog}
                onClose={() => setShowAddAgentDialog(false)}
                spaceResourceId={resourceId}
                spaceLocation={agentSpace?.location || ''}
                spaceName={spaceName}
                maxAgentCount={agentSpace?.properties?.maxAgentCount || 9}
                currentAgentCount={agentSpace?.properties?.currentAgentCount || memberAgents.length}
                onAgentsAdded={refreshAgents}
            />
        </div>
    );
};

export const AgentSpaceView = () => {
    const { spaceId: encodedSpaceId } = useParams<{ spaceId: string }>();

    const resourceId = useMemo(() => decodeURIComponent(encodedSpaceId ?? ''), [encodedSpaceId]);

    return (
        <AmplitudeContextProvider
            resourceId={resourceId}
            productName={ProductName.SreAgent}
            telemetrySource={TelemetrySource.AgentSpaceView}
        >
            <AgentSpaceViewContent resourceId={resourceId} />
        </AmplitudeContextProvider>
    );
};
