import { Button, Divider } from '@fluentui/react-components';
import { Add16Regular, ArrowClockwise16Regular, Delete16Regular } from '@fluentui/react-icons';
import { FC, useMemo } from 'react';
import { useIntl } from 'react-intl';
import PermissionedButton from '../../../Common/Components/PermissionedButton';
import { PillFilter } from '../../../Common/Components/PillFilter/PillFilter';
import { SearchBoxWithDebounce } from '../../../Common/Components/SearchBox/SearchBoxWithDebounce';
import useUserPermissions from '../../../Common/Hooks/useUserPermissions';
import { ConnectorsResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import { ServiceTypeFilter, ServiceTypeFilterKey } from '../DataConnectorsUtilities';
import { useConnectorsStyles } from './Connectors.styles';
import { ConnectorType } from './Wizard/Common/ConnectorType';

export type ConnectorsToolbarProps = {
    onRefreshClick: () => void;
    onNewConnectorClick: () => void;
    onDeleteConnectorClick: () => void;
    isConnectorSelected: boolean;
    setSearchTerm: React.Dispatch<React.SetStateAction<string>>;
    selectedCount?: number;
    isOperationInProgress?: boolean;
    serviceTypeFilter: ServiceTypeFilter;
    setServiceTypeFilter: (serviceType: ServiceTypeFilter) => void;
};

const ConnectorsToolbar: FC<ConnectorsToolbarProps> = ({
    onRefreshClick,
    onNewConnectorClick,
    onDeleteConnectorClick,
    isConnectorSelected,
    setSearchTerm,
    isOperationInProgress = false,
    serviceTypeFilter,
    setServiceTypeFilter,
}) => {
    const intl = useIntl();
    const styles = useConnectorsStyles();
    const { canWriteAgent } = useUserPermissions();

    const serviceTypeOptions = useMemo(
        () => [
            {
                key: ServiceTypeFilterKey.All,
                label: intl.formatMessage(SreAgentResources.all),
            },
            {
                key: ConnectorType.AzureDataExplorerQuery,
                label: intl.formatMessage(ConnectorsResources.databaseQueryConnector),
            },
            {
                key: ConnectorType.AzureDataExplorerIndexing,
                label: intl.formatMessage(ConnectorsResources.databaseIndexingConnector),
            },
            {
                key: ConnectorType.AzureDevOpsDocumentation,
                label: intl.formatMessage(ConnectorsResources.azureDevops),
            },
            {
                key: ConnectorType.OutlookSendEmail,
                label: intl.formatMessage(ConnectorsResources.office365Outlook),
            },
            {
                key: ConnectorType.TeamsSendNotification,
                label: intl.formatMessage(ConnectorsResources.microsoftTeams),
            },
            {
                key: ConnectorType.McpServer,
                label: intl.formatMessage(ConnectorsResources.mcpServer),
            },
        ],
        [intl]
    );

    return (
        <div className={styles.toolbar}>
            <PermissionedButton
                icon={<Add16Regular />}
                appearance="transparent"
                className={styles.button}
                canPerform={canWriteAgent}
                disabledReason={isOperationInProgress}
                noPermissionTooltip={intl.formatMessage(SreAgentResources.noPermissionDataConnectors)}
                onClick={() => onNewConnectorClick()}
            >
                {intl.formatMessage(ConnectorsResources.addConnector)}
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
            <PermissionedButton
                icon={<Delete16Regular />}
                appearance="transparent"
                className={styles.button}
                canPerform={canWriteAgent}
                disabledReason={!isConnectorSelected || isOperationInProgress}
                noPermissionTooltip={intl.formatMessage(SreAgentResources.noPermissionDataConnectors)}
                onClick={() => onDeleteConnectorClick()}
            >
                {intl.formatMessage(SreAgentResources.remove)}
            </PermissionedButton>
            <Divider vertical className={styles.divider} />
            <SearchBoxWithDebounce className={styles.searchBox} setSearchTerm={setSearchTerm} />
            <PillFilter
                label={intl.formatMessage(ConnectorsResources.service)}
                filterType="combobox"
                options={serviceTypeOptions}
                selectedKeys={[serviceTypeFilter]}
                onApply={keys => {
                    setServiceTypeFilter(keys[0] as ServiceTypeFilter);
                }}
            />
        </div>
    );
};

export default ConnectorsToolbar;
