import { Button, makeStyles, tokens } from '@fluentui/react-components';
import { memo } from 'react';
import { FormattedMessage } from 'react-intl';
import { SreAgentResources } from '../../Strings/SREAgentResources';
import IncidentStatusBar from '../Activities/IncidentStatusBar';
import { ThreadFilter } from '../Contracts/Activities';
import { IncidentMetrics } from '../Hooks/useMetrics';

const useThreadFiltersAndIncidentStatusStyles = makeStyles({
    root: {
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
        padding: '15px 0px',
        margin: '0px 15px',
    },
    filterContainer: {
        display: 'flex',
        alignItems: 'center',
        gap: `${tokens.spacingHorizontalS}`,
        flexWrap: 'wrap',
    },
});

const ThreadFiltersAndIncidentStatus = ({
    threadFilters,
    updateThreadFilters,
    incidentMetrics,
}: {
    threadFilters: Set<ThreadFilter>;
    updateThreadFilters: (filter: ThreadFilter) => void;
    incidentMetrics?: IncidentMetrics;
}) => {
    const { root, filterContainer } = useThreadFiltersAndIncidentStatusStyles();

    return (
        <div className={root}>
            <div className={filterContainer}>
                <ThreadFilterButton
                    threadFilter={ThreadFilter.Incidents}
                    isSelected={threadFilters.has(ThreadFilter.Incidents)}
                    updateThreadFilter={updateThreadFilters}
                />
                <ThreadFilterButton
                    threadFilter={ThreadFilter.Unread}
                    isSelected={threadFilters.has(ThreadFilter.Unread)}
                    updateThreadFilter={updateThreadFilters}
                />
            </div>
            {threadFilters.has(ThreadFilter.Incidents) && <IncidentStatusBar incidentMetrics={incidentMetrics} />}
        </div>
    );
};

const ThreadFilterButton = memo(
    ({
        isSelected,
        threadFilter,
        updateThreadFilter,
    }: {
        isSelected: boolean;
        threadFilter: ThreadFilter;
        updateThreadFilter: (filter: ThreadFilter) => void;
    }) => {
        return (
            <Button
                shape={'circular'}
                size={'small'}
                appearance={isSelected ? 'primary' : 'outline'}
                onClick={() => {
                    updateThreadFilter(threadFilter);
                }}
            >
                <FormattedMessage {...(threadFilter === ThreadFilter.Incidents ? SreAgentResources.incidents : SreAgentResources.unread)} />
            </Button>
        );
    }
);

export default memo(ThreadFiltersAndIncidentStatus);
