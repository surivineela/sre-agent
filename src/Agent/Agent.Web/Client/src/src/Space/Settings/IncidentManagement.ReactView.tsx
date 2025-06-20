import { Formik } from 'formik';
import { FC, useContext, useMemo } from 'react';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ArmResourceDescriptor } from '../../Common/Helpers/ResourceDescriptors';
import { IncidentManagementFormValues } from '../Contracts/IncidentManagement';
import { useIncidentManagement } from './Hooks/useIncidentManagement';
import IncidentManagementForm from './IncidentManagementForm';

const IncidentManagement: FC = () => {
    const environmentContext = useContext(EnvironmentContext);

    const { loading, loaded, loadFailure, saving, saveFailure, initialValues, save, disconnect, validate, agent } = useIncidentManagement(
        environmentContext.resourceId
    );

    const { ameUrl, managedIdentityResourceName } = useMemo(() => {
        if (!agent?.properties?.actionConfiguration?.identity) {
            return {
                ameUrl: '',
                managedIdentityResourceName: '',
            };
        }
        let agentMSIResourceId = agent?.properties.actionConfiguration?.identity;
        if (!agentMSIResourceId.startsWith('/')) {
            agentMSIResourceId = `/${agentMSIResourceId}`;
        }
        const resource = new ArmResourceDescriptor(agentMSIResourceId);
        return {
            managedIdentityResourceName: resource.resourceName,
            ameUrl: `https://ms.portal.azure.com/#@MSAzureCloud.onmicrosoft.com/resource${agentMSIResourceId}/overview`,
        };
    }, [agent?.properties?.actionConfiguration?.identity]);

    return (
        <Formik<IncidentManagementFormValues> initialValues={initialValues} enableReinitialize={true} onSubmit={save} validate={validate}>
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
                        armUrl={ameUrl}
                        managedIdentityResourceName={managedIdentityResourceName}
                        tenantId={agent?.identity?.tenantId || ''}
                    />
                );
            }}
        </Formik>
    );
};

export default IncidentManagement;
