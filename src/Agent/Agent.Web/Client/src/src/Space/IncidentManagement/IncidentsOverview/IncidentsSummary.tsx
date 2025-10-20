import { makeStyles, Text, tokens } from '@fluentui/react-components';
import { Shimmer } from '@fluentui/react/lib/Shimmer';
import { FC, useContext, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { IncidentManagementType, IncidentStatus } from '../../../Common/Contracts/Azure/SreAgent';
import { IncidentThreadCounts, InvestigationStatus } from '../../../Common/Contracts/DataPlane/Thread';
import { IncidentManagementResources } from '../../../Strings/SREAgentResources';
import { SreAgentContext } from '../../Contracts/Context';
import { getPlatformSpecificStrings } from '../Utilities';
import { StatusLabel, StatusLabelProps } from './StatusLabel';

const useStyles = makeStyles({
    summaryRoot: {
        margin: '8px 0px',
    },
    summaryContainer: {
        display: 'flex',
        flexDirection: 'row',
        gap: '24px',
        padding: '20px 12px',
        marginLeft: '4px',
        boxShadow: '0px 1.6px 3.6px 0px #00000021, 0px 0.3px 0.9px 0px #0000001A', // TODO (andimarc): handle dark mode
        borderRadius: tokens.borderRadiusXLarge,
        overflowX: 'hidden',
        flexWrap: 'wrap',
    },
    summarySectionRoot: {
        display: 'flex',
        flexDirection: 'column',
        gap: '12px',
    },
    summaryTitle: {
        fontSize: '13px',
        fontWeight: 600,
        overflowX: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
    },
    summaryFieldsRoot: {
        display: 'flex',
        flexDirection: 'row',
        gap: '24px',
        overflowX: 'hidden',
    },
    summaryFieldsContainer: {
        display: 'flex',
        flexDirection: 'row',
        gap: '24px',
        overflowX: 'hidden',
        flexWrap: 'wrap',
    },
    summaryFieldWrapper: {
        display: 'flex',
        flexDirection: 'column',
        gap: '2px',
    },
    summaryFieldValue: {
        fontFamily: tokens.fontFamilyBase,
        fontWeight: 600,
        fontStyle: 'semibold',
        fontSize: '20px',
        leadingTrim: 'none',
        lineHeight: '28px',
        letterSpacing: 0,
        overflowX: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
    },
    divider: { height: '100%', width: '2px', backgroundColor: tokens.colorNeutralStroke1 },
});

interface SummarySectionProps {
    title: string;
    fields: { props: StatusLabelProps; value: number | undefined }[];
    loading?: boolean;
    addDivider?: boolean;
}

const SummarySection: FC<SummarySectionProps> = ({ title, fields, loading, addDivider }) => {
    const styles = useStyles();
    return (
        <div className={styles.summarySectionRoot}>
            <div className={styles.summaryTitle}>{title}</div>
            <div className={styles.summaryFieldsRoot}>
                <div className={styles.summaryFieldsContainer}>
                    {fields.map((field, index) => (
                        <div key={index} className={styles.summaryFieldWrapper}>
                            <StatusLabel {...field.props} />
                            <Shimmer
                                isDataLoaded={!loading}
                                width={70}
                                styles={{
                                    root: { marginTop: '4px' },
                                    shimmerWrapper: { height: '28px' },
                                }}
                            >
                                <Text className={styles.summaryFieldValue}>{field.value !== undefined ? field.value : '-'}</Text>
                            </Shimmer>
                        </div>
                    ))}
                </div>
                {addDivider && <div className={styles.divider} />}
            </div>
        </div>
    );
};

interface IncidentsSummaryInnerProps {
    sections: Omit<SummarySectionProps, 'addDivider'>[];
}

const IncidentsSummaryInner: FC<IncidentsSummaryInnerProps> = ({ sections }) => {
    const styles = useStyles();
    const nonEmptySections = useMemo(() => sections.filter(section => section.fields.length > 0), [sections]);

    return (
        <div className={styles.summaryRoot}>
            <div className={styles.summaryContainer}>
                {nonEmptySections.map((section, index) => (
                    <SummarySection key={index} {...section} addDivider={index !== nonEmptySections.length - 1} />
                ))}
            </div>
        </div>
    );
};

export interface IncidentsSummaryProps {
    threadCounts: IncidentThreadCounts | undefined;
    loading?: boolean;
}

export const IncidentsSummary: FC<IncidentsSummaryProps> = ({ threadCounts, loading }) => {
    const intl = useIntl();
    const {
        incidentManagement: { incidentPlatformType },
    } = useContext(SreAgentContext);
    const platformSpecificStrings = useMemo(() => getPlatformSpecificStrings(incidentPlatformType), [incidentPlatformType]);

    const incidentStatusCounts = useMemo(() => {
        if (incidentPlatformType === IncidentManagementType.Icm) {
            const activeCount = threadCounts?.incidentStatusCounts.find(item => item.status === '')?.count ?? 0;
            const mitigatedCount = threadCounts?.incidentStatusCounts.find(item => item.status?.toLowerCase() === 'mitigated')?.count ?? 0;
            return [
                { status: IncidentStatus.active, value: activeCount },
                { status: IncidentStatus.mitigated, value: mitigatedCount },
            ];
        } else if (incidentPlatformType === IncidentManagementType.PagerDuty) {
            const triggeredCount = threadCounts?.incidentStatusCounts.find(item => item.status === '')?.count ?? 0;
            const acknowledgedCount =
                threadCounts?.incidentStatusCounts.find(item => item.status?.toLowerCase() === 'acknowledged')?.count ?? 0;
            return [
                { status: IncidentStatus.triggered, value: triggeredCount },
                { status: IncidentStatus.acknowledged, value: acknowledgedCount },
            ];
        } else if (incidentPlatformType === IncidentManagementType.AzMonitor) {
            const newCount = threadCounts?.incidentStatusCounts.find(item => item.status === '')?.count ?? 0;
            const acknowledgedCount =
                threadCounts?.incidentStatusCounts.find(item => item.status?.toLowerCase() === 'acknowledged')?.count ?? 0;
            return [
                { status: IncidentStatus.new, value: newCount },
                { status: IncidentStatus.acknowledged, value: acknowledgedCount },
            ];
        } else if (incidentPlatformType === IncidentManagementType.ServiceNow) {
            const newCount = threadCounts?.incidentStatusCounts.find(item => item.status?.toLowerCase() === 'new')?.count ?? 0;
            const assignedCount = threadCounts?.incidentStatusCounts.find(item => item.status?.toLowerCase() === 'assigned')?.count ?? 0;
            const inProgressCount =
                threadCounts?.incidentStatusCounts.find(item => item.status?.toLowerCase() === 'in progress')?.count ?? 0;
            return [
                { status: IncidentStatus.new, value: newCount },
                { status: IncidentStatus.assigned, value: assignedCount },
                { status: IncidentStatus.inProgress, value: inProgressCount },
            ];
        } else if (threadCounts?.incidentStatusCounts.length) {
            return threadCounts.incidentStatusCounts.map(item => {
                return {
                    status: (item.status || IncidentStatus.active) as IncidentStatus,
                    value: item.count,
                };
            });
        }

        return [];
    }, [threadCounts?.incidentStatusCounts, incidentPlatformType]);

    const investigationStatusCounts = useMemo(() => {
        const pendingUserInput =
            threadCounts?.investigationStatusCounts.find(item => item.status?.toLowerCase() === 'pendinguserinput')?.count ?? 0;
        const inProgress = threadCounts?.investigationStatusCounts.find(item => item.status?.toLowerCase() === 'inprogress')?.count ?? 0;
        const completed = threadCounts?.investigationStatusCounts.find(item => item.status?.toLowerCase() === 'completed')?.count ?? 0;

        return [
            { status: InvestigationStatus.pendingUserInput, value: pendingUserInput },
            { status: InvestigationStatus.inProgress, value: inProgress },
            { status: InvestigationStatus.complete, value: completed },
        ];
    }, [threadCounts?.investigationStatusCounts]);

    return (
        <IncidentsSummaryInner
            sections={[
                {
                    title: intl.formatMessage(platformSpecificStrings.incidentOrAlertStatusLabel),
                    fields: incidentStatusCounts.map(item => ({
                        props: { type: 'incidentStatus', status: item.status },
                        value: item.value,
                    })),
                    loading: loading,
                },
                {
                    title: intl.formatMessage(IncidentManagementResources.agentStatus),
                    fields: investigationStatusCounts.map(item => ({
                        props: { type: 'investigationStatus', status: item.status },
                        value: item.value,
                    })),
                    loading: loading,
                },
            ]}
        />
    );
};
