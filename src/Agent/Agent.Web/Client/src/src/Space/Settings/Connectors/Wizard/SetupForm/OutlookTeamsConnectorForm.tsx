import { Button, Card, Link } from '@fluentui/react-components';
import { CheckmarkCircle20Filled } from '@fluentui/react-icons';
import { useFormikContext } from 'formik';
import { useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { EnvironmentContext } from '../../../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { OAuthPopup } from '../../../../../Common/Clients/OAuthPopupClient';
import { OAuthServiceClient } from '../../../../../Common/Clients/OAuthService';
import { FieldWrapper } from '../../../../../Common/Components/Field/FieldWrapper';
import InputFormik from '../../../../../Common/Components/Input/InputFormik';
import { MsiIdentity } from '../../../../../Common/Contracts/Azure/ArmObj';
import { ArmResourceDescriptor } from '../../../../../Common/Helpers/ResourceDescriptors';
import { resolveResourceIcon } from '../../../../../Common/Helpers/Resources';
import { ConnectorsResources } from '../../../../../Strings/SREAgentResources';
import { useApiConnection } from '../../Hooks/useApiConnection';
import { useConsentLink } from '../../Hooks/useConsentLink';
import { ConnectorType } from '../Common/ConnectorType';
import { ManagedIdentityDropdownWithValidation } from '../Common/ManagedIdentityDropdownWithValidation';
import { NameInput } from '../Common/NameInput';
import { parseTeamsChannelLink } from '../Common/TeamsConnectorHelper';
import { useConnectorWizardStyles } from '../ConnectorWizard.styles';
import { ConnectorFormProps } from '../ConnectorWizardFormik';

interface OutlookTeamsConnectorFormProps {
    userAssignedIdentities: { id: string; name: string }[];
    agentName: string | undefined;
    agentLocation: string | undefined;
    agentIdentity: MsiIdentity | undefined;
    refreshAgent: () => void;
    isEditMode?: boolean;
}

export const OutlookTeamsConnectorForm: React.FC<OutlookTeamsConnectorFormProps> = props => {
    const { agentName, agentIdentity, agentLocation, userAssignedIdentities, isEditMode = false } = props;

    const intl = useIntl();
    const styles = useConnectorWizardStyles();

    const { initialValues, values, setFieldValue } = useFormikContext<ConnectorFormProps>();
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

    const { apiConnection, apiConnectionLoaded, apiConnectionCreating, fetchApiConnection, createApiConnection } = useApiConnection();
    const { consentLink, consentLinkLoaded, fetchConsentLink, refreshConsentLink } = useConsentLink(`${agentName}-${connectorApiName}`);

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

    const loadData = useCallback(async () => {
        const fetchedConnection = await fetchApiConnection({
            subscriptionId: subscription,
            resourceGroup,
            agentName: agentName || '',
            connectionName: connectorApiName,
        });
        await fetchConsentLink();

        setFieldValue('email', fetchedConnection?.properties?.displayName || '');
        setFieldValue('url', fetchedConnection?.properties?.connectionRuntimeUrl || '');
    }, [agentName, connectorApiName, fetchApiConnection, fetchConsentLink, resourceGroup, setFieldValue, subscription]);

    useEffect(() => {
        if (isEditMode) {
            loadData();
        }
    }, [isEditMode, loadData]);

    const [displayChannelId, setDisplayChannelId] = useState<string>('');
    const [displayTeamsGroupId, setDisplayTeamsGroupId] = useState<string>('');

    useEffect(() => {
        if (isEditMode && connectorType === ConnectorType.TeamsSendNotification) {
            if (values.teamsChannelLink) {
                const parsedInfo = parseTeamsChannelLink(values.teamsChannelLink);
                if (parsedInfo) {
                    setFieldValue('channelId', parsedInfo.channelId);
                    setDisplayChannelId(parsedInfo.channelId);
                    setFieldValue('teamsGroupId', parsedInfo.teamsGroupId);
                    setDisplayTeamsGroupId(parsedInfo.teamsGroupId);
                } else {
                    setFieldValue('channelId', '');
                    setDisplayChannelId('');
                    setFieldValue('teamsGroupId', '');
                    setDisplayTeamsGroupId('');
                }
            } else {
                setFieldValue('channelId', initialValues.channelId);
                setDisplayChannelId(initialValues.channelId || '');
                setFieldValue('teamsGroupId', initialValues.teamsGroupId);
                setDisplayTeamsGroupId(initialValues.teamsGroupId || '');
            }
        }
    }, [isEditMode, values.teamsChannelLink, initialValues, setFieldValue, connectorType]);

    return (
        <>
            <NameInput disabled={isEditMode} />
            <FieldWrapper
                label={intl.formatMessage(ConnectorsResources.serviceAccount, { service: connectorService })}
                required
                orientation="vertical"
            >
                {isNotAuthenticated ? (
                    <Button
                        appearance="primary"
                        onClick={onSignInClick}
                        disabled={apiConnectionCreating || (isEditMode && (!apiConnectionLoaded || !consentLinkLoaded))}
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
            {connectorType === ConnectorType.TeamsSendNotification && (
                <>
                    <InputFormik
                        name="teamsChannelLink"
                        label={intl.formatMessage(ConnectorsResources.teamsChannelLink)}
                        required
                        orientation="vertical"
                        placeholder={intl.formatMessage(ConnectorsResources.teamsChannelLinkPlaceholder)}
                    />
                    {isEditMode && (
                        <div className={styles.teamsInfoContainer}>
                            <FieldWrapper label={intl.formatMessage(ConnectorsResources.channelId)} orientation="vertical">
                                <div className={styles.readOnlyValue}>{displayChannelId}</div>
                            </FieldWrapper>
                            <FieldWrapper label={intl.formatMessage(ConnectorsResources.teamsGroupId)} orientation="vertical">
                                <div className={styles.readOnlyValue}>{displayTeamsGroupId}</div>
                            </FieldWrapper>
                        </div>
                    )}
                </>
            )}
            <ManagedIdentityDropdownWithValidation
                userAssignedIdentities={userAssignedIdentities}
                agentIdentity={agentIdentity}
                refreshAgent={props.refreshAgent}
            />
        </>
    );
};
