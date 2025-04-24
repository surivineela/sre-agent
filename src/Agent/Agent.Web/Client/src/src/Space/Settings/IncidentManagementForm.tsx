import { DefaultButton, Dropdown, PrimaryButton, TextField } from "@fluentui/react";
import { FC, useMemo } from "react";
import { IncidentManagementPlatformResources, IncidentManagementResources, PagerDutyResources, Settings_Tabs, SreAgentResources } from "../../Strings/SREResources.resjson";
import { IncidentManagementFormProps, IncidentManagementPlatform } from "../Contracts/IncidentManagement";
import { incidentManagementDropdownStyles, incidentManagementTextFieldStyles, useSettingsStyles } from "./Styles/Settings.styles";


const IncidentManagementForm: FC<IncidentManagementFormProps> = ({ formikProps, loading, loadFailure, saving }: IncidentManagementFormProps) => {
    const styles = useSettingsStyles();
    const { setFieldValue, setFieldTouched, submitForm, resetForm, values, dirty, initialValues } = formikProps;

    const isDirty = useMemo(() => {
        if (initialValues.platform === IncidentManagementPlatform.Disconnected && values.platform === IncidentManagementPlatform.Disconnected) {
            return false;
        }
        return dirty;
    }, [dirty, values.platform, initialValues.platform]);

    return (
        <>
            <div style={styles.generalSettingsHeader}>{Settings_Tabs.incidentManagement}</div>
            <div>
                <div style={styles.incidentManagementDescriptionStyle}>{IncidentManagementResources.incidentManagementDescription}</div>
                <Dropdown
                    id="platform"
                    options={[
                        { key: IncidentManagementPlatform.Disconnected, text: IncidentManagementPlatformResources.disconnected },
                        { key: IncidentManagementPlatform.PagerDuty, text: IncidentManagementPlatformResources.pagerDuty },
                    ]}
                    label={IncidentManagementResources.incidentPlatform}
                    required={true}
                    styles={incidentManagementDropdownStyles}
                    selectedKey={values.platform}
                    onChange={(_event, option, _index) => {
                        setFieldValue("platform", option?.key, true);
                        setFieldTouched("platform", true, true);
                    }}
                    disabled={loading || !!loadFailure || saving}
                />

                {values.platform === IncidentManagementPlatform.PagerDuty && (
                    <>
                        <div>
                            <img src="./PagerDuty.svg" alt="PagerDuty" style={styles.pagerDutyLogoStyle} />
                        </div>
                        <div style={styles.incidentManagementDescriptionStyle}>{PagerDutyResources.pagerDutyDescription}</div>
                        <TextField
                            id="connectionKey"
                            label={PagerDutyResources.pagerDutyApiKey}
                            required={true}
                            styles={incidentManagementTextFieldStyles}
                            value={values.connectionKey}
                            onChange={(_event, newValue) => {
                                setFieldValue("connectionKey", newValue, true);
                                setFieldTouched("connectionKey", true, true);
                            }}
                            disabled={saving}
                        />
                    </>
                )}

                <div>
                    <PrimaryButton
                        style={{ borderRadius: 5 }}
                        onClick={() => submitForm()}
                        text={SreAgentResources.save}
                        disabled={!isDirty || saving || (values.platform === IncidentManagementPlatform.PagerDuty && !values.connectionKey)}
                    />
                    <DefaultButton
                        style={{ borderRadius: 5, marginLeft: 10 }}
                        onClick={() => resetForm()}
                        text={SreAgentResources.discard}
                        disabled={!isDirty || saving}
                    />
                </div>
            </div>
        </>
    );
}

export default IncidentManagementForm;