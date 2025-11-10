import { Button, makeStyles, Text, tokens } from '@fluentui/react-components';
import { ErrorCircleRegular } from '@fluentui/react-icons';
import { useEffect, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { useNavigate, useRouteError } from 'react-router-dom';
import { PortalResources } from '../../Strings/Resources';
import { TelemetrySource } from '../Constants/Telemetry';
import { LogLevel } from '../Contracts/Telemetry';
import { useTelemetry } from '../Hooks/useTelemetry';

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
        color: tokens.colorPaletteRedForeground1,
    },
});

export const RouteErrorBoundary = () => {
    const error = useRouteError() as Error;
    const navigate = useNavigate();
    const intl = useIntl();
    const styles = useStyles();
    const { logEvent } = useTelemetry(TelemetrySource.PortalLayout, undefined);

    const errorMessage = useMemo(() => {
        return typeof error?.message === 'string' ? error.message : JSON.stringify(error);
    }, [error]);

    useEffect(() => {
        console.error(error);
        logEvent({
            action: 'route-error',
            actionModifier: 'error',
            logLevel: LogLevel.Error,
            additionalData: {
                error,
                pathname: window.location.pathname,
            },
        });
    }, [error, logEvent]);

    return (
        <div className={styles.root} role="alert" aria-live="assertive">
            <ErrorCircleRegular className={styles.icon} fontSize={128} aria-hidden="true" />
            <Text size={900} weight="semibold" as="h1">
                {intl.formatMessage(PortalResources.unexpectedErrorOccurred)}
            </Text>
            <Text size={400} style={{ color: tokens.colorNeutralForeground2, maxWidth: '600px' }}>
                {errorMessage}
            </Text>
            <Button appearance="primary" onClick={() => navigate('/')}>
                {intl.formatMessage(PortalResources.backToHome)}
            </Button>
        </div>
    );
};
