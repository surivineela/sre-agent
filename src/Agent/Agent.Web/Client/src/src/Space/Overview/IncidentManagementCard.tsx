import { Body1, Body1Strong, Body2Strong, Caption1, Caption1Strong, tokens } from '@fluentui-copilot/react-copilot';
import {
    Card,
    CardHeader,
    Link,
    makeStyles,
    mergeClasses,
    Skeleton,
    SkeletonItem,
    Table,
    TableBody,
    TableCell,
    TableCellLayout,
    TableHeader,
    TableHeaderCell,
    TableRow,
    typographyStyles,
} from '@fluentui/react-components';
import { FC, memo, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { InvestigationStatus, Thread, ThreadSource } from '../../Common/Contracts/DataPlane/Thread';
import { useScrollableComponentStyles } from '../../Common/Styles/Scrollable';
import { ActivitiesResources, IncidentManagementResources, OverviewResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { useThreadList } from '../Hooks/useThreadList';
import { StatusLabel } from '../IncidentManagement/IncidentsOverview/StatusLabel';

const useStyles = makeStyles({
    card: {
        height: '100%',
        display: 'flex',
        flexDirection: 'column',
    },
    tableContainer: {
        flex: 1,
        overflowY: 'auto',
        padding: `${tokens.spacingVerticalM} ${tokens.spacingHorizontalM}`,
        position: 'relative',
    },
    tableBadge: {
        marginBottom: tokens.spacingVerticalM,
        border: `${tokens.strokeWidthThin} solid ${tokens.colorNeutralStroke1}`,
        width: 'fit-content',
        padding: `${tokens.spacingVerticalXXS} ${tokens.spacingHorizontalM} ${tokens.spacingVerticalXS}`,
        borderRadius: tokens.borderRadiusLarge,
    },
    tableBadgeNumber: {
        marginLeft: tokens.spacingHorizontalXS,
        display: 'inline',
    },
    table: {
        width: '100%',
        tableLayout: 'fixed',
    },
    incidentLink: {
        ...typographyStyles.caption1,
        display: 'inline',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
        width: '100%',
    },
    tableRowLoader: {
        padding: `${tokens.spacingVerticalS} ${tokens.spacingHorizontalM}`,
    },
    emptyBodyContainer: {
        position: 'absolute',
        top: '50%',
        left: '50%',
        transform: 'translate(-50%, -50%)',
    },
    emptyBodyImage: {
        width: '150px',
        height: '150px',
        display: 'block',
    },
});

const IncidentManagementCard: FC = () => {
    const styles = useStyles();
    const intl = useIntl();
    const { scrollable } = useScrollableComponentStyles();

    const columns = [
        { key: 'title', label: intl.formatMessage(IncidentManagementResources.incidentTitle) },
        { key: 'investigationStatus', label: intl.formatMessage(IncidentManagementResources.agentStatus) },
    ];

    const includedSources = useMemo(() => [ThreadSource.incident], []);
    const {
        threads,
        moreThreadsToLoad,
        isLoadingInitialThreads,

        threadListDivRef,
        intersectionObserverRef,

        onScroll,
    } = useThreadList(false, undefined, includedSources, undefined, undefined, undefined, 'modifiedTimestamp');

    const pendingUserInputCount = useMemo(
        () => threads.filter(thread => thread.incidentDetails?.investigationStatus === InvestigationStatus.pendingUserInput).length,
        [threads]
    );

    const noIncidents = !isLoadingInitialThreads && threads.length === 0;

    return (
        <Card size={'small'} className={styles.card}>
            <CardHeader header={<Body1Strong>{intl.formatMessage(OverviewResources.incidentManagement)}</Body1Strong>} />
            <div className={mergeClasses(styles.tableContainer, scrollable)} ref={threadListDivRef} onScroll={onScroll}>
                <div className={styles.tableBadge}>
                    <Caption1>{intl.formatMessage(IncidentManagementResources.pendingUserInput)}</Caption1>
                    <div className={styles.tableBadgeNumber}>
                        <Body2Strong>{pendingUserInputCount}</Body2Strong>
                    </div>
                </div>
                <Table size="small" className={styles.table}>
                    <TableHeader>
                        <TableRow>
                            {columns.map(column => (
                                <TableHeaderCell key={column.key}>
                                    <Caption1Strong>{column.label}</Caption1Strong>
                                </TableHeaderCell>
                            ))}
                        </TableRow>
                    </TableHeader>
                    <TableBody>
                        {threads.map(thread => (
                            <IncidentTableRow key={thread.id} thread={thread} />
                        ))}
                    </TableBody>
                </Table>
                {noIncidents && <EmptyBody />}
                {moreThreadsToLoad && (
                    <div ref={intersectionObserverRef}>
                        <TableRowLoader />
                    </div>
                )}
            </div>
        </Card>
    );
};

interface IncidentTableRowProps {
    thread: Thread;
}

const IncidentTableRow = memo(({ thread }: IncidentTableRowProps) => {
    const styles = useStyles();
    const incidentTitle = thread.incidentDetails?.incidentTitle || thread.title;
    const investigationStatus = thread.incidentDetails?.investigationStatus;

    return (
        <TableRow>
            <TableCell>
                <TableCellLayout truncate>
                    <Link className={styles.incidentLink} title={incidentTitle}>
                        {incidentTitle}
                    </Link>
                </TableCellLayout>
            </TableCell>
            <TableCell>
                <TableCellLayout truncate>
                    {investigationStatus ? <StatusLabel type="investigationStatus" status={investigationStatus} /> : '-'}
                </TableCellLayout>
            </TableCell>
        </TableRow>
    );
});

const TableRowLoader = memo(() => {
    const styles = useStyles();
    const intl = useIntl();

    return (
        <Skeleton aria-label={intl.formatMessage(ActivitiesResources.threadsLoadingSkeletonAriaLabel)} className={styles.tableRowLoader}>
            <Table size="small" className={styles.table}>
                <TableBody>
                    {Array.from({ length: 3 }, (_, index) => (
                        <TableRow key={index}>
                            <TableCell>
                                <SkeletonItem size={12} />
                            </TableCell>
                            <TableCell>
                                <SkeletonItem size={12} />
                            </TableCell>
                        </TableRow>
                    ))}
                </TableBody>
            </Table>
        </Skeleton>
    );
});

const EmptyBody = memo(() => {
    const intl = useIntl();
    const styles = useStyles();

    return (
        <div className={styles.emptyBodyContainer}>
            <img
                src={'AiSearchWarningSpotIllustration.svg'}
                alt={intl.formatMessage(SreAgentResources.warning)}
                className={styles.emptyBodyImage}
            />
            <Body1 block={true} align={'center'}>
                {intl.formatMessage(IncidentManagementResources.noIncidentsFound)}
            </Body1>
        </div>
    );
});

export default memo(IncidentManagementCard);
