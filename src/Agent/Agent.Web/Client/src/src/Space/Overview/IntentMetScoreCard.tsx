import { ArrowTrendingSettingsRegular } from '@fluentui/react-icons';
import { FC, memo } from 'react';
import { useIntl } from 'react-intl';
import { OverviewResources } from '../../Strings/SREAgentResources';
import MetricsCard from './MetricsCard';

const IntentMetScoreCard: FC = () => {
    const intl = useIntl();

    return (
        <MetricsCard
            title={intl.formatMessage(OverviewResources.intentMetScore)}
            subtitle={'Last 30 days'}
            percentageChange={20}
            score={'98%'}
            footer={{
                icon: <ArrowTrendingSettingsRegular />,
                text: 'Estimated time saved',
                result: '15 hrs',
            }}
        />
    );
};

export default memo(IntentMetScoreCard);
