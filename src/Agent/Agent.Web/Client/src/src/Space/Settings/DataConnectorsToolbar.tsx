import { Button, InputOnChangeData, SearchBox, SearchBoxChangeEvent } from '@fluentui/react-components';
import { Add16Regular, ArrowClockwise16Regular, Delete16Regular } from '@fluentui/react-icons';
import { FC } from 'react';
import { useIntl } from 'react-intl';
import PermissionedButton from '../../Common/Components/PermissionedButton';
import useUserPermissions from '../../Common/Hooks/useUserPermissions';
import { DataConnectorsResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { useDataKnowledgeSpaceStyles } from './Styles/DataKnowledgeSpace.styles';

export type DataConnectorsToolbarProps = {
    onRefreshClick: () => void;
    onNewDataConnectorClick: () => void;
    onDeleteDataConnectorClick: () => void;
    isConnectorSelected: boolean;
    selectedCount?: number;
    isOperationInProgress?: boolean;
    searchText?: string;
    onSearchChange?: (event: SearchBoxChangeEvent, data: InputOnChangeData) => void;
};

const DataConnectorsToolbar: FC<DataConnectorsToolbarProps> = ({
    onRefreshClick,
    onNewDataConnectorClick,
    onDeleteDataConnectorClick,
    isConnectorSelected,
    isOperationInProgress = false,
    searchText = '',
    onSearchChange,
}) => {
    const intl = useIntl();
    const styles = useDataKnowledgeSpaceStyles();
    const { canWriteAgent } = useUserPermissions();

    return (
        <div className={styles.toolbar}>
            <PermissionedButton
                icon={<Add16Regular />}
                appearance="transparent"
                className={styles.button}
                style={{ fontWeight: 500 }}
                canPerform={canWriteAgent}
                disabledReason={isOperationInProgress}
                noPermissionTooltip={intl.formatMessage(SreAgentResources.noPermissionDataConnectors)}
                onClick={() => onNewDataConnectorClick()}
            >
                {intl.formatMessage(DataConnectorsResources.createDataConnector)}
            </PermissionedButton>
            <PermissionedButton
                icon={<Delete16Regular />}
                appearance="transparent"
                className={styles.button}
                canPerform={canWriteAgent}
                disabledReason={!isConnectorSelected || isOperationInProgress}
                noPermissionTooltip={intl.formatMessage(SreAgentResources.noPermissionDataConnectors)}
                onClick={() => onDeleteDataConnectorClick()}
            >
                {intl.formatMessage(DataConnectorsResources.disconnect)}
            </PermissionedButton>
            <SearchBox
                className={styles.searchBox}
                placeholder={intl.formatMessage(DataConnectorsResources.searchPlaceholder)}
                value={searchText}
                onChange={onSearchChange}
            />
            <Button
                icon={<ArrowClockwise16Regular />}
                appearance="transparent"
                className={styles.toolbarRefresh}
                onClick={() => {
                    onRefreshClick();
                }}
                disabled={isOperationInProgress}
            >
                {intl.formatMessage(SreAgentResources.refresh)}
            </Button>
        </div>
    );
};

export default DataConnectorsToolbar;
