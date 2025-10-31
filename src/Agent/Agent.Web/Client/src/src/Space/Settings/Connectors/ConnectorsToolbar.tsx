import { Button, Divider } from '@fluentui/react-components';
import { Add16Regular, ArrowClockwise16Regular, Delete16Regular } from '@fluentui/react-icons';
import { FC } from 'react';
import { useIntl } from 'react-intl';
import PermissionedButton from '../../../Common/Components/PermissionedButton';
import { SearchBoxWithDebounce } from '../../../Common/Components/SearchBox/SearchBoxWithDebounce';
import useUserPermissions from '../../../Common/Hooks/useUserPermissions';
import { ConnectorsResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import { useConnectorsStyles } from './Connectors.styles';

export type ConnectorsToolbarProps = {
    onRefreshClick: () => void;
    onNewConnectorClick: () => void;
    onDeleteConnectorClick: () => void;
    isConnectorSelected: boolean;
    setSearchTerm: React.Dispatch<React.SetStateAction<string>>;
    selectedCount?: number;
    isOperationInProgress?: boolean;
};

const ConnectorsToolbar: FC<ConnectorsToolbarProps> = ({
    onRefreshClick,
    onNewConnectorClick,
    onDeleteConnectorClick,
    isConnectorSelected,
    setSearchTerm,
    isOperationInProgress = false,
}) => {
    const intl = useIntl();
    const styles = useConnectorsStyles();
    const { canWriteAgent } = useUserPermissions();

    return (
        <div className={styles.toolbar}>
            <div className={styles.toolbarLeft}>
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
                <Divider vertical />
                <SearchBoxWithDebounce className={styles.searchBox} setSearchTerm={setSearchTerm} />
            </div>
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

export default ConnectorsToolbar;
