import { DefaultButton, Dropdown, PrimaryButton, TextField } from "@fluentui/react";
import { FC } from "react";
import { incidentManagementResources, pagerDutyResources, Settings_Tabs, SreAgentResources } from "../../Strings/SREResources.resjson";
import { incidentManagementDropdownStyles, incidentManagementTextFieldStyles, useSettingsStyles } from "./Styles/Settings.styles";

interface IncidentManagementProps {
    parameters: {
        resourceId: string;
    };
}

const IncidentManagement: FC<IncidentManagementProps> = () => {
    const styles = useSettingsStyles();

    return (
        <>
            <div style={styles.generalSettingsHeader}>{Settings_Tabs.incidentManagement}</div>
            <div>
                <div style={styles.incidentManagementDescriptionStyle}>{incidentManagementResources.incidentManagementDescription}</div>
                <Dropdown
                    id="incidentPlatform"
                    options={[
                        { key: 'pagerDuty', text: pagerDutyResources.pagerDuty },
                    ]}
                    label={incidentManagementResources.incidentPlatform}
                    required={true}
                    styles={incidentManagementDropdownStyles}
                />
                <img src="./PagerDuty.svg" alt="PagerDuty" style={styles.pagerDutyLogoStyle}></img>
                <div style={styles.incidentManagementDescriptionStyle}>{pagerDutyResources.pagerDutyDescription}</div>
                <TextField
                    id="logicAppName"
                    label={SreAgentResources.logicAppName}
                    required={true}
                    styles={incidentManagementTextFieldStyles}
                />
                <Dropdown
                    id="region"
                    options={[
                        { key: 'eastUs', text: 'East US' },
                        { key: 'westUs', text: 'West US' },
                        { key: 'centralUs', text: 'Central US' },
                    ]}
                    label={SreAgentResources.region}
                    required={true}
                    styles={incidentManagementDropdownStyles}
                />
                <TextField
                    id="pagerDutyApiKey"
                    label={pagerDutyResources.pagerDutyApiKey}
                    required={true}
                    styles={incidentManagementTextFieldStyles}
                />
                <div>
                    <PrimaryButton
                        style={{ borderRadius: 5 }}
                        onClick={() => {}}
                        text={"Save"}
                        disabled={false}
                    />
                    <DefaultButton
                        style={{ borderRadius: 5, marginLeft: 10 }}
                        onClick={() => {}}
                        text={"Discard"}
                        disabled={false}
                    />
                </div>
            </div>
        </>
    );
};

export default IncidentManagement;