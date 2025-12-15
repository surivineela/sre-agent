import { Body1, Button, makeStyles, Subtitle1, tokens } from '@fluentui/react-components';
import { BotFilled, LockClosedRegular } from '@fluentui/react-icons';
import { memo, useCallback } from 'react';
import { useIntl } from 'react-intl';
import CopyButton from '../../../Common/Components/CopyButton';
import { TelemetrySource } from '../../../Common/Constants/Telemetry';
import { usePersistentNavigate } from '../../../Common/Hooks/usePersistentNavigate';
import { useTelemetry } from '../../../Common/Hooks/useTelemetry';
import { getSessionId } from '../../../Common/Utilities/SessionManager';
import { buildBladeUrl, IOpenBladeInfo } from '../../../Common/Utilities/Url';
import { PortalResources } from '../../../Strings/Resources';
import { AgentLoadError } from '../Utilities';

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
        flex: 1,
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

interface AgentLoadArmErrorProps {
    agentLoadError: AgentLoadError;
    resourceId?: string;
}

const AgentLoadArmError = ({ agentLoadError, resourceId }: AgentLoadArmErrorProps) => {
    const styles = useStyles();
    const intl = useIntl();
    const navigate = usePersistentNavigate();
    const { logEvent } = useTelemetry(TelemetrySource.AgentIFrameView, resourceId);

    const sessionId = getSessionId();

    const title =
        agentLoadError.type === 'notFound'
            ? intl.formatMessage(PortalResources.agentNotFound)
            : agentLoadError.type === 'accessDenied'
              ? intl.formatMessage(PortalResources.agentAccessDenied)
              : intl.formatMessage(PortalResources.agentLoadError);
    const description =
        agentLoadError.type === 'notFound'
            ? intl.formatMessage(PortalResources.agentNotFoundDescription)
            : agentLoadError.type === 'accessDenied'
              ? intl.formatMessage(PortalResources.agentAccessDeniedDescription)
              : intl.formatMessage(PortalResources.agentLoadErrorDescription);

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
                {agentLoadError.type === 'accessDenied' ? (
                    <LockClosedRegular className={styles.icon} />
                ) : (
                    <BotFilled className={styles.icon} />
                )}
                <Subtitle1 className={styles.title}>{title}</Subtitle1>
                <Body1 className={styles.description}>{description}</Body1>

                <div className={styles.infoSection}>
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

                    {agentLoadError.message && (
                        <div className={styles.infoRow}>
                            <span className={styles.infoLabel}>{intl.formatMessage(PortalResources.errorDetails)}:</span>
                            <span className={styles.infoValue}>{agentLoadError.message}</span>
                            <CopyButton textToCopy={agentLoadError.message} />
                        </div>
                    )}
                </div>

                <div className={styles.buttonGroup}>
                    <Button appearance="primary" onClick={() => handleBrowseAgents()}>
                        {intl.formatMessage(PortalResources.backToHome)}
                    </Button>
                    <Button appearance="secondary" onClick={() => handleGetSupport()}>
                        {intl.formatMessage(PortalResources.getSupport)}
                    </Button>
                </div>
            </div>
        </div>
    );
};

export default memo(AgentLoadArmError);
