import {
    Button,
    Card,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    Field,
    Link,
    MessageBar,
    MessageBarBody,
    Radio,
    RadioGroup,
    Spinner,
    Text,
} from '@fluentui/react-components';
import { CheckmarkCircle20Filled } from '@fluentui/react-icons';
import { useFormikContext } from 'formik';
import { useCallback, useContext, useEffect, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ExtendedAgentClient } from '../../../../../Common/Clients/ExtendedAgentClient';
import InputFormik from '../../../../../Common/Components/Input/InputFormik';
import { MsiIdentity } from '../../../../../Common/Contracts/Azure/ArmObj';
import { resolveResourceIcon } from '../../../../../Common/Helpers/Resources';
import { ConnectorsResources, SreAgentResources } from '../../../../../Strings/SREAgentResources';
import { ManagedIdentityDropdownWithValidation } from '../Common/ManagedIdentityDropdownWithValidation';
import { NameInput } from '../Common/NameInput';
import { useConnectorWizardStyles } from '../ConnectorWizard.styles';
import { ConnectorFormProps } from '../ConnectorWizardFormik';

enum AzureDevOpsAuthMode {
    UserAccount = 'UserAccount',
    ManagedIdentity = 'ManagedIdentity',
}

interface AzureDevOpsConnectorFormProps {
    userAssignedIdentities: { id: string; name: string }[];
    agentIdentity: MsiIdentity | undefined;
    refreshAgent: () => void;
    isEditMode?: boolean;
}

