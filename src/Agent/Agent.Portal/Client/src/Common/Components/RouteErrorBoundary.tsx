import { Button, makeStyles, Text, tokens } from '@fluentui/react-components';
import { ChatHelpRegular, HomeRegular, WarningRegular } from '@fluentui/react-icons';
import { useCallback, useEffect, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { useRouteError } from 'react-router-dom';
import { PortalResources } from '../../Strings/Resources';
import { TelemetrySource } from '../Constants/Telemetry';
import { LogLevel } from '../Contracts/Telemetry';
import { usePersistentNavigate } from '../Hooks/usePersistentNavigate';
import { useTelemetry } from '../Hooks/useTelemetry';
import { getSessionId } from '../Utilities/SessionManager';
import { buildBladeUrl, IOpenBladeInfo } from '../Utilities/Url';
import { CopyButton } from './CopyButton';

const useStyles = makeStyles({
    root: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        flex: 1,
        padding: tokens.spacingVerticalXXL,
        gap: tokens.spacingVerticalXXL,
        textAlign: 'center',
        height: '100vh',
        backgroundColor: tokens.colorNeutralBackground2,
    },
    icon: {
        color: tokens.colorNeutralForegroundDisabled,
    },
    sessionIdContainer: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
        padding: tokens.spacingVerticalM,
        backgroundColor: tokens.colorNeutralBackground3,
        borderRadius: tokens.borderRadiusMedium,
    },
    sessionIdText: {
        fontFamily: 'monospace',
        color: tokens.colorNeutralForeground2,
        fontSize: '14px',
    },
    buttonContainer: {
        display: 'flex',
        gap: tokens.spacingHorizontalM,
    },
});

export const RouteErrorBoundary = () => {
    const error = useRouteError() as Error;
    const navigate = usePersistentNavigate();
    const intl = useIntl();
    const styles = useStyles();
    const { logEvent } = useTelemetry(TelemetrySource.PortalLayout, undefined);

    const errorMessage = useMemo(() => {
        return typeof error?.message === 'string' ? error.message : JSON.stringify(error);
    }, [error]);

    const handleOpenSupport = useCallback(() => {
        let bladeUrl = '';
        const bladeInfo: IOpenBladeInfo = {
                extension: 'Microsoft_Azure_Support',
            detailBlade: 'HelpPane.ReactView',
            detailBladeInputs: {},
        };
        bladeUrl = buildBladeUrl(bladeInfo);

        window.open(bladeUrl, '_blank', 'noopener,noreferrer');
    }, []);

    useEffect(() => {
        console.error(error);
        logEvent({
            action: 'route-error',
            actionModifier: 'error',
            logLevel: LogLevel.Error,
            additionalData: {
                error,
            },
        });
    }, [error, logEvent]);

    return (
        <div className={styles.root} role="alert" aria-live="assertive">
            <WarningRegular className={styles.icon} fontSize={104} aria-hidden="true" />
            <Text size={500} weight="semibold" as="h2" style={{ margin: 0 }}>
                {intl.formatMessage(PortalResources.unexpectedErrorOccurred)}
            </Text>
            <Text size={400} style={{ color: tokens.colorNeutralForeground2, maxWidth: '600px', textAlign: 'center' }}>
                {errorMessage}
            </Text>
            <div className={styles.sessionIdContainer}>
                <Text size={300} style={{ color: tokens.colorNeutralForeground3 }}>
                    {intl.formatMessage(PortalResources.sessionId)}:
                </Text>
                <Text size={300} className={styles.sessionIdText}>
                    {getSessionId()}
                </Text>
                <CopyButton textToCopy={getSessionId()} buttonAppearance="subtle" />
            </div>
            <div className={styles.buttonContainer}>
                <Button appearance="primary" onClick={() => navigate('/')} icon={<HomeRegular />}>
                    {intl.formatMessage(PortalResources.backToHome)}
                </Button>
                <Button onClick={handleOpenSupport} icon={<ChatHelpRegular />}>
                    {intl.formatMessage(PortalResources.getSupport)}
                </Button>
            </div>
        </div>
    );
};
