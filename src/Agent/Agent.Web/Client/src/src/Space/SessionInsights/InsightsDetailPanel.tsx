import { Button, Text } from '@fluentui/react-components';
import { Open20Regular } from '@fluentui/react-icons';
import { FC, useCallback, useContext } from 'react';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { PrimaryNavItemValues } from '../Contracts/SreAgentSpace';
import { useAgentSiteNavigate } from '../Hooks/useAgentSiteNavigate';
import SessionInsightBody from './SessionInsightBody';
import { SessionInsightData } from './SessionInsightTypes';
import { useStyles } from './Styles/InsightsDetailPanel.styles';

interface InsightsDetailPanelProps {
    insight: SessionInsightData;
    onInsightsGenerated?: () => void;
}

const InsightsDetailPanel: FC<InsightsDetailPanelProps> = ({ insight, onInsightsGenerated }) => {
    const styles = useStyles();
    const navigate = useAgentSiteNavigate();
    const { sreAgentEndpoint } = useContext(EnvironmentContext);

    const handleGoToThread = useCallback(() => {
        navigate({
            primaryNavItemValue: PrimaryNavItemValues.Threads,
            threadId: insight.threadId,
        });
    }, [navigate, insight.threadId]);

    const formatDate = (dateString: string | undefined) => {
        if (!dateString) return 'Unknown';
        try {
            const date = new Date(dateString);
            return date.toLocaleString('en-US', {
                month: 'short',
                day: 'numeric',
                year: 'numeric',
                hour: '2-digit',
                minute: '2-digit',
            });
        } catch {
            return 'Invalid date';
        }
    };

    return (
        <div className={styles.container}>
            <div className={styles.header}>
                <div className={styles.headerTop}>
                    <Text className={styles.threadTitle}>{insight.title}</Text>
                    <Button appearance="primary" icon={<Open20Regular />} onClick={handleGoToThread}>
                        Go to Thread
                    </Button>
                </div>
                <div className={styles.threadMeta}>
                    <span>Generated: {formatDate(insight.generatedTimestamp)}</span>
                </div>
            </div>
            <div className={styles.content}>
                <div className={styles.contentInner}>
                    <SessionInsightBody insight={insight} sreAgentEndpoint={sreAgentEndpoint} onFeedbackSaved={onInsightsGenerated} />
                </div>
            </div>
        </div>
    );
};

export default InsightsDetailPanel;
