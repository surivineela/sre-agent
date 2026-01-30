import { FC, memo } from 'react';
import { useIntl } from 'react-intl';
import { OverviewResources } from '../../Strings/SREAgentResources';
import MetricsCard from './MetricsCard';

const MitigationMeanTimeCard: FC = () => {
    const intl = useIntl();

    return (
        <MetricsCard
            title={intl.formatMessage(OverviewResources.meanTimeToMitigate)}
            subtitle={'Last 30 days'}
            percentageChange={-10}
            score={'12m'}
            refresh={() => Promise.resolve()}
        />
    );
};

export default memo(MitigationMeanTimeCard);
