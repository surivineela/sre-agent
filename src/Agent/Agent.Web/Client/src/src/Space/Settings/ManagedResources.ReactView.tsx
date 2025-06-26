import { ConstrainMode, DetailsListLayoutMode, ShimmeredDetailsList } from '@fluentui/react';
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
import { ManagedResourcesStringResources, SettingsTabResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { useManagedResources } from './Hooks/useManagedResources';
import ResourceGroupPicker from './ResourceGroupPicker';
import { useManagedResourcesStyles } from './Styles/ManagedResources.styles';

const ManagedResources: FC = () => {
    const { resourceId } = useContext(EnvironmentContext);
    const az = useContext(AzPortalContext);
    const intl = useIntl();

    const styles = useManagedResourcesStyles();

    const {
        managedResourceGroups,
        columns,
        isLoading,
        subscriptionOptions,
        searchText,
        selection,
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
                    <ToolbarButton
                        icon={<Add16Regular />}
                        style={{ paddingLeft: '0px', minWidth: '20px' }}
                        appearance="subtle"
                        disabled={isLoading || isUpdating}
                        onClick={() => setHideResourceGroupPicker(false)}
                    >
                        {intl.formatMessage(ManagedResourcesStringResources.add)}
                    </ToolbarButton>
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
                    <ToolbarButton
                        appearance="subtle"
                        icon={<Delete16Regular />}
                        onClick={() => setShowDeleteConfirmationDialog(true)}
                        disabled={isDeleteDisabled}
                    >
                        {intl.formatMessage(SreAgentResources.delete)}
                    </ToolbarButton>
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
                <ShimmeredDetailsList
                    key={managedResourceGroups?.length}
                    enableShimmer={isLoading}
                    items={managedResourceGroups || []}
                    columns={columns}
                    selection={selection.current}
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
