import { FC, useContext } from "react";
import { AccessControlResources, Settings_Tabs } from "../../Strings/SREResources.resjson";
import { DefaultButton } from "@fluentui/react/lib/Button";
import { useSettingsStyles } from "./Styles/Settings.styles";
import { EnvironmentContext } from "../../Common/AzPortalProxy/Providers/StartupInfoContext";

const AccessControl: FC = () => {
    const { resourceId } = useContext(EnvironmentContext);

    const styles = useSettingsStyles();

    return (
      <>
        <div style={styles.generalSettingsHeader}>{Settings_Tabs.accessControl}</div>
        <div style={styles.accessControlSettingsContainer}>
          {AccessControlResources.accessControlDescription}
          <DefaultButton
            iconProps={{ imageProps: { src: './Open.svg', width: 18, height: 18 } }}
            text={AccessControlResources.openAccessControl}
            style={styles.accessControlSettingsButton}
            onClick={() => window.open(`https://portal.azure.com/#view/Microsoft_Azure_AD/AccessControlBlade/scope${resourceId}`, '_blank')}
          />
        </div>
      </>
    );
  };

export default AccessControl;