import { Button } from '@fluentui/react-components';
import { Add16Regular, ArrowClockwise16Regular, Delete16Regular } from '@fluentui/react-icons';
import { FC } from 'react';
import { useIntl } from 'react-intl';
import { DataConnectionsResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { useIncidentManagementStyles } from '../Styles/IncidentManagement.styles';

export type DataConnectionsToolbarProps = {
    onRefreshClick: () => void;
    onNewDataConnectorClick: () => void;
    onDeleteDataConnectorClick: () => void;
    isConnectorSelected: boolean;
    isOperationInProgress?: boolean;
};

const DataConnectionsToolbar: FC<DataConnectionsToolbarProps> = ({
    onRefreshClick,
    onNewDataConnectorClick,
    onDeleteDataConnectorClick,
    isConnectorSelected,
    isOperationInProgress = false,
}) => {
    const intl = useIntl();
    const styles = useIncidentManagementStyles();

    return (
        <div className={styles.toolbar}>
            <Button
                icon={<Add16Regular />}
                appearance="transparent"
                className={styles.button}
                onClick={() => {
                    onNewDataConnectorClick();
                }}
                disabled={isOperationInProgress}
            >
                {intl.formatMessage(DataConnectionsResources.createDataConnection)}
            </Button>
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
            <Button
                icon={<Delete16Regular />}
                appearance="transparent"
                className={styles.button}
                onClick={() => {
                    onDeleteDataConnectorClick();
                }}
                disabled={!isConnectorSelected || isOperationInProgress}
            >
                {intl.formatMessage(SreAgentResources.delete)}
            </Button>
        </div>
    );
};

export default DataConnectionsToolbar;
