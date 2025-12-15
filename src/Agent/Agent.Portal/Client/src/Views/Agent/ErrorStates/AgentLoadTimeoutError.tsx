import { Body1, Button, makeStyles, MessageBar, MessageBarBody, Subtitle1, Text, tokens } from '@fluentui/react-components';
import { PlugDisconnectedRegular } from '@fluentui/react-icons';
import { memo, useCallback } from 'react';
import { useIntl } from 'react-intl';
import CopyButton from '../../../Common/Components/CopyButton';
import { TelemetrySource } from '../../../Common/Constants/Telemetry';
import { usePersistentNavigate } from '../../../Common/Hooks/usePersistentNavigate';
import { useTelemetry } from '../../../Common/Hooks/useTelemetry';
import { getSessionId } from '../../../Common/Utilities/SessionManager';
import { buildBladeUrl, IOpenBladeInfo } from '../../../Common/Utilities/Url';
import { PortalResources } from '../../../Strings/Resources';

const azureSreWildcardDomain = '*.azuresre.ai';

const useStyles = makeStyles({
    wrapper: {
        display: 'flex',
        flexDirection: 'column',
        height: '95vh',
        backgroundColor: tokens.colorNeutralBackground1,
    },
    container: {
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'center',
        alignItems: 'center',
        gap: tokens.spacingVerticalM,
        width: '100%',
        height: '100%',
        padding: tokens.spacingHorizontalXXL,
        boxSizing: 'border-box',
    },
    icon: {
        fontSize: '104px',
        color: tokens.colorNeutralForegroundDisabled,
    },
    title: {
        textAlign: 'center',
    },
    description: {
        textAlign: 'center',
        maxWidth: '600px',
    },
    messageBar: {
        maxWidth: '600px',
    },
    infoSection: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
        marginTop: tokens.spacingVerticalM,
        maxWidth: '600px',
        width: '100%',
    },
    infoRow: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: tokens.spacingHorizontalM,
        padding: tokens.spacingVerticalXS,
        backgroundColor: tokens.colorNeutralBackground2,
        borderRadius: tokens.borderRadiusMedium,
    },
    infoLabel: {
        fontWeight: tokens.fontWeightSemibold,
        minWidth: '100px',
    },
    infoValue: {
        flex: 1,
        wordBreak: 'break-all',
        overflowWrap: 'break-word',
    },
    copyButton: {
        minWidth: 'auto',
    },
    buttonGroup: {
        display: 'flex',
        gap: tokens.spacingHorizontalM,
        marginTop: tokens.spacingVerticalL,
        flexWrap: 'wrap',
        justifyContent: 'center',
    },
});

interface AgentLoadTimeoutErrorProps {
    resourceId?: string;
    agentSiteUrl?: string;
}

const AgentLoadTimeoutError = ({ resourceId, agentSiteUrl }: AgentLoadTimeoutErrorProps) => {
    const styles = useStyles();
    const intl = useIntl();
    const navigate = usePersistentNavigate();
    const { logEvent } = useTelemetry(TelemetrySource.AgentIFrameView, resourceId);

    const sessionId = getSessionId();

    const handleBrowseAgents = useCallback(() => {
        logEvent({
            action: 'agent-not-found',
            actionModifier: 'back-to-home',
        });

        navigate('/');
    }, [logEvent, navigate]);

    const handleGetSupport = useCallback(() => {
        const bladeInfo: IOpenBladeInfo = {
            extension: 'Microsoft_Azure_Support',
            detailBlade: 'Aurora.ReactView',
            detailBladeInputs: {
                resourceId,
            },
        };
        const bladeUrl = buildBladeUrl(bladeInfo);

        logEvent({
            action: 'agent-not-found',
            actionModifier: 'open-support',
        });

        window.open(bladeUrl, '_blank', 'noopener,noreferrer');
    }, [resourceId, logEvent]);

    return (
        <div className={styles.wrapper}>
            <div className={styles.container}>
                <PlugDisconnectedRegular className={styles.icon} />
                <Subtitle1 className={styles.title}>{intl.formatMessage(PortalResources.agentLoadTimeout)}</Subtitle1>
                <Body1 className={styles.description}>{intl.formatMessage(PortalResources.agentLoadTimeoutDescription)}</Body1>

                <MessageBar intent="warning" className={styles.messageBar}>
                    <MessageBarBody>
                        {intl.formatMessage(PortalResources.ensureNetworkAllows)}
                        {': '}
                        <Text weight="semibold">{azureSreWildcardDomain}</Text>
                    </MessageBarBody>
                </MessageBar>

                <div className={styles.infoSection}>
                    {agentSiteUrl && (
                        <div className={styles.infoRow}>
                            <span className={styles.infoLabel}>{intl.formatMessage(PortalResources.agentUrl)}:</span>
                            <span className={styles.infoValue}>{agentSiteUrl}</span>
                            <CopyButton textToCopy={agentSiteUrl} />
                        </div>
                    )}

                    {resourceId && (
                        <div className={styles.infoRow}>
                            <span className={styles.infoLabel}>{intl.formatMessage(PortalResources.resourceId)}:</span>
                            <span className={styles.infoValue}>{resourceId}</span>
                            <CopyButton textToCopy={resourceId} />
                        </div>
                    )}

                    <div className={styles.infoRow}>
                        <span className={styles.infoLabel}>{intl.formatMessage(PortalResources.sessionId)}:</span>
                        <span className={styles.infoValue}>{sessionId}</span>
                        <CopyButton textToCopy={sessionId} />
                    </div>
                </div>

                <div className={styles.buttonGroup}>
                    <Button appearance="primary" onClick={() => handleBrowseAgents()}>
                        {intl.formatMessage(PortalResources.backToHome)}
                    </Button>
                    {resourceId && (
                        <Button appearance="secondary" onClick={() => handleGetSupport()}>
                            {intl.formatMessage(PortalResources.getSupport)}
                        </Button>
                    )}
                </div>
            </div>
        </div>
    );
};

export default memo(AgentLoadTimeoutError);
