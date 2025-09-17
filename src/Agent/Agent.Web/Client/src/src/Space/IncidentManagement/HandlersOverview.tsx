import { Shimmer } from '@fluentui/react';
import { MessageBar, MessageBarBody, MessageBarGroup } from '@fluentui/react-components';
import { CheckmarkCircle16Filled, Warning16Filled } from '@fluentui/react-icons';
import { tokens } from '@fluentui/react-theme';
import { FC, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { IncidentFilter } from '../../Common/Contracts/Azure/IncidentHandler';
import { IncidentManagementType } from '../../Common/Contracts/Azure/SreAgent';
import useUserPermissions from '../../Common/Hooks/useUserPermissions';
import { IcMResources, IncidentManagementResources, PagerDutyResources, ServiceNowResources } from '../../Strings/SREAgentResources';
import { SreAgentContext } from '../Contracts/Context';
import { useIncidentFilterFields } from '../Hooks/useIncidentFilterFields';
import { useIncidentFilters } from '../Hooks/useIncidentFilters';
import { useIncidentHandlers } from '../Hooks/useIncidentHandlers';
import { useIncidentManagementStyles } from '../Styles/IncidentManagement.styles';
import { CreateOrUpdateIncidentFilterDialog, IncidentFilterFormProps } from './CreateIncidentFilterDialog';
import { HandlerCreateOrEditInfo, OperationStatus } from './CreateIncidentHandler/Contracts';
import CreateIncidentHandlerConsolidated from './CreateIncidentHandler/CreateIncidentHandlerConsolidated';
import IncidentFiltersToolbar from './IncidentFiltersToolbar';
import IncidentsFiltersGrid from './IncidentsFiltersGrid';

interface ConnectionIndicatorProps {
    connected: boolean;
    platform?: IncidentManagementType;
    style?: React.CSSProperties | undefined;
    loading?: boolean;
}

const ConnectionIndicator: FC<ConnectionIndicatorProps> = ({ platform, connected, style, loading }) => {
    const intl = useIntl();
    let notConnectedMessage;
    let connectedMessage;
    switch (platform) {
        case IncidentManagementType.PagerDuty:
            notConnectedMessage = PagerDutyResources.notConnectedMessage;
            connectedMessage = PagerDutyResources.connectedMessage;
            break;
        case IncidentManagementType.Icm:
            notConnectedMessage = IcMResources.notConnectedMessage;
            connectedMessage = IcMResources.connectedMessage;
            break;
        case IncidentManagementType.ServiceNow:
            notConnectedMessage = ServiceNowResources.notConnectedMessage;
            connectedMessage = ServiceNowResources.connectedMessage;
            break;
        default:
            break;
    }

    if (!platform || !notConnectedMessage || !connectedMessage) {
        return null;
    }

    return (
        <div
            style={{
                display: 'flex',
                alignItems: 'center',
                gap: '4px',
                ...style,
            }}
        >
            {loading ? (
                <Shimmer width={160} />
            ) : connected ? (
                <>
                    <CheckmarkCircle16Filled
                        style={{ height: '16px', width: '16px', color: tokens.colorPaletteGreenForeground1 }}
                        aria-label={intl.formatMessage(IncidentManagementResources.connected)}
                    />
                    <div>{intl.formatMessage(connectedMessage)}</div>
                </>
            ) : (
                <>
                    <Warning16Filled
                        style={{ height: '16px', width: '16px', color: tokens.colorPaletteYellowForeground1 }}
                        aria-label={intl.formatMessage(IncidentManagementResources.notConnected)}
                    />
                    <div>{intl.formatMessage(notConnectedMessage)}</div>
                </>
            )}
        </div>
    );
};

interface ConnectionMessageBarProps {
    platform?: IncidentManagementType;
}

const ConnectionFailureMessageBar: FC<ConnectionMessageBarProps> = ({ platform }) => {
    const intl = useIntl();
    const connectionFailureMessage = useMemo(() => {
        switch (platform) {
            case IncidentManagementType.PagerDuty:
                return intl.formatMessage(PagerDutyResources.connectionFailureMessage);
            case IncidentManagementType.Icm:
                return intl.formatMessage(IcMResources.connectionFailureMessage);
            case IncidentManagementType.ServiceNow:
                return intl.formatMessage(ServiceNowResources.connectionFailureMessage);
            default:
                return undefined;
        }
    }, [platform, intl]);

    return (
        !!platform &&
        !!connectionFailureMessage && (
            <MessageBarGroup
                animate={'exit-only'}
                style={{
                    width: '100%',
                    maxWidth: '100%',
                    marginBottom: '16px',
                }}
            >
                <MessageBar
                    style={{
                        padding: '10px',
                        whiteSpace: 'normal',
                        wordBreak: 'break-word',
                        overflow: 'hidden',
                        overflowWrap: 'break-word',
                    }}
                    intent={'error'}
                >
                    <MessageBarBody
                        style={{
                            wordBreak: 'break-word',
                            overflowWrap: 'break-word',
                        }}
                    >
                        {connectionFailureMessage}
                    </MessageBarBody>
                </MessageBar>
            </MessageBarGroup>
        )
    );
};

interface HandlersOverviewProps {
    setNavigationHidden: (hidden: boolean) => void;
    useConsolidatedCreate: boolean;
}

const HandlersOverview: FC<HandlersOverviewProps> = ({ setNavigationHidden, useConsolidatedCreate }) => {
    const {
        incidentManagement: { isIncidentManagementConnected, checkingConnectivity, refreshConnectivity },
        agentObj,
    } = useContext(SreAgentContext);

    const incidentManagementType = useMemo(
        () => agentObj?.properties.incidentManagementConfiguration?.type,
        [agentObj?.properties.incidentManagementConfiguration?.type]
    );
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
                    {!checkingConnectivity && !isIncidentManagementConnected && (
                        <ConnectionFailureMessageBar platform={incidentManagementType} />
                    )}
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
                        <ConnectionIndicator
                            platform={incidentManagementType}
                            connected={isIncidentManagementConnected}
                            style={{ marginLeft: 'auto', marginRight: '16px' }}
                            loading={checkingConnectivity}
                        />
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
