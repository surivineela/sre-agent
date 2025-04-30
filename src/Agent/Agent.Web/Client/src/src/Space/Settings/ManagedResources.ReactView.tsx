import { DefaultButton, PrimaryButton, SearchBox, ShimmeredDetailsList } from '@fluentui/react';
import { FC, useContext } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import ConfirmDialog from '../../Common/Components/ConfirmDialog';
import { ManagedResourcesStringResources, SreAgentResources } from '../../Strings/SREAgentResources';
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
    } = useManagedResources(resourceId, az);

    return (
        <>
            <div className={styles.commandBarContainer}>
                <div className={styles.buttonsContainer}>
                    <DefaultButton
                        className={styles.buttonStyle}
                        text={intl.formatMessage(ManagedResourcesStringResources.addResourceGroup)}
                        iconProps={{ iconName: 'Add' }}
                        disabled={isLoading}
                        onClick={() => setHideResourceGroupPicker(false)}
                    />
                    <PrimaryButton
                        className={styles.buttonStyle}
                        text={intl.formatMessage(SreAgentResources.delete)}
                        iconProps={{ iconName: 'Delete' }}
                        disabled={isDeleteDisabled}
                        onClick={() => setShowDeleteConfirmationDialog(true)}
                    />
                </div>
                <div className={styles.pillsContainer}>
                    <SearchBox
                        placeholder={intl.formatMessage(SreAgentResources.search)}
                        value={searchText}
                        onChange={(_, newValue) => {
                            setSearchText(newValue || '');
                        }}
                    />
                </div>
                <ShimmeredDetailsList
                    key={managedResourceGroups?.length}
                    enableShimmer={isLoading}
                    items={managedResourceGroups || []}
                    columns={columns}
                    selection={selection.current}
                />
                <ConfirmDialog
                    primaryActionButton={{
                        title: intl.formatMessage(SreAgentResources.delete),
                        onClick: () => {
                            setShowDeleteConfirmationDialog(false);
                            onDeleteClick();
                        },
                    }}
                    defaultActionButton={{
                        title: intl.formatMessage(SreAgentResources.cancel),
                        onClick: () => setShowDeleteConfirmationDialog(false),
                    }}
                    title={intl.formatMessage(ManagedResourcesStringResources.deleteTitle)}
                    content={intl.formatMessage(ManagedResourcesStringResources.confirmDelete)}
                    onDismiss={() => setShowDeleteConfirmationDialog(false)}
                    hidden={!showDeleteConfirmationDialog}
                />
                <ResourceGroupPicker
                    subscriptionId={subscriptionId}
                    hideResourceGroupPicker={hideResourceGroupPicker}
                    existingResourceGroupIds={managedResourceGroupIds}
                    setHideResourceGroupPicker={setHideResourceGroupPicker}
                    onClick={onAddClick}
                    subscriptionOptions={subscriptionOptions}
                />
            </div>
        </>
    );
};

export default ManagedResources;
