import {
    Badge,
    Button,
    Caption1,
    Card,
    CardFooter,
    CardHeader,
    Divider,
    makeStyles,
    mergeClasses,
    shorthands,
    Text,
    tokens,
} from '@fluentui/react-components';
import { CheckmarkCircle16Filled, Copy16Regular } from '@fluentui/react-icons';
import * as React from 'react';
import { useIntl } from 'react-intl';
import { formatDateTimeWithShortYear, getSafeDateTime } from '../../Common/Helpers/Date';
import { GenericErrorResources, ScheduledTasksResources } from '../../Strings/SREAgentResources';

interface ScheduledTaskCreationData {
    taskId: string;
    taskName: string;
    description: string;
    cronExpression: string;
    agentPrompt: string;
    status: string;
    durationText: string;
    maxExecutionsText: string;
    createdAt: string;
}

interface ScheduledTaskCreationCardProps {
    data: ScheduledTaskCreationData;
    className?: string;
}

const useStyles = makeStyles({
    card: {
        backgroundColor: tokens.colorNeutralBackground2,
        ...shorthands.borderRadius(tokens.borderRadiusLarge),
        ...shorthands.border('1px', 'solid', tokens.colorNeutralStroke2),
        maxWidth: '100%',
        position: 'relative',
        paddingLeft: tokens.spacingHorizontalS, // extra space after accent bar
        '::before': {
            content: '""',
            position: 'absolute',
            left: 0,
            top: 0,
            bottom: 0,
            width: '4px',
            background: tokens.colorBrandBackground,
            ...shorthands.borderRadius(tokens.borderRadiusLarge, 0, 0, tokens.borderRadiusLarge),
        },
    },
    headerIconWrap: {
        ...shorthands.borderRadius(tokens.borderRadiusCircular),
        backgroundColor: tokens.colorPaletteGreenBackground2,
        width: '32px',
        height: '32px',
        display: 'grid',
        placeItems: 'center',
        color: tokens.colorPaletteGreenForeground2,
        marginRight: tokens.spacingHorizontalS,
    },
    headerTitle: {
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorBrandForegroundLink,
    },
    headerSubtitle: {
        color: tokens.colorNeutralForeground3,
    },
    body: {
        ...shorthands.padding(tokens.spacingVerticalM, tokens.spacingHorizontalM),
        display: 'grid',
        rowGap: tokens.spacingVerticalS,
    },
    row: {
        display: 'grid',
        gridTemplateColumns: '120px 1fr',
        columnGap: tokens.spacingHorizontalM,
        alignItems: 'start',
    },
    label: {
        color: tokens.colorNeutralForeground3,
        fontWeight: tokens.fontWeightSemibold,
    },
    value: {
        textAlign: 'left',
    },
    footer: {
        ...shorthands.padding(tokens.spacingVerticalS, tokens.spacingHorizontalM),
    },
    faded: {
        color: tokens.colorNeutralForeground3,
    },
    codePill: {
        backgroundColor: tokens.colorNeutralBackground4,
        ...shorthands.borderRadius(tokens.borderRadiusSmall),
        ...shorthands.padding(tokens.spacingVerticalXXS, tokens.spacingHorizontalXS),
        fontFamily: tokens.fontFamilyMonospace,
        fontSize: tokens.fontSizeBase200,
        display: 'inline-flex',
        alignItems: 'center',
        ...shorthands.gap(tokens.spacingHorizontalXS),
    },
    sectionTitle: {
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground2,
        marginTop: tokens.spacingVerticalS,
        marginBottom: tokens.spacingVerticalXS,
    },
    promptBox: {
        backgroundColor: tokens.colorNeutralBackground4,
        ...shorthands.borderRadius(tokens.borderRadiusSmall),
        ...shorthands.padding(tokens.spacingVerticalS, tokens.spacingHorizontalS),
        fontFamily: tokens.fontFamilyMonospace,
        fontSize: tokens.fontSizeBase200,
        lineHeight: tokens.lineHeightBase200,
        maxHeight: '120px',
        overflowY: 'auto',
        whiteSpace: 'pre-wrap',
        wordBreak: 'break-word',
    },
});

const copyToClipboard = async (value: string) => {
    try {
        await navigator.clipboard.writeText(value);
    } catch {
        // no-op (avoid throwing in restricted contexts)
    }
};

