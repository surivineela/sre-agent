import { FC, useCallback, useContext, useEffect, useState } from 'react';
import { useIntl } from 'react-intl';
import { SpecialControlValue } from '../../Common/AzPortalProxy/Models/IAmplitude';
import { useAzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { IncidentFilter } from '../../Common/Contracts/Azure/IncidentHandler';
import useUserPermissions from '../../Common/Hooks/useUserPermissions';
import { IncidentManagementResources } from '../../Strings/SREAgentResources';
import { SreAgentContext } from '../Contracts/Context';
import { useIncidentFilterFields } from '../Hooks/useIncidentFilterFields';
import { useIncidentFilters } from '../Hooks/useIncidentFilters';
import { useIncidentHandlers } from '../Hooks/useIncidentHandlers';
import { useIncidentManagementStyles } from '../Styles/IncidentManagement.styles';
import { PlatformConnectionIndicator } from './Common/PlatformConnectionIndicator';
import { PlatformConnectionMessageBar } from './Common/PlatformConnectionMessageBar';
import { CreateOrUpdateIncidentFilterDialog, IncidentFilterFormProps } from './CreateIncidentFilterDialog';
import { HandlerCreateOrEditInfo, OperationStatus } from './CreateIncidentHandler/Contracts';
import CreateIncidentHandlerConsolidated from './CreateIncidentHandler/CreateIncidentHandlerConsolidated';
import IncidentFiltersToolbar from './IncidentFiltersToolbar';
import IncidentsFiltersGrid from './IncidentsFiltersGrid';

interface HandlersOverviewProps {
    setNavigationHidden: (hidden: boolean) => void;
    useConsolidatedCreate: boolean;
}

const HandlersOverview: FC<HandlersOverviewProps> = ({ setNavigationHidden, useConsolidatedCreate }) => {
    const { logAmplitudeControlEvent } = useAzPortalContext();
    const {
        incidentManagement: { isIncidentManagementConnected, checkingConnectivity, refreshConnectivity },
    } = useContext(SreAgentContext);

    const { canWriteIncidentManagement, canDeleteIncidentManagement } = useUserPermissions();

    const intl = useIntl();
    const styles = useIncidentManagementStyles();

    const [isCreateIncidentFilterDialogOpen, setIsCreateIncidentFilterDialogOpen] = useState<boolean>(false);
    const [selectedIncidentFilter, setSelectedIncidentFilter] = useState<IncidentFilter | undefined>();
    const [isEditFilterMode, setIsEditFilterMode] = useState<boolean>(false);
    const [initialValues, setInitialValues] = useState<IncidentFilterFormProps | undefined>(undefined);

    const {
        refresh: refreshIncidentFilters,
        incidentFilters,
        incidentFiltersLoading,
        deleteIncidentFilter,
        createIncidentFilter,
        updateIncidentFilter,
        enableIncidentFilter,
        disableIncidentFilter,
    } = useIncidentFilters();
    const { filterIdToHandlerMap, refresh: refreshIncidentHandlers } = useIncidentHandlers();
    const { incidentTypeOptions, impactedServiceOptions, priorityOptions } = useIncidentFilterFields();

    const [isRefreshNeeded, setIsRefreshNeeded] = useState<boolean>(false);

    const refresh = useCallback(() => {
        refreshIncidentFilters();
        refreshIncidentHandlers();
        refreshConnectivity();
    }, [refreshIncidentFilters, refreshIncidentHandlers, refreshConnectivity]);

    const [handlerCreateOrEditInfo, setHandlerCreateOrEditInfo] = useState<HandlerCreateOrEditInfo>();
    const [handlerOperationStatus, setHandlerOperationStatus] = useState<OperationStatus | undefined>(undefined);

    const setVisibleHandler = useCallback(
        (info: HandlerCreateOrEditInfo | undefined) => {
            setHandlerCreateOrEditInfo(info);
            setNavigationHidden(!!info && !info.filter);
        },
        [setNavigationHidden]
    );

    useEffect(() => {
        if (handlerOperationStatus === 'succeeded') {
            setIsRefreshNeeded(true);
        }
    }, [handlerOperationStatus]);

    useEffect(() => {
        if (isRefreshNeeded) {
            refresh();
            setIsRefreshNeeded(false);
        }
    }, [refresh, isRefreshNeeded]);

    return handlerCreateOrEditInfo ? (
        <CreateIncidentHandlerConsolidated
            exitToHome={() => setVisibleHandler(undefined)}
            setHandlerOperationStatus={setHandlerOperationStatus}
            handlerCreateOrEditInfo={handlerCreateOrEditInfo}
        />
    ) : (
        <div className={styles.navPanelWrapper}>
            <div className={styles.navPanelContent}>
                <div className={styles.navPanelPadding}>
                    <PlatformConnectionMessageBar />
                    <div className={styles.description}>
                        {intl.formatMessage(IncidentManagementResources.incidentManagementTabDescription)}
                    </div>
                    <div style={{ display: 'flex', flexDirection: 'row', alignItems: 'center' }}>
                        <IncidentFiltersToolbar
                            onRefreshClick={() => {
                                refresh();
                            }}
                            onDeleteIncidentFilterClick={() => {
                                deleteIncidentFilter(selectedIncidentFilter?.id ?? '');
                            }}
                            onNewIncidentFilterClick={() => {
                                setIsEditFilterMode(false);
                                setInitialValues(undefined);
                                if (useConsolidatedCreate) {
                                    setVisibleHandler({});
                                } else {
                                    setIsCreateIncidentFilterDialogOpen(true);
                                }

                                logAmplitudeControlEvent({
                                    targetAction: 'clicked',
                                    targetType: 'button',
                                    targetName: 'newIncidentHandler',
                                    targetFriendlyName: 'New incident handler',
                                    valueObjectName: SpecialControlValue.DoAction,
                                    valueObjectFriendlyName: SpecialControlValue.DoAction,
                                    metadata: { useConsolidatedCreate, incidentHandlersCount: incidentFilters?.length ?? 0 },
                                });
                            }}
                            onTurnOffIncidentFilterClick={() => {
                                if (selectedIncidentFilter?.isEnabled) {
                                    disableIncidentFilter(selectedIncidentFilter?.id ?? '').then(() =>
                                        setSelectedIncidentFilter(undefined)
                                    );
                                } else {
                                    enableIncidentFilter(selectedIncidentFilter?.id ?? '').then(() => setSelectedIncidentFilter(undefined));
                                }
                            }}
                            isFilterSelected={!!selectedIncidentFilter}
                            isFilterEnabled={!selectedIncidentFilter || selectedIncidentFilter?.isEnabled}
                            connected={!checkingConnectivity && isIncidentManagementConnected}
                            canWriteIncidentManagement={canWriteIncidentManagement}
                            canDeleteIncidentManagement={canDeleteIncidentManagement}
                        />
                        <PlatformConnectionIndicator style={{ marginLeft: 'auto', marginRight: '16px' }} />
                    </div>
                    <IncidentsFiltersGrid
                        handlerOperationStatus={handlerOperationStatus}
                        openHandlerCreate={setVisibleHandler}
                        incidentFilters={incidentFilters ?? []}
                        incidentFiltersLoading={incidentFiltersLoading || checkingConnectivity}
                        setSelectedFilter={setSelectedIncidentFilter}
                        setIsCreateIncidentFilterDialogOpen={setIsCreateIncidentFilterDialogOpen}
                        filterIdToHandlerMap={filterIdToHandlerMap}
                        setIsEditFilterMode={setIsEditFilterMode}
                        setInitialValues={setInitialValues}
                        useConsolidatedCreate={useConsolidatedCreate}
                        disabled={!checkingConnectivity && !isIncidentManagementConnected}
                        canWriteIncidentManagement={canWriteIncidentManagement}
                    />
                    <CreateOrUpdateIncidentFilterDialog
                        isDialogOpen={isCreateIncidentFilterDialogOpen}
                        setIsDialogOpen={setIsCreateIncidentFilterDialogOpen}
                        createIncidentFilter={createIncidentFilter}
                        updateIncidentFilter={updateIncidentFilter}
                        priorityOptions={priorityOptions}
                        incidentTypeOptions={incidentTypeOptions}
                        impactedServiceOptions={impactedServiceOptions}
                        isEditMode={isEditFilterMode}
                        initialValues={initialValues}
                    />
                </div>
            </div>
        </div>
    );
};

export default HandlersOverview;
