import { FC, useCallback, useContext, useEffect, useMemo, useState } from 'react';

import { useIntl } from 'react-intl';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { IncidentFilter } from '../../Common/Contracts/Azure/IncidentHandler';
import { AgentAccessLevel } from '../../Common/Contracts/Azure/SreAgent';
import { AntUxStringComparison, equals } from '../../Common/Helpers/Strings';
import { IncidentManagementResources } from '../../Strings/SREAgentResources';
import { useIncidentFilterFields } from '../Hooks/useIncidentFilterFields';
import { useIncidentFilters } from '../Hooks/useIncidentFilters';
import { useIncidentHandlers } from '../Hooks/useIncidentHandlers';
import { useSreAgent } from '../Settings/Hooks/useSreAgent';
import { useIncidentManagementStyles } from '../Styles/IncidentManagement.styles';
import { CreateOrUpdateIncidentFilterDialog, IncidentFilterFormProps } from './CreateIncidentFilterDialog';
import { HandlerCreateOrEditInfo, OperationStatus } from './CreateIncidentHandler/Contracts';
import IncidentFiltersToolbar from './IncidentFiltersToolbar';
import IncidentsFiltersGrid from './IncidentsFiltersGrid';

interface IncidentManagementHomeProps {
    openHandlerCreate: (handlerCreateOrEditInfo: HandlerCreateOrEditInfo) => void;
    handlerOperationStatus: OperationStatus | undefined;
    useConsolidatedCreate: boolean;
}

const IncidentManagementHome: FC<IncidentManagementHomeProps> = ({ openHandlerCreate, handlerOperationStatus, useConsolidatedCreate }) => {
    const intl = useIntl();
    const styles = useIncidentManagementStyles();
    const { resourceId } = useContext(EnvironmentContext);

    const { agent, agentLoaded, refresh: refreshAgent } = useSreAgent(resourceId);

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

    const isAgentHighAccessLevel = useMemo<boolean>(
        () => equals(AgentAccessLevel.high, agent?.properties.actionConfiguration?.accessLevel ?? '', AntUxStringComparison.IgnoreCase),
        [agent]
    );

    const refresh = useCallback(() => {
        refreshIncidentFilters();
        refreshIncidentHandlers();
        refreshAgent();
    }, [refreshIncidentFilters, refreshIncidentHandlers, refreshAgent]);

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

    return (
        <div className={styles.root}>
            <div className={styles.container}>
                <div className={styles.description}>{intl.formatMessage(IncidentManagementResources.incidentManagementTabDescription)}</div>
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
                            openHandlerCreate({});
                        } else {
                            setIsCreateIncidentFilterDialogOpen(true);
                        }
                    }}
                    onTurnOffIncidentFilterClick={() => {
                        if (selectedIncidentFilter?.isEnabled) {
                            disableIncidentFilter(selectedIncidentFilter?.id ?? '').then(() => setSelectedIncidentFilter(undefined));
                        } else {
                            enableIncidentFilter(selectedIncidentFilter?.id ?? '').then(() => setSelectedIncidentFilter(undefined));
                        }
                    }}
                    isFilterSelected={!!selectedIncidentFilter}
                    isFilterEnabled={!selectedIncidentFilter || selectedIncidentFilter?.isEnabled}
                    isNewIncidentFilterDisabled={!agentLoaded} // Could reset initialValues and re-init form w/o this safeguard
                />
                <IncidentsFiltersGrid
                    handlerOperationStatus={handlerOperationStatus}
                    openHandlerCreate={openHandlerCreate}
                    incidentFilters={incidentFilters ?? []}
                    incidentFiltersLoading={incidentFiltersLoading}
                    setSelectedFilter={setSelectedIncidentFilter}
                    setIsCreateIncidentFilterDialogOpen={setIsCreateIncidentFilterDialogOpen}
                    filterIdToHandlerMap={filterIdToHandlerMap}
                    setIsEditFilterMode={setIsEditFilterMode}
                    setInitialValues={setInitialValues}
                    useConsolidatedCreate={useConsolidatedCreate}
                    isAgentHighAccessLevel={isAgentHighAccessLevel}
                    isNewIncidentFilterDisabled={!agentLoaded}
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
                    isAgentHighAccessLevel={isAgentHighAccessLevel}
                />
            </div>
        </div>
    );
};

export default IncidentManagementHome;
