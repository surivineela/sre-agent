import {
    Button,
    createTableColumn,
    DataGrid,
    DataGridBody,
    DataGridCell,
    DataGridHeader,
    DataGridHeaderCell,
    DataGridRow,
    Dialog,
    DialogActions,
    DialogBody,
    DialogSurface,
    DialogTitle,
    InputOnChangeData,
    Link,
    makeStyles,
    SearchBox,
    SearchBoxChangeEvent,
    SkeletonItem,
    TableCellLayout,
    TableColumnDefinition,
    tokens,
    Toolbar,
    ToolbarButton,
    ToolbarDivider,
} from '@fluentui/react-components';
import { Add16Regular, ArrowClockwise16Regular, Delete16Regular } from '@fluentui/react-icons';
import { Formik } from 'formik';
import debounce from 'lodash/debounce';
import { FC, useCallback, useContext, useEffect, useMemo, useRef } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import PermissionedToolbarButton from '../../../Common/Components/PermissionedToolbarButton';
import { TextWithLink } from '../../../Common/Components/TextWithLink';
import { SreAgentFwLinks } from '../../../Common/Constants/FwLinks';
import { getUserFriendlyLocation } from '../../../Common/Helpers/LocationHelper';
import useUserPermissions from '../../../Common/Hooks/useUserPermissions';
import { ManagedResourcesStringResources, SettingsTabResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import { useManagedResources } from '../Hooks/useManagedResources';
import { getSubscriptionId, ResourceGroup } from '../Hooks/useResourceGroups';
import { useManagedResourcesStyles } from '../Styles/ManagedResources.styles';
import ResourceGroupPicker, { ResourceGroupPickerFormValues } from './ResourceGroupPicker';

const useLocalStyles = makeStyles({
    scrollableContainer: {
        width: '100%',
        overflowX: 'auto',
        overflowY: 'auto',
        minWidth: '0',
    },
    dataGrid: {
        width: '100%',
        '& table': {
            width: '100%',
            tableLayout: 'auto',
        },
    },
    dataGridHeader: {
        fontWeight: '600',
        position: 'sticky',
        top: '0',
        backgroundColor: tokens.colorNeutralBackground1,
        zIndex: '1',
    },
});

const ManagedResources: FC = () => {
    const { resourceId } = useContext(EnvironmentContext);
    const az = useContext(AzPortalContext);
    const intl = useIntl();

    const styles = useManagedResourcesStyles();
    const localStyles = useLocalStyles();
    const { canWriteAgent } = useUserPermissions();

    const {
        managedResourceGroups,
        isLoading,
        subscriptionOptions,
        subscriptionsList,
        searchText,
        selectedKeys,
        onUpdateSelection,
        isDeleteDisabled,
        showDeleteConfirmationDialog,
        subscriptionId,
        managedResourceGroupIds,
        onDeleteClick,
        onAddClick,
        setShowDeleteConfirmationDialog,
        setSearchText,
        refresh,
        isUpdating,
        showResourceGroupPicker,
        setShowResourceGroupPicker,
    } = useManagedResources(resourceId, az);

    const addButtonRef = useRef<HTMLButtonElement | null>(null);
    const previousShowResourceGroupPicker = useRef(showResourceGroupPicker);

    const openResourceOverviewBlade = useCallback(
        (id: string) => {
            if (id) {
                az.openBlade({
                    extension: 'HubsExtension',
                    detailBlade: 'ResourceMenuBlade',
                    detailBladeInputs: {
                        id,
                    },
                });
            }
        },
        [az]
    );

    const columns = useMemo<TableColumnDefinition<ResourceGroup>[]>(
        () => [
            createTableColumn<ResourceGroup>({
                columnId: 'name',
                compare: (a, b) => a.name.localeCompare(b.name),
                renderHeaderCell: () => (
                    <span style={{ fontWeight: 600 }}>{intl.formatMessage(ManagedResourcesStringResources.resourceGroup)}</span>
                ),
                renderCell: item => (
                    <TableCellLayout media={<img src="./ResourceGroup.svg" alt="ResourceGroup" style={{ height: 16, width: 16 }} />}>
                        <Link onClick={() => openResourceOverviewBlade(item.id)}>{item.name}</Link>
                    </TableCellLayout>
                ),
            }),
            createTableColumn<ResourceGroup>({
                columnId: 'subscription',
                compare: (a, b) => {
                    const aSubId = getSubscriptionId(a.id);
                    const bSubId = getSubscriptionId(b.id);
                    const aSub = subscriptionsList?.find(s => s.subscriptionId === aSubId);
                    const bSub = subscriptionsList?.find(s => s.subscriptionId === bSubId);
                    return (aSub?.displayName ?? '').localeCompare(bSub?.displayName ?? '');
                },
                renderHeaderCell: () => (
                    <span style={{ fontWeight: 600 }}>{intl.formatMessage(ManagedResourcesStringResources.subscription)}</span>
                ),
                renderCell: item => {
                    const subscriptionId = getSubscriptionId(item.id);
                    const subscription = subscriptionsList?.find(s => s.subscriptionId === subscriptionId);
                    return (
                        <TableCellLayout>
                            <Link onClick={() => openResourceOverviewBlade(item.id.split('/resource')[0])}>
                                {subscription?.displayName ?? ''}
                            </Link>
                        </TableCellLayout>
                    );
                },
            }),
            createTableColumn<ResourceGroup>({
                columnId: 'location',
                compare: (a, b) => a.location.localeCompare(b.location),
                renderHeaderCell: () => (
                    <span style={{ fontWeight: 600 }}>{intl.formatMessage(ManagedResourcesStringResources.region)}</span>
                ),
                renderCell: item => <TableCellLayout>{getUserFriendlyLocation(item.location)}</TableCellLayout>,
            }),
        ],
        [intl, subscriptionsList, openResourceOverviewBlade]
    );

    const columnSizingOptions = useMemo(
        () => ({
            name: {
                minWidth: 200,
                idealWidth: 400,
            },
            subscription: {
                minWidth: 150,
                idealWidth: 300,
            },
            location: {
                minWidth: 100,
                idealWidth: 200,
            },
        }),
        []
    );

    useEffect(() => {
        if (previousShowResourceGroupPicker.current && !showResourceGroupPicker) {
            addButtonRef.current?.focus();
        }

        previousShowResourceGroupPicker.current = !showResourceGroupPicker;
    }, [showResourceGroupPicker]);

    return (
        <div className={styles.container}>
            <div className={styles.header}>{intl.formatMessage(SettingsTabResources.managedResources)}</div>
            <TextWithLink
                text={intl.formatMessage(SreAgentResources.elevatePermissionsMessage)}
                linkUrl={SreAgentFwLinks.agentManagedIdentity}
            />
            <div className={styles.buttonsContainer}>
                <Toolbar>
                    <PermissionedToolbarButton
                        ref={addButtonRef}
                        icon={<Add16Regular />}
                        style={{ paddingLeft: '0px', minWidth: '20px' }}
                        appearance="subtle"
                        disabledReason={isLoading || isUpdating}
                        onClick={() => setShowResourceGroupPicker(true)}
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
            <div style={{ width: '100%', paddingTop: '10px' }}>
                {isLoading && (!managedResourceGroups || managedResourceGroups.length === 0) ? (
                    <div>
                        {Array.from({ length: 5 }).map((_, index) => (
                            <div key={index} style={{ display: 'flex', padding: '8px 12px', alignItems: 'center', gap: '12px' }}>
                                <SkeletonItem size={16} style={{ width: '30px' }} />
                                <SkeletonItem size={16} style={{ width: '300px', flex: 1 }} />
                                <SkeletonItem size={16} style={{ width: '200px' }} />
                                <SkeletonItem size={16} style={{ width: '150px' }} />
                            </div>
                        ))}
                    </div>
                ) : (
                    <div className={localStyles.scrollableContainer}>
                        <DataGrid
                            items={managedResourceGroups || []}
                            columns={columns}
                            sortable
                            selectionMode="multiselect"
                            selectedItems={new Set(selectedKeys)}
                            onSelectionChange={(_, data) => {
                                const newSelectedKeys = Array.from(data.selectedItems).map(String);
                                const selectedItems = managedResourceGroups?.filter(rg => newSelectedKeys.includes(rg.id)) || [];
                                onUpdateSelection({ selectedItems, selectedKeys: newSelectedKeys });
                            }}
                            getRowId={item => item.id}
                            resizableColumns
                            columnSizingOptions={columnSizingOptions}
                            className={localStyles.dataGrid}
                            size="small"
                        >
                            <DataGridHeader className={localStyles.dataGridHeader}>
                                <DataGridRow>
                                    {({ renderHeaderCell }) => <DataGridHeaderCell>{renderHeaderCell()}</DataGridHeaderCell>}
                                </DataGridRow>
                            </DataGridHeader>
                            <DataGridBody<ResourceGroup>>
                                {({ item, rowId }) => (
                                    <DataGridRow<ResourceGroup> key={rowId}>
                                        {({ renderCell }) => <DataGridCell>{renderCell(item)}</DataGridCell>}
                                    </DataGridRow>
                                )}
                            </DataGridBody>
                        </DataGrid>
                    </div>
                )}
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
            <Formik<ResourceGroupPickerFormValues>
                initialValues={[]}
                onSubmit={(values, { resetForm }) => {
                    onAddClick(values);
                    resetForm();
                }}
            >
                <ResourceGroupPicker
                    subscriptionId={subscriptionId}
                    showResourceGroupPicker={showResourceGroupPicker}
                    existingResourceGroupIds={managedResourceGroupIds}
                    setShowResourceGroupPicker={setShowResourceGroupPicker}
                    onClick={onAddClick}
                    subscriptionOptions={subscriptionOptions}
                />
            </Formik>
        </div>
    );
};

export default ManagedResources;
