import { FC } from "react";
import { AccessControlResources, Settings_Tabs } from "../../Strings/SREResources.resjson";
import { DefaultButton } from "@fluentui/react/lib/Button";
import { useSettingsStyles } from "./Styles/Settings.styles";

interface AccessControlProps {
    parameters: {
        resourceId: string;
    };
}

const AccessControl: FC<AccessControlProps> = ({ parameters }) => {
    const { resourceId } = parameters;

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