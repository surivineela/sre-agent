import { FC, memo } from 'react';
import { useIntl } from 'react-intl';
import { OverviewResources } from '../../Strings/SREAgentResources';
import MetricsCard from './MetricsCard';

const ReviewedIncidentsCard: FC = () => {
    const intl = useIntl();

    return (
        <MetricsCard
            title={intl.formatMessage(OverviewResources.reviewedIncidents)}
            subtitle={'Last 30 days'}
            percentageChange={-10}
            score={'80/100'}
            refresh={() => Promise.resolve()}
        />
    );
};

export default memo(ReviewedIncidentsCard);