const cronToHuman = (cron: string): string => {
    const cronMap: { [key: string]: string } = {
        '0 * * * *': 'Every hour',
        '*/5 * * * *': 'Every 5 minutes',
        '*/10 * * * *': 'Every 10 minutes',
        '*/15 * * * *': 'Every 15 minutes',
        '*/30 * * * *': 'Every 30 minutes',
        '0 */2 * * *': 'Every 2 hours',
        '0 */6 * * *': 'Every 6 hours',
        '0 */12 * * *': 'Every 12 hours',
        '0 0 * * *': 'Daily at midnight',
        '0 9 * * *': 'Daily at 9 AM',
        '0 0 * * 0': 'Weekly on Sunday',
        '0 0 1 * *': 'Monthly on 1st',
    };

    if (cronMap[cron]) return cronMap[cron];

    // */N minute pattern
    const minutePattern = cron.match(/^\*\/(\d+) \* \* \* \*$/);
    if (minutePattern) {
        const n = minutePattern[1];
        return `Every ${n} minute${n === '1' ? '' : 's'}`;
    }

    return cron; // fallback
};

const ScheduledTaskCreationCard: React.FC<ScheduledTaskCreationCardProps> = ({ data, className }) => {
    const styles = useStyles();
    const intl = useIntl();
    const scheduleText = cronToHuman(data.cronExpression);
    const createdTime = getSafeDateTime(data.createdAt);
    const formattedTime = createdTime ? formatDateTimeWithShortYear(createdTime) : intl.formatMessage(GenericErrorResources.justNow);

    return (
        <Card className={mergeClasses(styles.card, className)}>
            <CardHeader
                image={
                    <div className={styles.headerIconWrap}>
                        <CheckmarkCircle16Filled />
                    </div>
                }
                header={<Text className={styles.headerTitle}>{intl.formatMessage(ScheduledTasksResources.taskCreatedSuccessfully)}</Text>}
                description={
                    <Caption1 className={styles.headerSubtitle}>
                        {intl.formatMessage(ScheduledTasksResources.taskCreatedSuccessfully)}
                    </Caption1>
                }
            />

            <Divider />

            <div className={styles.body}>
                <div className={styles.row}>
                    <Text className={styles.label}>{intl.formatMessage(ScheduledTasksResources.name)}</Text>
                    <Text className={styles.value} weight="semibold">
                        {data.taskName}
                    </Text>
                </div>

                {data.description && (
                    <div className={styles.row}>
                        <Text className={styles.label}>{intl.formatMessage(ScheduledTasksResources.description)}</Text>
                        <Text className={styles.value}>{data.description}</Text>
                    </div>
                )}

                <div className={styles.row}>
                    <Text className={styles.label}>{intl.formatMessage(ScheduledTasksResources.scheduleSection)}</Text>
                    <div className={styles.value}>
                        <Text>{scheduleText}</Text>
                        <div style={{ marginTop: tokens.spacingVerticalXS }}>
                            <span className={styles.codePill}>
                                <span>{data.cronExpression}</span>
                                <Button
                                    aria-label={intl.formatMessage(ScheduledTasksResources.customCronExpression)}
                                    size="small"
                                    appearance="subtle"
                                    icon={<Copy16Regular />}
                                    onClick={() => copyToClipboard(data.cronExpression)}
                                />
                            </span>
                        </div>
                    </div>
                </div>

                <div className={styles.row}>
                    <Text className={styles.label}>
                        {intl.formatMessage(ScheduledTasksResources.status ?? ScheduledTasksResources.taskDetailsSection)}
                    </Text>
                    <div className={styles.value}>
                        <Badge appearance="filled" color="success">
                            {data.status}
                        </Badge>
                    </div>
                </div>

                <div className={styles.row}>
                    <Text className={styles.label}>{intl.formatMessage(ScheduledTasksResources.executionDetailsSection)}</Text>
                    <Text className={styles.value}>
                        {[data.durationText, data.maxExecutionsText].filter((v, i, arr) => v && arr.indexOf(v) === i).join(' | ')}
                    </Text>
                </div>

                <div className={styles.row}>
                    <Text className={styles.label}>{intl.formatMessage(ScheduledTasksResources.createScheduledTask)}</Text>
                    <Text className={styles.value}>{formattedTime}</Text>
                </div>
            </div>

            <CardFooter className={styles.footer}>
                <Caption1 className={styles.faded}>
                    {intl.formatMessage(ScheduledTasksResources.name)} ID: {data.taskId}
                </Caption1>
            </CardFooter>
        </Card>
    );
};

export default ScheduledTaskCreationCard;
export type { ScheduledTaskCreationData };
