import { Button } from '@fluentui/react-components';
import { Add16Regular, ArrowClockwise16Regular, Delete16Regular } from '@fluentui/react-icons';
import { FC } from 'react';
import { useIntl } from 'react-intl';
import PermissionedButton from '../../Common/Components/PermissionedButton';
import useUserPermissions from '../../Common/Hooks/useUserPermissions';
import { DataConnectorsResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { useIncidentManagementStyles } from '../Styles/IncidentManagement.styles';

export type DataConnectorsToolbarProps = {
    onRefreshClick: () => void;
    onNewDataConnectorClick: () => void;
    onDeleteDataConnectorClick: () => void;
    isConnectorSelected: boolean;
    isOperationInProgress?: boolean;
};

const DataConnectorsToolbar: FC<DataConnectorsToolbarProps> = ({
    onRefreshClick,
    onNewDataConnectorClick,
    onDeleteDataConnectorClick,
    isConnectorSelected,
    isOperationInProgress = false,
}) => {
    const intl = useIntl();
    const styles = useIncidentManagementStyles();
    const { canWriteAgent } = useUserPermissions();

    return (
        <div className={styles.toolbar}>
            <PermissionedButton
                icon={<Add16Regular />}
                appearance="transparent"
                className={styles.button}
                canPerform={canWriteAgent}
                disabledReason={isOperationInProgress}
                noPermissionTooltip={intl.formatMessage(SreAgentResources.noPermissionDataConnectors)}
                onClick={() => onNewDataConnectorClick()}
            >
                {intl.formatMessage(DataConnectorsResources.createDataConnector)}
            </PermissionedButton>
            <Button
                icon={<ArrowClockwise16Regular />}
                appearance="transparent"
                className={styles.button}
                onClick={() => {
                    onRefreshClick();
                }}
                disabled={isOperationInProgress}
            >
                {intl.formatMessage(SreAgentResources.refresh)}
            </Button>
            <div className={styles.divider} />
            <PermissionedButton
                icon={<Delete16Regular />}
                appearance="transparent"
                className={styles.button}
                canPerform={canWriteAgent}
                disabledReason={!isConnectorSelected || isOperationInProgress}
                noPermissionTooltip={intl.formatMessage(SreAgentResources.noPermissionDataConnectors)}
                onClick={() => onDeleteDataConnectorClick()}
            >
                {intl.formatMessage(SreAgentResources.delete)}
            </PermissionedButton>
        </div>
    );
};

export default DataConnectorsToolbar;
