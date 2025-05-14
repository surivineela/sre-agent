import { ShimmeredDetailsList } from '@fluentui/react';
import {
    Button,
    Dialog,
    DialogActions,
    DialogBody,
    DialogSurface,
    DialogTitle,
    InputOnChangeData,
    SearchBox,
    SearchBoxChangeEvent,
} from '@fluentui/react-components';
import { Add16Regular, Delete16Regular } from '@fluentui/react-icons';
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
    } = useManagedResources(resourceId, az);

    return (
        <div className={styles.container}>
            <div className={styles.header}>{intl.formatMessage(SettingsTabResources.managedResources)}</div>
            <div className={styles.buttonsContainer}>
                <Button
                    className={styles.buttonStyle}
                    icon={<Add16Regular />}
                    appearance="outline"
                    disabled={isLoading}
                    onClick={() => setHideResourceGroupPicker(false)}
                >
                    {intl.formatMessage(ManagedResourcesStringResources.add)}
                </Button>
                <Button
                    appearance="primary"
                    className={styles.buttonStyle}
                    icon={<Delete16Regular />}
                    disabled={isDeleteDisabled}
                    onClick={() => setShowDeleteConfirmationDialog(true)}
                >
                    {intl.formatMessage(SreAgentResources.delete)}
                </Button>
            </div>
            <div className={styles.pillsContainer}>
                <SearchBox
                    className={styles.searchBox}
                    placeholder={intl.formatMessage(ManagedResourcesStringResources.searchForResourceGroups)}
                    value={searchText}
                    onChange={debounce((_event: SearchBoxChangeEvent, data: InputOnChangeData) => setSearchText(data.value ?? ''))}
                />
            </div>
            <ShimmeredDetailsList
                key={managedResourceGroups?.length}
                enableShimmer={isLoading}
                items={managedResourceGroups || []}
                columns={columns}
                selection={selection.current}
                className={styles.detailsList}
            />
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
