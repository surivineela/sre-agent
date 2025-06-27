import { Formik } from 'formik';
import { FC, useContext } from 'react';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { IncidentManagementFormValues } from '../Contracts/IncidentManagement';
import { useIncidentManagement } from './Hooks/useIncidentManagement';
import IncidentManagementForm from './IncidentManagementForm';

const IncidentManagement: FC = () => {
    const environmentContext = useContext(EnvironmentContext);

    const { loading, loaded, loadFailure, saving, saveFailure, initialValues, save, disconnect, validate, agent } = useIncidentManagement(
        environmentContext.resourceId
    );

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
                        managedIdentityId={agent?.properties?.actionConfiguration?.identity || ''}
                        tenantId={agent?.identity?.tenantId || ''}
                    />
                );
            }}
        </Formik>
    );
};

export default IncidentManagement;
