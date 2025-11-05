import { Button, Card, Link } from '@fluentui/react-components';
import { CheckmarkCircle20Filled } from '@fluentui/react-icons';
import { useFormikContext } from 'formik';
import { useCallback, useContext, useEffect, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { EnvironmentContext } from '../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { OAuthPopup } from '../../../Common/Clients/OAuthPopupClient';
import { OAuthServiceClient } from '../../../Common/Clients/OAuthService';
import { FieldWrapper } from '../../../Common/Components/Field/FieldWrapper';
import { ArmResourceDescriptor } from '../../../Common/Helpers/ResourceDescriptors';
import { resolveResourceIcon } from '../../../Common/Helpers/Resources';
import { ConnectorsResources } from '../../../Strings/SREAgentResources';
import { ConnectorType } from './ConnectorType';
import { ConnectorWithManagedIdentityFormBase, ConnectorWithManagedIdentityFormBaseProps } from './ConnectorWithManagedIdentityFormBase';
import { useConnectorWizardStyles } from './ConnectorWizard.styles';
import { ConnectorFormProps } from './ConnectorWizardFormik';
import { useApiConnection } from './useApiConnection';
import { useConsentLink } from './useConsentLink';

export type OutlookTeamsConnectorFormProps = Omit<ConnectorWithManagedIdentityFormBaseProps, 'children'> & {
    agentName?: string;
    agentLocation?: string;
};

export const OutlookTeamsConnectorForm: React.FC<OutlookTeamsConnectorFormProps> = props => {
    const { selectedConnector, agentName, agentIdentity, agentLocation } = props;

    const intl = useIntl();
    const styles = useConnectorWizardStyles();

    const { values, setFieldValue } = useFormikContext<ConnectorFormProps>();
    const { resourceId } = useContext(EnvironmentContext);

    const { subscription, resourceGroup } = new ArmResourceDescriptor(resourceId);

    const connectorType = useMemo(() => {
        return selectedConnector ? (selectedConnector.dataConnectorType as ConnectorType) : (values.connectorType as ConnectorType);
    }, [selectedConnector, values.connectorType]);

    const connectorService = useMemo(() => {
        return connectorType === ConnectorType.OutlookSendEmail
            ? intl.formatMessage(ConnectorsResources.outlook)
            : intl.formatMessage(ConnectorsResources.microsoftTeams);
    }, [connectorType, intl]);

    const connectorIconName = useMemo(() => {
        return connectorType === ConnectorType.OutlookSendEmail ? 'outlook' : 'teams';
    }, [connectorType]);

    const connectorApiName = useMemo(() => {
        return connectorType === ConnectorType.OutlookSendEmail ? 'office365' : 'teams';
    }, [connectorType]);

    const {
        apiConnection,
        apiConnectionLoading,
        apiConnectionLoaded,
        apiConnectionLoadFailure,
        apiConnectionCreating,
        apiConnectionCreateFailure,
        fetchApiConnection,
        createApiConnection,
    } = useApiConnection();
    const { consentLink, fetchConsentLink } = useConsentLink(connectorApiName);

    useEffect(() => {
        if (!apiConnectionLoaded && !apiConnectionLoadFailure) {
            fetchApiConnection({ subscriptionId: subscription, resourceGroup, connectionName: connectorApiName }).then(apiConnection => {
                setFieldValue('email', apiConnection?.properties?.displayName || '');
                setFieldValue('url', apiConnection?.properties?.connectionRuntimeUrl || '');
            });
        }
    }, [
        apiConnectionLoadFailure,
        apiConnectionLoaded,
        apiConnectionLoading,
        connectorApiName,
        fetchApiConnection,
        resourceGroup,
        setFieldValue,
        subscription,
    ]);

    useEffect(() => {
        if (apiConnection && !apiConnectionCreateFailure) {
            fetchConsentLink();
        }
    }, [apiConnection, apiConnectionCreateFailure, fetchConsentLink]);

    const onSignInClick = useCallback(async () => {
        let apiConnectionLocal = apiConnection;
        let consentLinkObject = consentLink;
        if (!apiConnectionLocal) {
            apiConnectionLocal = await createApiConnection({
                subscriptionId: subscription,
                resourceGroup,
                connectionName: connectorApiName,
                location: agentLocation || '',
                agentName: agentName || '',
                tenantId: agentIdentity?.tenantId || '',
                objectId: agentIdentity?.principalId || '',
            });
            consentLinkObject = await fetchConsentLink();
        }

        const oauthPopupClient = new OAuthPopup({ consentUrl: consentLinkObject?.link || '' });

        const loginResponse = await oauthPopupClient.loginPromise;
        if (loginResponse.error) {
            throw new Error(atob(loginResponse.error));
        }
        if (loginResponse.code) {
            await OAuthServiceClient.confirmConsentCodeForConnection({
                subscriptionId: subscription,
                resourceGroup,
                connectionName: connectorApiName,
                code: loginResponse.code,
                tenantId: agentIdentity?.tenantId || '',
                objectId: agentIdentity?.principalId || '',
            });
        }

        const fetchedConnection = await fetchApiConnection({
            subscriptionId: subscription,
            resourceGroup,
            connectionName: connectorApiName,
        });

        if (fetchedConnection) {
            await OAuthServiceClient.testConnection(fetchedConnection);
        }

        setFieldValue('email', fetchedConnection?.properties?.displayName || '');
        setFieldValue('url', fetchedConnection?.properties?.connectionRuntimeUrl || '');
        return { connection: fetchedConnection };
    }, [
        agentIdentity,
        agentLocation,
        agentName,
        apiConnection,
        connectorApiName,
        consentLink,
        createApiConnection,
        fetchApiConnection,
        fetchConsentLink,
        resourceGroup,
        setFieldValue,
        subscription,
    ]);

    return (
        <ConnectorWithManagedIdentityFormBase {...props}>
            <FieldWrapper
                label={intl.formatMessage(ConnectorsResources.serviceAccount, { service: connectorService })}
                required
                orientation="vertical"
            >
                {!apiConnection || !consentLink || consentLink.status === 'Unauthenticated' ? (
                    <Button
                        appearance="primary"
                        onClick={onSignInClick}
                        disabled={apiConnectionLoading || apiConnectionLoaded || !!apiConnectionCreateFailure || apiConnectionCreating}
                        className={styles.outlookTeamsButton}
                    >
                        {intl.formatMessage(ConnectorsResources.signInToService, { service: connectorService })}
                    </Button>
                ) : (
                    <>
                        <Card className={styles.accountCard}>
                            <div className={styles.accountInfo}>
                                <img src={resolveResourceIcon(connectorIconName)} alt={connectorService} width={24} height={24} />
                                <div className={styles.accountText}>
                                    <span className={styles.connectedLabel}>{intl.formatMessage(ConnectorsResources.connectedAs)}</span>
                                    <span className={styles.accountEmail}>{apiConnection?.properties?.displayName || ''}</span>
                                </div>
                            </div>
                            <CheckmarkCircle20Filled className={styles.checkmark} />
                        </Card>
                        <Link onClick={onSignInClick} className={styles.signInDifferent}>
                            {intl.formatMessage(ConnectorsResources.signInWithDifferentAccount)}
                        </Link>
                    </>
                )}
            </FieldWrapper>
        </ConnectorWithManagedIdentityFormBase>
    );
};
