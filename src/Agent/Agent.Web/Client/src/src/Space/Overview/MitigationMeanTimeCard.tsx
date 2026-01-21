import { ArrowTrendingSettingsRegular } from '@fluentui/react-icons';
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
            footer={{
                icon: <ArrowTrendingSettingsRegular />,
                text: 'Projected savings',
                result: '40 hrs',
            }}
        />
    );
};

export default memo(MitigationMeanTimeCard);
