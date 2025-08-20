import { Formik } from 'formik';
import { FC } from 'react';
import { IncidentManagementFormValues, IncidentManagementSettingsProps } from '../Contracts/IncidentManagement';
import { useIncidentManagementStyles } from '../Styles/IncidentManagement.styles';
import { useIncidentManagement } from './Hooks/useIncidentManagementSettings';
import IncidentManagementForm from './IncidentManagementForm';

const IncidentManagementSettings: FC<IncidentManagementSettingsProps> = ({ integrated, close, keepOpen }) => {
    const styles = useIncidentManagementStyles();
    const { loading, loaded, loadFailure, saving, saveFailure, initialValues, save, disconnect, validate, agent } =
        useIncidentManagement(close);

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
                                    managedIdentityId={agent?.properties?.actionConfiguration?.identity || ''}
                                    tenantId={agent?.identity?.tenantId || ''}
                                    integrated={integrated}
                                    close={close}
                                    keepOpen={keepOpen}
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
