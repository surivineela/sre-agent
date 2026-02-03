import { FC, memo } from 'react';
import { useIntl } from 'react-intl';
import { OverviewResources } from '../../Strings/SREAgentResources';
import MetricsCard from './MetricsCard';

const TimeSavingCard: FC = () => {
    const intl = useIntl();

    return (
        <MetricsCard title={intl.formatMessage(OverviewResources.estimatedTimeSaved)} score={'100 h'} refresh={() => Promise.resolve()} />
    );
};

export default memo(TimeSavingCard);