export const AzureDevOpsConnectorForm: React.FC<AzureDevOpsConnectorFormProps> = ({
    userAssignedIdentities,
    agentIdentity,
    refreshAgent,
    isEditMode = false,
}) => {
    const intl = useIntl();
    const styles = useConnectorWizardStyles();
    const { values, setFieldValue, setFieldError } = useFormikContext<ConnectorFormProps>();
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const azPortalProxy = useContext(AzPortalContext);

    const [authMode, setAuthMode] = useState<AzureDevOpsAuthMode>(
        values.identity ? AzureDevOpsAuthMode.ManagedIdentity : AzureDevOpsAuthMode.UserAccount
    );
    const [isAuthenticated, setIsAuthenticated] = useState(false);
    const [isAuthenticating, setIsAuthenticating] = useState(false);
    const [errorMessage, setErrorMessage] = useState<string | null>(null);
    const [showResultDialog, setShowResultDialog] = useState(false);
    const [resultMessage, setResultMessage] = useState<{ success: boolean; message: string } | null>(null);

    const client = ExtendedAgentClient.getInstance(sreAgentEndpoint);

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
        if (isEditMode) {
            checkAuthStatus();
        }
    }, [isEditMode, checkAuthStatus]);

    // Handle OAuth sign-in
    const handleSignIn = useCallback(async () => {
        setIsAuthenticating(true);
        setErrorMessage(null);

        azPortalProxy.logAmplitudeControlEvent({
            targetType: 'button',
            targetAction: 'clicked',
            targetName: 'signInToAzureDevOps',
            targetFriendlyName: 'Sign in to Azure DevOps',
            valueObjectName: 'oauth-signin',
            valueObjectFriendlyName: 'OAuth Sign In',
        });

        const response = await client.completeAzureDevOpsOAuth(values.azureDevOpsOrganization || '');

        if (response.isSuccessful && response.content) {
            setIsAuthenticated(true);
            setResultMessage({
                success: true,
                message: response.content.message,
            });
            setShowResultDialog(true);

            azPortalProxy.log({
                action: 'azureDevOpsOAuth',
                actionModifier: 'succeeded',
                logLevel: 'info',
                data: { connectorName: values.name },
            });
        } else {
            const errorMsg = response.error || 'Failed to authenticate with Azure DevOps';
            setErrorMessage(errorMsg);
            setResultMessage({
                success: false,
                message: errorMsg,
            });
            setShowResultDialog(true);

            azPortalProxy.log({
                action: 'azureDevOpsOAuth',
                actionModifier: 'failed',
                logLevel: 'error',
                data: { error: errorMsg, connectorName: values.name },
            });
        }

        setIsAuthenticating(false);
    }, [client, values.azureDevOpsOrganization, values.name, azPortalProxy]);

    // Handle sign in with different account
    const handleSignInWithDifferentAccount = useCallback(() => {
        setIsAuthenticated(false);
        handleSignIn();
    }, [handleSignIn]);

    // Close result dialog
    const handleCloseResultDialog = useCallback(() => {
        setShowResultDialog(false);
        setResultMessage(null);
    }, []);

    // Validate identity when in Managed Identity mode
    useEffect(() => {
        if (authMode === AzureDevOpsAuthMode.ManagedIdentity) {
            if (!values.identity) {
                setFieldError('identity', intl.formatMessage(SreAgentResources.fieldRequired));
            } else {
                setFieldError('identity', undefined);
            }
        } else {
            // Clear any identity errors when in User Account mode
            setFieldError('identity', undefined);
        }
    }, [authMode, values.identity, setFieldError, intl]);

    return (
        <>
            <NameInput disabled={isEditMode} />

            <InputFormik
                name="azureDevOpsOrganization"
                label={intl.formatMessage(ConnectorsResources.organization)}
                required
                orientation="vertical"
                placeholder={intl.formatMessage(ConnectorsResources.organizationPlaceholder)}
                disabled={isEditMode}
            />

            <Field label={intl.formatMessage(ConnectorsResources.authenticationMethod)} required orientation="vertical">
                <RadioGroup
                    layout="horizontal"
                    value={authMode}
                    onChange={(_, data) => {
                        const newMode = data.value as AzureDevOpsAuthMode;
                        setAuthMode(newMode);
                        if (newMode === AzureDevOpsAuthMode.UserAccount) {
                            // Clear identity when switching to User Account mode
                            setFieldValue('identity', '');
                            setFieldValue('useManagedIdentityAsFic', false);
                            setIsAuthenticated(false);
                        }
                    }}
                >
                    <Radio value={AzureDevOpsAuthMode.UserAccount} label={intl.formatMessage(ConnectorsResources.userAccount)} />
                    <Radio value={AzureDevOpsAuthMode.ManagedIdentity} label={intl.formatMessage(ConnectorsResources.managedIdentity)} />
                </RadioGroup>
            </Field>

            {authMode === AzureDevOpsAuthMode.UserAccount && (
                <>
                    {errorMessage && (
                        <MessageBar intent="error" role="alert" aria-live="assertive">
                            <MessageBarBody>{errorMessage}</MessageBarBody>
                        </MessageBar>
                    )}

                    {!isAuthenticated ? (
                        <div className={styles.signInLoading}>
                            <Button
                                appearance="primary"
                                onClick={handleSignIn}
                                disabled={isAuthenticating || !values.azureDevOpsOrganization}
                                className={styles.outlookTeamsButton}
                            >
                                {intl.formatMessage(ConnectorsResources.signInToAzureDevOps)}
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
                                    <img src={resolveResourceIcon('AzureDevOps')} alt="Azure DevOps" width={24} height={24} />
                                    <div className={styles.accountText}>
                                        <span className={styles.connectedLabel}>
                                            {intl.formatMessage(ConnectorsResources.connectedToAzureDevOps)}
                                        </span>
                                    </div>
                                </div>
                                <CheckmarkCircle20Filled className={styles.checkmark} aria-hidden="true" />
                            </Card>
                            <div className={styles.signInLoading}>
                                <Link onClick={handleSignInWithDifferentAccount} className={styles.differentAccountSignInLoading}>
                                    {intl.formatMessage(ConnectorsResources.signInWithDifferentAzureDevOpsAccount)}
                                </Link>
                            </div>
                        </>
                    )}
                </>
            )}

            {authMode === AzureDevOpsAuthMode.ManagedIdentity && (
                <ManagedIdentityDropdownWithValidation
                    userAssignedIdentities={userAssignedIdentities}
                    agentIdentity={agentIdentity}
                    refreshAgent={refreshAgent}
                    showFicFields={true}
                />
            )}

            <Dialog open={showResultDialog} onOpenChange={(_, data) => data.open || handleCloseResultDialog()}>
                <DialogSurface>
                    <DialogBody>
                        <DialogTitle>
                            {resultMessage?.success
                                ? intl.formatMessage(ConnectorsResources.connectedToAzureDevOps)
                                : intl.formatMessage(ConnectorsResources.authenticationFailed)}
                        </DialogTitle>
                        <DialogContent>
                            <Text>{resultMessage?.message}</Text>
                        </DialogContent>
                        <DialogActions>
                            <Button appearance="primary" onClick={handleCloseResultDialog}>
                                {intl.formatMessage(SreAgentResources.close)}
                            </Button>
                        </DialogActions>
                    </DialogBody>
                </DialogSurface>
            </Dialog>
        </>
    );
};
