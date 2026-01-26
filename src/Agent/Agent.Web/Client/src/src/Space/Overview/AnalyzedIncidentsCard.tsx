import { SavingsRegular } from '@fluentui/react-icons';
import { FC, memo } from 'react';
import { useIntl } from 'react-intl';
import { OverviewResources } from '../../Strings/SREAgentResources';
import MetricsCard from './MetricsCard';

const AnalyzedIncidentsCard: FC = () => {
    const intl = useIntl();

    return (
        <MetricsCard
            title={intl.formatMessage(OverviewResources.incidentsAnalyzed)}
            subtitle={'Last 30 days'}
            percentageChange={0}
            score={'124'}
            footer={{
                icon: <SavingsRegular />,
                text: 'Estimated time saved',
                result: '100 hrs',
            }}
            refresh={() => Promise.resolve()}
        />
    );
};

export default memo(AnalyzedIncidentsCard);
