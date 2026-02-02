import { Button, Card, Link, MessageBar, MessageBarBody, Spinner, Text } from '@fluentui/react-components';
import { CheckmarkCircle20Filled } from '@fluentui/react-icons';
import { useFormikContext } from 'formik';
import { useCallback, useContext, useEffect, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ExtendedAgentClient } from '../../../../../Common/Clients/ExtendedAgentClient';
import { resolveResourceIcon } from '../../../../../Common/Helpers/Resources';
import { useOAuthPopup } from '../../../../../Common/Hooks/useOAuthPopup';
import { ConnectorsResources } from '../../../../../Strings/SREAgentResources';
import { NameInput } from '../Common/NameInput';
import { useConnectorWizardStyles } from '../ConnectorWizard.styles';
import { ConnectorFormProps } from '../ConnectorWizardFormik';

interface GitHubConnectorFormProps {
    isEditMode?: boolean;
}

export const GitHubConnectorForm: React.FC<GitHubConnectorFormProps> = ({ isEditMode = false }) => {
    const intl = useIntl();
    const styles = useConnectorWizardStyles();
    const { values, setFieldValue } = useFormikContext<ConnectorFormProps>();
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const azPortalProxy = useContext(AzPortalContext);

    const [isLoadingConfig, setIsLoadingConfig] = useState(false);
    const [isAuthenticated, setIsAuthenticated] = useState(false);
    const [errorMessage, setErrorMessage] = useState<string | null>(null);
    const [oauthUrl, setOauthUrl] = useState<string | null>(null);

    const client = ExtendedAgentClient.getInstance(sreAgentEndpoint);

    // Load GitHub OAuth configuration
    const loadGitHubConfig = useCallback(async () => {
        setIsLoadingConfig(true);
        setErrorMessage(null);

        const response = await client.getGitHubOAuthConfig();
        if (!response.isSuccessful) {
            setErrorMessage(response.error || 'Failed to load GitHub configuration');
            azPortalProxy.log({
                action: 'loadGitHubConfig',
                actionModifier: 'failed',
                logLevel: 'error',
                data: { error: response.error },
            });
        } else {
            setOauthUrl(response.content?.oAuthUrl || null);
        }
        setIsLoadingConfig(false);
    }, [client, azPortalProxy]);

    // Check authentication status
    const checkAuthStatus = useCallback(async () => {
        if (!values.name) return;

        const response = await client.getConnectorStatus(values.name);
        if (response.isSuccessful) {
            setIsAuthenticated(response.content?.healthy || false);
        } else {
            // Connector might not exist yet, log at verbose level
            azPortalProxy.log({
                action: 'checkAuthStatus',
                actionModifier: 'failed',
                logLevel: 'verbose',
                data: { connectorName: values.name },
            });
        }
    }, [client, values.name, azPortalProxy]);

    useEffect(() => {
        loadGitHubConfig();
    }, [loadGitHubConfig]);

    useEffect(() => {
        if (isEditMode) {
            checkAuthStatus();
        }
    }, [isEditMode, checkAuthStatus]);

    const handleOAuthSuccess = useCallback(() => {
        setIsAuthenticated(true);
    }, []);

    const handleOAuthError = useCallback((error: string) => {
        setErrorMessage(error);
    }, []);

    const { openPopup, isAuthenticating } = useOAuthPopup({
        authUrl: oauthUrl || '',
        popupName: 'GitHubOAuth',
        messageType: 'github-oauth-complete',
        onSuccess: handleOAuthSuccess,
        onError: handleOAuthError,
        checkAuthStatus,
    });

    // Handle sign in with different account
    const handleSignInWithDifferentAccount = useCallback(() => {
        setIsAuthenticated(false);
        openPopup();
    }, [openPopup]);

    // Set default name to "github" if not in edit mode
    useEffect(() => {
        if (!isEditMode && !values.name) {
            setFieldValue('name', 'github');
        }
    }, [isEditMode, values.name, setFieldValue]);

    return (
        <>
            <NameInput disabled={isEditMode || true} />

            {errorMessage && (
                <MessageBar intent="error" role="alert" aria-live="assertive">
                    <MessageBarBody>{errorMessage}</MessageBarBody>
                </MessageBar>
            )}

            {isLoadingConfig ? (
                <div className={styles.signInLoading}>
                    <Spinner size="tiny" />
                    <Text>{intl.formatMessage(ConnectorsResources.loadingConfiguration)}</Text>
                </div>
            ) : !isAuthenticated ? (
                <div className={styles.signInLoading}>
                    <Button
                        appearance="primary"
                        onClick={openPopup}
                        disabled={!oauthUrl || isAuthenticating}
                        className={styles.outlookTeamsButton}
                    >
                        {intl.formatMessage(ConnectorsResources.signInToGitHub)}
                    </Button>
                    {isAuthenticating && (
                        <>
                            <Spinner size="tiny" />
                            <Text>{intl.formatMessage(ConnectorsResources.authenticating)}</Text>
                        </>
                    )}
                </div>
            ) : (
                <>
                    <Card className={styles.accountCard}>
                        <div className={styles.accountInfo}>
                            <img src={resolveResourceIcon('github')} alt="GitHub" width={24} height={24} />
                            <div className={styles.accountText}>
                                <span className={styles.connectedLabel}>{intl.formatMessage(ConnectorsResources.connectedToGitHub)}</span>
                            </div>
                        </div>
                        <CheckmarkCircle20Filled className={styles.checkmark} aria-hidden="true" />
                    </Card>
                    <div className={styles.signInLoading}>
                        <Link onClick={handleSignInWithDifferentAccount} className={styles.differentAccountSignInLoading}>
                            {intl.formatMessage(ConnectorsResources.signInWithDifferentGitHubAccount)}
                        </Link>
                    </div>
                </>
            )}
        </>
    );
};
