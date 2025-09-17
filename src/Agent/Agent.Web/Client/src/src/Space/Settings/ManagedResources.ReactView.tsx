import { ConstrainMode, DetailsListLayoutMode } from '@fluentui/react';
import {
    Button,
    Dialog,
    DialogActions,
    DialogBody,
    DialogSurface,
    DialogTitle,
    InputOnChangeData,
    MessageBar,
    MessageBarBody,
    MessageBarGroup,
    SearchBox,
    SearchBoxChangeEvent,
    Toolbar,
    ToolbarButton,
    ToolbarDivider,
} from '@fluentui/react-components';
import { Add16Regular, ArrowClockwise16Regular, Delete16Regular } from '@fluentui/react-icons';
import { debounce } from 'lodash';
import { FC, useContext } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import PermissionedToolbarButton from '../../Common/Components/PermissionedToolbarButton';
import ShimmeredDetailsListWithSelection from '../../Common/Components/ShimmeredDetailsListWithSelection';
import useUserPermissions from '../../Common/Hooks/useUserPermissions';
import { ManagedResourcesStringResources, SettingsTabResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { useManagedResources } from './Hooks/useManagedResources';
import { ResourceGroup } from './Hooks/useResourceGroups';
import ResourceGroupPicker from './ResourceGroupPicker';
import { useManagedResourcesStyles } from './Styles/ManagedResources.styles';

const ManagedResources: FC = () => {
    const { resourceId } = useContext(EnvironmentContext);
    const az = useContext(AzPortalContext);
    const intl = useIntl();

    const styles = useManagedResourcesStyles();
    const { canWriteAgent } = useUserPermissions();

    const {
        managedResourceGroups,
        columns,
        isLoading,
        subscriptionOptions,
        searchText,
        selectedKeys,
        onUpdateSelection,
        isDeleteDisabled,
        showDeleteConfirmationDialog,
        hideResourceGroupPicker,
        subscriptionId,
        managedResourceGroupIds,
        setHideResourceGroupPicker,
        onDeleteClick,
        onAddClick,
        setShowDeleteConfirmationDialog,
        setSearchText,
        refresh,
        isUpdating,
    } = useManagedResources(resourceId, az);

    return (
        <div className={styles.container}>
            <div className={styles.header}>{intl.formatMessage(SettingsTabResources.managedResources)}</div>
            <MessageBarGroup animate={'exit-only'} className={styles.messageBarGroup}>
                <MessageBar className={styles.messageBar} intent={'info'}>
                    <MessageBarBody className={styles.messageBarBody}>
                        {intl.formatMessage(SreAgentResources.supportedServicesMessage)}
                    </MessageBarBody>
                </MessageBar>
            </MessageBarGroup>
            <div className={styles.buttonsContainer}>
                <Toolbar>
                    <PermissionedToolbarButton
                        icon={<Add16Regular />}
                        style={{ paddingLeft: '0px', minWidth: '20px' }}
                        appearance="subtle"
                        disabledReason={isLoading || isUpdating}
                        onClick={() => setHideResourceGroupPicker(false)}
                        canPerform={canWriteAgent}
                        noPermissionTooltip={intl.formatMessage(SreAgentResources.noPermissionManagedResources)}
                    >
                        {intl.formatMessage(ManagedResourcesStringResources.add)}
                    </PermissionedToolbarButton>
                    <ToolbarButton
                        style={{ paddingLeft: '0px', minWidth: '20px' }}
                        icon={<ArrowClockwise16Regular />}
                        appearance="subtle"
                        disabled={isLoading || isUpdating}
                        onClick={() => refresh()}
                    >
                        {intl.formatMessage(ManagedResourcesStringResources.refresh)}
                    </ToolbarButton>
                    <ToolbarDivider style={{ padding: '0px' }} />
                    <PermissionedToolbarButton
                        appearance="subtle"
                        icon={<Delete16Regular />}
                        onClick={() => setShowDeleteConfirmationDialog(true)}
                        disabledReason={isDeleteDisabled}
                        canPerform={canWriteAgent}
                        noPermissionTooltip={intl.formatMessage(SreAgentResources.noPermissionManagedResources)}
                    >
                        {intl.formatMessage(SreAgentResources.delete)}
                    </PermissionedToolbarButton>
                </Toolbar>
            </div>
            <div className={styles.pillsContainer}>
                <SearchBox
                    className={styles.searchBox}
                    placeholder={intl.formatMessage(ManagedResourcesStringResources.searchForResourceGroups)}
                    value={searchText}
                    onChange={debounce((_event: SearchBoxChangeEvent, data: InputOnChangeData) => setSearchText(data.value ?? ''))}
                />
            </div>
            <div style={{ width: '99%', maxWidth: '99%' }}>
                <ShimmeredDetailsListWithSelection<ResourceGroup>
                    enableShimmer={isLoading}
                    items={managedResourceGroups || []}
                    getKey={rg => rg.id}
                    columns={columns}
                    selectedKeys={selectedKeys}
                    onUpdateSelection={onUpdateSelection}
                    className={styles.detailsList}
                    layoutMode={DetailsListLayoutMode.justified}
                    constrainMode={ConstrainMode.horizontalConstrained}
                />
            </div>
            <Dialog open={showDeleteConfirmationDialog} onOpenChange={(_, data) => setShowDeleteConfirmationDialog(data.open)}>
                <DialogSurface>
                    <DialogTitle>{intl.formatMessage(ManagedResourcesStringResources.deleteTitle)}</DialogTitle>
                    <DialogBody style={{ display: 'flex' }}>{intl.formatMessage(ManagedResourcesStringResources.confirmDelete)}</DialogBody>
                    <DialogActions>
                        <Button
                            appearance="transparent"
                            className={styles.dangerButton}
                            onClick={() => {
                                setShowDeleteConfirmationDialog(false);
                                onDeleteClick();
                            }}
                        >
                            {intl.formatMessage(SreAgentResources.yes)}
                        </Button>
                        <Button appearance="secondary" onClick={() => setShowDeleteConfirmationDialog(false)}>
                            {intl.formatMessage(SreAgentResources.no)}
                        </Button>
                    </DialogActions>
                </DialogSurface>
            </Dialog>
            <ResourceGroupPicker
                subscriptionId={subscriptionId}
                hideResourceGroupPicker={hideResourceGroupPicker}
                existingResourceGroupIds={managedResourceGroupIds}
                setHideResourceGroupPicker={setHideResourceGroupPicker}
                onClick={onAddClick}
                subscriptionOptions={subscriptionOptions}
            />
        </div>
    );
};

export default ManagedResources;
