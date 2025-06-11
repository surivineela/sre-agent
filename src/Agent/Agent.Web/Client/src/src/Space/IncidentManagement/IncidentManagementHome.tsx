import { FC, useState } from 'react';

import { useIntl } from 'react-intl';
import { IncidentFilter } from '../../Common/Contracts/Azure/IncidentHandler';
import { IncidentManagementResources } from '../../Strings/SREAgentResources';
import { useIncidentFilterFields } from '../Hooks/useIncidentFilterFields';
import { useIncidentFilters } from '../Hooks/useIncidentFilters';
import { useIncidentHandlers } from '../Hooks/useIncidentHandlers';
import { useIncidentManagementStyles } from '../Styles/IncidentManagement.styles';
import { CreateIncidentFilterDialog } from './CreateIncidentFilterDialog';
import IncidentFiltersToolbar from './IncidentFiltersToolbar';
import IncidentsFiltersGrid from './IncidentsFiltersGrid';
interface IncidentManagementHomeProps {
    openHandlerCreate?: () => void;
}

const IncidentManagementHome: FC<IncidentManagementHomeProps> = ({ openHandlerCreate }) => {
    const intl = useIntl();
    const styles = useIncidentManagementStyles();

    const [isCreateIncidentFilterDialogOpen, setIsCreateIncidentFilterDialogOpen] = useState<boolean>(false);
    const [selectedIncidentFilter, setSelectedIncidentFilter] = useState<IncidentFilter | undefined>();

    const {
        refresh: refreshIncidentFilters,
        incidentFilters,
        incidentFiltersLoading,
        deleteIncidentFilter,
        createIncidentFilter,
        enableIncidentFilter,
        disableIncidentFilter,
    } = useIncidentFilters();
    const { filterIdToHandlerMap, refresh: refreshIncidentHandlers } = useIncidentHandlers();
    const { incidentTypeOptions, impactedServiceOptions, priorityOptions } = useIncidentFilterFields();

    const refresh = () => {
        refreshIncidentFilters();
        refreshIncidentHandlers();
    };

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
                        setIsCreateIncidentFilterDialogOpen(true);
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
                />
                <IncidentsFiltersGrid
                    openHandlerCreate={openHandlerCreate}
                    incidentFilters={incidentFilters ?? []}
                    incidentFiltersLoading={incidentFiltersLoading}
                    setSelectedFilter={setSelectedIncidentFilter}
                    setIsCreateIncidentFilterDialogOpen={setIsCreateIncidentFilterDialogOpen}
                    filterIdToHandlerMap={filterIdToHandlerMap}
                />
                <CreateIncidentFilterDialog
                    isDialogOpen={isCreateIncidentFilterDialogOpen}
                    setIsDialogOpen={setIsCreateIncidentFilterDialogOpen}
                    createIncidentFilter={createIncidentFilter}
                    priorityOptions={priorityOptions}
                    incidentTypeOptions={incidentTypeOptions}
                    impactedServiceOptions={impactedServiceOptions}
                />
            </div>
        </div>
    );
};

export default IncidentManagementHome;
