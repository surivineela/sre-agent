import { Formik } from 'formik';
import { FC, useContext, useEffect, useState } from 'react';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { IncidentHandlerClient } from '../../Common/Clients/IncidentHandlerClient';
import { IncidentManagementType } from '../../Common/Contracts/Azure/SreAgent';
import { IncidentManagementFormValues, IncidentManagementSettingsProps } from '../Contracts/IncidentManagement';
import { useIncidentManagementStyles } from '../Styles/IncidentManagement.styles';
import { useIncidentManagementSettings } from './Hooks/useIncidentManagementSettings';
import IncidentManagementForm from './IncidentManagementForm';

const IncidentManagementSettings: FC<IncidentManagementSettingsProps> = ({ integrated, close, keepOpen }) => {
    const styles = useIncidentManagementStyles();
    const { loading, loaded, loadFailure, saving, saveFailure, initialValues, save, disconnect, validate, agent } =
        useIncidentManagementSettings(close);
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const [effectiveManagedIdentity, setEffectiveManagedIdentity] = useState<string>('');
    const [isUsingAgentSpaceIdentity, setIsUsingAgentSpaceIdentity] = useState<boolean>(false);

    // Get effective managed identity (Agent Space if available, otherwise agent identity)
    // currently only for ICM platform
    useEffect(() => {
        const getEffectiveManagedIdentity = async () => {
            if (initialValues.platform === IncidentManagementType.Icm) {
                try {
                    const client = IncidentHandlerClient.getInstance(sreAgentEndpoint, () => {});
                    const response = await client.getAgentSpaceIdentity(IncidentManagementType.Icm);

                    // Use Agent Space identity if available, otherwise fall back to agent identity
                    const agentSpaceIdentity = response.isSuccessful && response.content ? response.content : null;
                    // const agentSpaceIdentity = "/subscriptions/d1a91e0b-79f7-4318-9a09-0ba93b569ce1/resourceGroups/jijohn-sreagent/providers/Microsoft.ManagedIdentity/userAssignedIdentities/gpt5-jijohn-bugbash-ame-03-ir6f72spmuhio";
                    const agentIdentity = agent?.properties?.actionConfiguration?.identity || '';

                    if (agentSpaceIdentity) {
                        setEffectiveManagedIdentity(agentSpaceIdentity);
                        setIsUsingAgentSpaceIdentity(true);
                    } else {
                        setEffectiveManagedIdentity(agentIdentity);
                        setIsUsingAgentSpaceIdentity(false);
                    }
                } catch (error) {
                    // Fall back to agent identity on error
                    setEffectiveManagedIdentity(agent?.properties?.actionConfiguration?.identity || '');
                    setIsUsingAgentSpaceIdentity(false);
                }
            } else {
                // For non-ICM platforms, use agent identity
                setEffectiveManagedIdentity(agent?.properties?.actionConfiguration?.identity || '');
                setIsUsingAgentSpaceIdentity(false);
            }
        };

        getEffectiveManagedIdentity();
    }, [initialValues.platform, agent?.properties?.actionConfiguration?.identity, sreAgentEndpoint]);

    return (
        <div className={styles.navPanelWrapper}>
            <div className={styles.navPanelContent}>
                <div className={styles.navPanelPadding}>
                    <Formik<IncidentManagementFormValues>
                        initialValues={initialValues}
                        enableReinitialize={true}
                        onSubmit={save}
                        validate={validate}
                    >
                        {formikProps => {
                            return (
                                <IncidentManagementForm
                                    formikProps={formikProps}
                                    loading={loading}
                                    loaded={loaded}
                                    loadFailure={loadFailure}
                                    saving={saving}
                                    saveFailure={saveFailure}
                                    disconnect={disconnect}
                                    managedIdentityId={effectiveManagedIdentity}
                                    tenantId={agent?.identity?.tenantId || ''}
                                    integrated={integrated}
                                    close={close}
                                    keepOpen={keepOpen}
                                    isUsingAgentSpaceIdentity={isUsingAgentSpaceIdentity}
                                />
                            );
                        }}
                    </Formik>
                </div>
            </div>
        </div>
    );
};

export default IncidentManagementSettings;
