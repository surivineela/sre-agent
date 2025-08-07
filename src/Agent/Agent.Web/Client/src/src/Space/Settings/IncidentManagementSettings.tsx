import { Formik } from 'formik';
import { FC } from 'react';
import { IncidentManagementFormValues, IncidentManagementSettingsProps } from '../Contracts/IncidentManagement';
import { useIncidentManagement } from './Hooks/useIncidentManagementSettings';
import IncidentManagementForm from './IncidentManagementForm';

const IncidentManagementSettings: FC<IncidentManagementSettingsProps> = ({ integrated, close, keepOpen }) => {
    const { loading, loaded, loadFailure, saving, saveFailure, initialValues, save, disconnect, validate, agent } =
        useIncidentManagement(close);

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
                        integrated={integrated}
                        close={close}
                        keepOpen={keepOpen}
                    />
                );
            }}
        </Formik>
    );
};

export default IncidentManagementSettings;
