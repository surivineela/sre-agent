import { IconButton, MessageBar, MessageBarType } from '@fluentui/react';
import { CheckmarkCircle16Filled, DismissCircle16Filled } from '@fluentui/react-icons';
import { CheckboxVisibility, ConstrainMode, DetailsListLayoutMode, IColumn } from '@fluentui/react/lib/DetailsList';
import { Link } from '@fluentui/react/lib/Link';
import { ShimmeredDetailsList } from '@fluentui/react/lib/ShimmeredDetailsList';
import { Dispatch, FC, SetStateAction, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { PermissionClient } from '../../Common/Clients/PermissionsClient';
import { getUserFriendlyLocation } from '../../Common/Helpers/LocationHelper';
import { ManagedResourcesStringResources, ResourcePickerTabResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { ResourceGroup } from './Hooks/useResourceGroups';
import { ResourceGroupWithSelection } from './ResourceGroupPicker';
import { useManagedResourcesStyles } from './Styles/ManagedResources.styles';

enum ResourceGroupListColumnKey {
    name = 'name',
    subscription = 'subscription',
    location = 'location',
    permissions = 'permissions',
    delete = 'delete',
}

enum SreAgentPermissions {
    roleAssignmentWrite = 'Microsoft.Authorization/roleAssignments/write',
    identityWrite = 'Microsoft.ManagedIdentity/userAssignedIdentities/write',
}

interface ManagedResourceGroupGridItem extends ResourceGroup {
    permissions: boolean;
}

export type ReviewTabProps = {
    selectedResourceGroups: ResourceGroupWithSelection[];
    resourceGroupPermissionsError: boolean;
    setResourceGroupPermissionsError: Dispatch<SetStateAction<boolean>>;
    resourceGroupMaxError: boolean;
    setResourceGroupMaxError: Dispatch<SetStateAction<boolean>>;
    toggleItemSelection: (id: string) => void;
    onRenderSubscription: (item: ResourceGroupWithSelection) => JSX.Element;
};

const RESOURCE_GROUP_LIMIT = 20;

const ReviewTab: FC<ReviewTabProps> = (props: ReviewTabProps) => {
    const {
        selectedResourceGroups,
        resourceGroupPermissionsError,
        setResourceGroupPermissionsError,
        resourceGroupMaxError,
        setResourceGroupMaxError,
        toggleItemSelection,
        onRenderSubscription,
    } = props;

    const portalContext = useContext(AzPortalContext);
    const intl = useIntl();

    const styles = useManagedResourcesStyles();
    const [managedResourceGroups, setManagedResourceGroups] = useState<ManagedResourceGroupGridItem[]>([]);

    const checkHasPermissionToCreateIdentities = useCallback(async (resourceGroup: string) => {
        return PermissionClient.getInstance().hasPermission(resourceGroup, [
            SreAgentPermissions.roleAssignmentWrite,
            SreAgentPermissions.identityWrite,
        ]);
    }, []);

    const runResourceGroupPermissionChecks = useCallback(async () => {
        const resourceGroupsWithPermissionChecks: ManagedResourceGroupGridItem[] = await Promise.all(
            selectedResourceGroups.map(async rg => {
                const permissions = await checkHasPermissionToCreateIdentities(rg.id);
                return { ...rg, permissions };
            })
        );

        const hasUnauthorized = resourceGroupsWithPermissionChecks.some(rg => !rg.permissions);

        setManagedResourceGroups(resourceGroupsWithPermissionChecks);
        setResourceGroupPermissionsError(hasUnauthorized);
    }, [selectedResourceGroups, checkHasPermissionToCreateIdentities, setResourceGroupPermissionsError]);

    useEffect(() => {
        setResourceGroupMaxError(selectedResourceGroups.length > RESOURCE_GROUP_LIMIT);
        runResourceGroupPermissionChecks();
    }, [selectedResourceGroups, runResourceGroupPermissionChecks, setResourceGroupMaxError]);

    const onNameClick = useCallback(
        (id: string) => {
            if (id) {
                portalContext.openBlade({
                    extension: 'HubsExtension',
                    detailBlade: 'ResourceGroupOverview',
                    detailBladeInputs: {
                        id,
                    },
                });
            }
        },
        [portalContext]
    );

    const onRenderName = useCallback(
        (item: ResourceGroupWithSelection) => {
            return (
                <div className={styles.statusRow}>
                    <img src="./ResourceGroup.svg" alt="ResourceGroup" style={{ height: 16, width: 16 }} />
                    <Link style={{ userSelect: 'text' }} onClick={_e => onNameClick(item.id)}>
                        {item.name}
                    </Link>
                </div>
            );
        },
        [styles.statusRow, onNameClick]
    );

    const onRenderLocation = useCallback(
        (item: ManagedResourceGroupGridItem) => {
            return <div className={styles.row}>{getUserFriendlyLocation(item.location)}</div>;
        },
        [styles.row]
    );

    const onRenderPermissions = useCallback(
        (item: ManagedResourceGroupGridItem) => {
            return (
                <div className={styles.row}>
                    <div className={styles.iconRow}>
                        {item.permissions ? (
                            <CheckmarkCircle16Filled primaryFill={'green'} />
                        ) : (
                            <DismissCircle16Filled primaryFill={'red'} />
                        )}
                        {item.permissions ? intl.formatMessage(SreAgentResources.yes) : intl.formatMessage(SreAgentResources.no)}
                    </div>
                </div>
            );
        },
        [styles, intl]
    );

    const onDeleteClick = useCallback(
        (item: ManagedResourceGroupGridItem) => {
            toggleItemSelection(item.id);

            const newGridValues = managedResourceGroups.filter(rg => rg.id !== item.id);
            setManagedResourceGroups(newGridValues);
            const hasUnauthorized = newGridValues.some(rg => !rg.permissions);
            setResourceGroupPermissionsError(hasUnauthorized);
        },
        [managedResourceGroups, setResourceGroupPermissionsError, setManagedResourceGroups, toggleItemSelection]
    );

    const onRenderDelete = useCallback(
        (item: ManagedResourceGroupGridItem) => {
            return (
                <IconButton
                    iconProps={{ iconName: 'Delete' }}
                    onClick={() => {
                        onDeleteClick(item);
                    }}
                    style={{ height: '14px', width: '14px' }}
                />
            );
        },
        [onDeleteClick]
    );

    const columns = useMemo<IColumn[]>(() => {
        return [
            {
                key: ResourceGroupListColumnKey.name,
                name: intl.formatMessage(ManagedResourcesStringResources.resourceGroupName),
                fieldName: ResourceGroupListColumnKey.name,
                minWidth: 200,
                maxWidth: 300,
                isResizable: true,
                onRender: onRenderName,
            },
            {
                key: ResourceGroupListColumnKey.subscription,
                name: intl.formatMessage(ManagedResourcesStringResources.subscription),
                fieldName: ResourceGroupListColumnKey.subscription,
                minWidth: 175,
                maxWidth: 175,
                isResizable: true,
                onRender: onRenderSubscription,
            },
            {
                key: ResourceGroupListColumnKey.location,
                name: intl.formatMessage(ManagedResourcesStringResources.location),
                fieldName: ResourceGroupListColumnKey.location,
                minWidth: 100,
                maxWidth: 100,
                isResizable: true,
                onRender: onRenderLocation,
            },
            {
                key: ResourceGroupListColumnKey.permissions,
                name: intl.formatMessage(ResourcePickerTabResources.permissionsForRoleAssignment),
                fieldName: ResourceGroupListColumnKey.permissions,
                minWidth: 170,
                maxWidth: 170,
                isResizable: true,
                onRender: onRenderPermissions,
                isMultiline: true,
            },
            {
                key: ResourceGroupListColumnKey.delete,
                name: '',
                fieldName: ResourceGroupListColumnKey.delete,
                minWidth: 25,
                maxWidth: 25,
                isResizable: true,
                onRender: onRenderDelete,
            },
        ];
    }, [onRenderName, onRenderSubscription, onRenderLocation, onRenderPermissions, onRenderDelete, intl]);

    const errorMessage = useMemo(() => {
        if (resourceGroupMaxError) {
            return intl.formatMessage(ResourcePickerTabResources.resourceGroupMaxError);
        } else if (resourceGroupPermissionsError) {
            return intl.formatMessage(ResourcePickerTabResources.resourceGroupPermissionError);
        }
        return '';
    }, [intl, resourceGroupMaxError, resourceGroupPermissionsError]);

    return (
        <div style={{ display: 'flex', gap: '5px', flexDirection: 'column' }}>
            {errorMessage && (
                <div style={{ paddingTop: '5px', marginBottom: '-5px' }}>
                    <MessageBar messageBarType={MessageBarType.error}>{errorMessage}</MessageBar>
                </div>
            )}
            <div
                style={{ minHeight: errorMessage ? '445px' : '490px', maxHeight: errorMessage ? '445px' : '490px', overflowY: 'auto' }}
                data-is-scrollable="true"
            >
                <ShimmeredDetailsList
                    columns={columns}
                    constrainMode={ConstrainMode.horizontalConstrained}
                    items={managedResourceGroups}
                    layoutMode={DetailsListLayoutMode.justified}
                    compact={true}
                    enableShimmer={false}
                    checkboxVisibility={CheckboxVisibility.hidden}
                    useReducedRowRenderer={false}
                    styles={{
                        root: {
                            overflow: 'visible',
                            height: 'auto',
                        },
                    }}
                />
            </div>
        </div>
    );
};

export default ReviewTab;
