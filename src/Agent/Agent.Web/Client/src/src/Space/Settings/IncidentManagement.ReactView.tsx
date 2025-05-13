import { Formik } from 'formik';
import { FC, useContext } from 'react';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { IncidentManagementFormValues } from '../Contracts/IncidentManagement';
import { useIncidentManagement } from './Hooks/useIncidentManagement';
import IncidentManagementForm from './IncidentManagementForm';

const IncidentManagement: FC = () => {
    const environmentContext = useContext(EnvironmentContext);

    const { loading, loaded, loadFailure, saving, saveFailure, initialValues, save, disconnect, validate } = useIncidentManagement(
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
                    />
                );
            }}
        </Formik>
    );
};

export default IncidentManagement;
