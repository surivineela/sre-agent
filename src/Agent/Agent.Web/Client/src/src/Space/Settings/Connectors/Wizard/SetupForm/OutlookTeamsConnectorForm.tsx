import { Button, Card, Link } from '@fluentui/react-components';
import { CheckmarkCircle20Filled } from '@fluentui/react-icons';
import { useFormikContext } from 'formik';
import { useCallback, useContext, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { EnvironmentContext } from '../../../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { OAuthPopup } from '../../../../../Common/Clients/OAuthPopupClient';
import { OAuthServiceClient } from '../../../../../Common/Clients/OAuthService';
import { FieldWrapper } from '../../../../../Common/Components/Field/FieldWrapper';
import { MsiIdentity } from '../../../../../Common/Contracts/Azure/ArmObj';
import { Connector } from '../../../../../Common/Contracts/Azure/SreAgent';
import { ArmResourceDescriptor } from '../../../../../Common/Helpers/ResourceDescriptors';
import { resolveResourceIcon } from '../../../../../Common/Helpers/Resources';
import { ConnectorsResources } from '../../../../../Strings/SREAgentResources';
import { useConnectorWizardStyles } from '../../ConnectorWizard.styles';
import { ConnectorFormProps } from '../../ConnectorWizardFormik';
import { useApiConnection } from '../../useApiConnection';
import { useConsentLink } from '../../useConsentLink';
import { ConnectorType } from '../Common/ConnectorType';
import { ManagedIdentityDropdownWithValidation } from '../Common/ManagedIdentityDropdownWithValidation';
import { NameInputWithValidation } from '../Common/NameInputWithValidation';
import { SetupConnectorFormWrapper } from '../Common/SetupConnectorFormWrapper';

interface OutlookTeamsConnectorFormProps {
    userAssignedIdentities: { id: string; name: string }[];
    agentName: string | undefined;
    agentLocation: string | undefined;
    agentIdentity: MsiIdentity | undefined;
    existingConnectors: Connector[] | undefined;
    refreshAgent: () => void;
    isEditMode?: boolean;
}

export const OutlookTeamsConnectorForm: React.FC<OutlookTeamsConnectorFormProps> = props => {
    const { agentName, agentIdentity, agentLocation, existingConnectors, userAssignedIdentities, isEditMode = false } = props;

    const intl = useIntl();
    const styles = useConnectorWizardStyles();

    const { values, setFieldValue } = useFormikContext<ConnectorFormProps>();
    const { resourceId } = useContext(EnvironmentContext);

    const { subscription, resourceGroup } = new ArmResourceDescriptor(resourceId);

    const connectorType = useMemo(() => values.connectorType as ConnectorType, [values.connectorType]);

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

    const { apiConnection, apiConnectionCreating, fetchApiConnection, createApiConnection } = useApiConnection();
    const { consentLink, fetchConsentLink, refreshConsentLink } = useConsentLink(`${agentName}-${connectorApiName}`);

    const onSignInClick = useCallback(async () => {
        await createApiConnection({
            subscriptionId: subscription,
            resourceGroup,
            connectionName: connectorApiName,
            location: agentLocation || '',
            agentName: agentName || '',
            tenantId: agentIdentity?.tenantId || '',
            objectId: agentIdentity?.principalId || '',
        });
        const consentLinkObject = await fetchConsentLink();
        if (consentLinkObject?.link) {
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
                agentName: agentName || '',
                connectionName: connectorApiName,
            });
            await refreshConsentLink();

            if (fetchedConnection) {
                await OAuthServiceClient.testConnection(fetchedConnection);
            }

            setFieldValue('email', fetchedConnection?.properties?.displayName || '');
            setFieldValue('url', fetchedConnection?.properties?.connectionRuntimeUrl || '');
        }
    }, [
        agentIdentity,
        agentLocation,
        agentName,
        connectorApiName,
        createApiConnection,
        fetchApiConnection,
        fetchConsentLink,
        refreshConsentLink,
        resourceGroup,
        setFieldValue,
        subscription,
    ]);

    const isNotAuthenticated = useMemo(
        () => !apiConnection || !consentLink || consentLink.status === 'Unauthenticated',
        [apiConnection, consentLink]
    );

    return (
        <SetupConnectorFormWrapper>
            <NameInputWithValidation disabled={isEditMode} existingConnectors={existingConnectors} />
            <FieldWrapper
                label={intl.formatMessage(ConnectorsResources.serviceAccount, { service: connectorService })}
                required
                orientation="vertical"
            >
                {isNotAuthenticated ? (
                    <Button
                        appearance="primary"
                        onClick={onSignInClick}
                        disabled={apiConnectionCreating}
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
                                    <span className={styles.accountEmail}>{apiConnection?.properties?.authenticatedUser?.name || ''}</span>
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
            <ManagedIdentityDropdownWithValidation
                userAssignedIdentities={userAssignedIdentities}
                agentIdentity={agentIdentity}
                refreshAgent={props.refreshAgent}
            />
        </SetupConnectorFormWrapper>
    );
};
