import { Button, makeStyles, Text, tokens } from '@fluentui/react-components';
import { ArrowClockwiseRegular, ChatHelpRegular, WarningRegular } from '@fluentui/react-icons';
import { useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { useRouteError } from 'react-router';
import GithubIssueDialog from '../../Space/Components/GithubIssueDialog';
import { GithubIssueIcon } from '../../Space/Components/Nav/FeedbackMenu';
import { GithubIssueResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { AzPortalContext } from '../AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../AzPortalProxy/Providers/StartupInfoContext';
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
    resourceIdContainer: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
        padding: tokens.spacingVerticalM,
        backgroundColor: tokens.colorNeutralBackground3,
        borderRadius: tokens.borderRadiusMedium,
    },
    resourceIdText: {
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
    const intl = useIntl();
    const styles = useStyles();
    const azPortalContext = useContext(AzPortalContext);
    const { resourceId, sessionId } = useContext(EnvironmentContext);

    const [isGithubIssueDialogOpen, setIsGithubIssueDialogOpen] = useState(false);

    const errorMessage = useMemo(() => {
        return typeof error?.message === 'string' ? error.message : JSON.stringify(error);
    }, [error]);

    const handleRefresh = useCallback(() => {
        window.location.reload();
    }, []);

    const handleOpenSupport = useCallback(() => {
        azPortalContext.openBlade({
            extension: 'Microsoft_Azure_Support',
            detailBlade: 'Aurora.ReactView',
            detailBladeInputs: {
                resourceId,
            },
        });
    }, [azPortalContext, resourceId]);

    const handleReportIssue = useCallback(() => {
        setIsGithubIssueDialogOpen(true);
    }, []);

    useEffect(() => {
        console.error(error);
        azPortalContext.log({
            action: 'route-error',
            actionModifier: 'error',
            resourceId,
            data: {
                error,
                errorMessage,
            },
        });
    }, [error, errorMessage, azPortalContext, resourceId]);

    return (
        <div className={styles.root} role="alert" aria-live="assertive">
            <WarningRegular className={styles.icon} fontSize={104} aria-hidden="true" />
            <Text size={500} weight="semibold" as="h2" style={{ margin: 0 }}>
                {intl.formatMessage(SreAgentResources.unexpectedErrorOccurred)}
            </Text>
            <Text size={400} style={{ color: tokens.colorNeutralForeground2, maxWidth: '600px', textAlign: 'center' }}>
                {errorMessage}
            </Text>
            {(resourceId || sessionId) && (
                <div className={styles.resourceIdContainer} style={{ flexDirection: 'column', alignItems: 'flex-start' }}>
                    {resourceId && (
                        <div style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS }}>
                            <Text size={300} style={{ color: tokens.colorNeutralForeground3 }}>
                                {intl.formatMessage(SreAgentResources.resourceId)}:
                            </Text>
                            <Text size={300} className={styles.resourceIdText}>
                                {resourceId}
                            </Text>
                            <CopyButton textToCopy={resourceId} buttonAppearance="transparent" />
                        </div>
                    )}
                    {sessionId && (
                        <div style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS }}>
                            <Text size={300} style={{ color: tokens.colorNeutralForeground3 }}>
                                {intl.formatMessage(SreAgentResources.sessionId)}:
                            </Text>
                            <Text size={300} className={styles.resourceIdText}>
                                {sessionId}
                            </Text>
                            <CopyButton textToCopy={sessionId} buttonAppearance="transparent" />
                        </div>
                    )}
                </div>
            )}
            <div className={styles.buttonContainer}>
                <Button appearance="primary" onClick={handleRefresh} icon={<ArrowClockwiseRegular />}>
                    {intl.formatMessage(SreAgentResources.refresh)}
                </Button>
                <Button onClick={handleOpenSupport} icon={<ChatHelpRegular />}>
                    {intl.formatMessage(SreAgentResources.getSupport)}
                </Button>
                <Button onClick={handleReportIssue} icon={<GithubIssueIcon />}>
                    {intl.formatMessage(GithubIssueResources.createGithubIssueTitle)}
                </Button>
            </div>
            <GithubIssueDialog isOpen={isGithubIssueDialogOpen} setIsOpen={setIsGithubIssueDialogOpen} />
        </div>
    );
};
