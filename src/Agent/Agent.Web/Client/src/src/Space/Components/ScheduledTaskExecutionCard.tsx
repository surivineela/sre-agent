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
    Tooltip,
} from '@fluentui/react-components';
import { Clock16Regular, Copy16Regular } from '@fluentui/react-icons';
import * as React from 'react';
import { useIntl } from 'react-intl';
import { formatDateTimeWithShortYear, getSafeDateTime } from '../../Common/Helpers/Date';
import { SreAgentResources } from '../../Strings/SREAgentResources';
import { ScheduledTask } from '../Contracts/ScheduledTasks';

/**
 * Cleaner, more intuitive, Fluent UI v9-styled execution card
 * - Uses Card + clear information hierarchy
 * - Human-friendly schedule text + monospace cron pill
 * - Accurate time display (Local + UTC)
 * - Status badge with color mapping
 * - Collapsible agent prompt with copy-to-clipboard
 */

const useStyles = makeStyles({
    card: {
        backgroundColor: tokens.colorNeutralBackground2,
        ...shorthands.borderRadius(tokens.borderRadiusLarge),
        ...shorthands.border('1px', 'solid', tokens.colorBrandStroke2),
        boxShadow: `0 2px 6px rgba(0,0,0,0.12), 0 0 0 2px ${tokens.colorBrandBackground2}`,
        maxWidth: '100%',
        position: 'relative',
        '::before': {
            content: '""',
            position: 'absolute',
            inset: 0,
            pointerEvents: 'none',
            ...shorthands.borderRadius(tokens.borderRadiusLarge),
            boxShadow: `0 0 0 1px ${tokens.colorBrandStroke2}`,
        },
    },
    headerIconWrap: {
        ...shorthands.borderRadius(tokens.borderRadiusCircular),
        backgroundColor: tokens.colorBrandBackground2,
        width: '36px',
        height: '36px',
        display: 'grid',
        placeItems: 'center',
        color: tokens.colorBrandForeground1,
        marginRight: tokens.spacingHorizontalS,
        boxShadow: `0 0 0 2px ${tokens.colorNeutralBackground2}`,
    },
    headerTitle: {
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorBrandForegroundLink,
    },
    headerSubtitle: {
        color: tokens.colorNeutralForeground3,
    },
    body: {
        display: 'grid',
        rowGap: tokens.spacingVerticalS,
        padding: `${tokens.spacingVerticalS} ${tokens.spacingHorizontalM}`,
    },
    row: {
        display: 'grid',
        gridTemplateColumns: '140px 1fr',
        columnGap: tokens.spacingHorizontalM,
        alignItems: 'start',
    },
    label: {
        color: tokens.colorNeutralForeground2,
        fontWeight: tokens.fontWeightSemibold,
    },
    value: {
        color: tokens.colorNeutralForeground1,
    },
    codePill: {
        fontFamily: tokens.fontFamilyMonospace,
        backgroundColor: tokens.colorNeutralBackground4,
        ...shorthands.border('1px', 'solid', tokens.colorNeutralStroke2),
        ...shorthands.borderRadius(tokens.borderRadiusMedium),
        padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}`,
        display: 'inline-flex',
        alignItems: 'center',
        columnGap: tokens.spacingHorizontalXS,
        wordBreak: 'break-all',
    },
    sectionTitle: {
        marginTop: tokens.spacingVerticalS,
        color: tokens.colorNeutralForeground1,
        fontWeight: tokens.fontWeightSemibold,
    },
    promptBox: {
        fontFamily: tokens.fontFamilyMonospace,
        whiteSpace: 'pre-wrap',
        wordBreak: 'break-word',
        backgroundColor: tokens.colorNeutralBackground3,
        ...shorthands.border('1px', 'solid', tokens.colorNeutralStroke2),
        ...shorthands.borderRadius(tokens.borderRadiusMedium),
        padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}`,
    },
    faded: {
        color: tokens.colorNeutralForeground3,
    },
    footer: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        padding: `${tokens.spacingVerticalS} ${tokens.spacingHorizontalM}`,
    },
});

export interface ScheduledTaskExecutionCardProps {
    task: ScheduledTask;
    executionTime?: string; // ISO string or undefined
    className?: string;
    compact?: boolean;
}

// --- Helpers ---

const statusToBadge = (status?: string): { color: any; text: string } => {
    const s = (status || '').toLowerCase();
    if (s.includes('fail') || s.includes('error')) return { color: 'danger', text: status || 'Failed' } as any;
    if (s.includes('running') || s.includes('in-progress')) return { color: 'informative', text: status || 'Running' } as any;
    if (s.includes('complete') || s.includes('success')) return { color: 'success', text: status || 'Completed' } as any;
    if (s.includes('cancel')) return { color: 'warning', text: status || 'Canceled' } as any;
    if (s.includes('schedule') || s === '') return { color: 'brand', text: status || 'Scheduled' } as any;
    return { color: 'subtle', text: status || '—' } as any;
};

const pad2 = (n: number) => n.toString().padStart(2, '0');

const cronToHuman = (cron: string): string => {
    const c = cron?.trim();
    if (!c) return '—';
    const parts = c.split(/\s+/);
    if (parts.length !== 5) return c;
    const [min, hour, dom, mon, dow] = parts;

    // Common presets
    if (c === '0 0 * * *') return 'Daily at 12:00 AM';
    if (c === '0 * * * *') return 'Every hour';
    if (c === '*/15 * * * *') return 'Every 15 minutes';
    if (c === '0 0 * * 0') return 'Weekly on Sunday at 12:00 AM';
    if (c === '0 0 1 * *') return 'Monthly on the 1st at 12:00 AM';
    if (c === '0 9 * * 1-5') return 'Weekdays at 9:00 AM';

    // Every N minutes
    if (/^\*\/\d+$/.test(min) && hour === '*' && dom === '*' && mon === '*' && dow === '*') {
        return `Every ${min.slice(2)} minutes`;
    }
    // Every N hours on the hour
    if (min === '0' && /^\*\/\d+$/.test(hour) && dom === '*' && mon === '*' && dow === '*') {
        return `Every ${hour.slice(2)} hours`;
    }
    // Daily specific time
    if (dom === '*' && mon === '*' && dow === '*' && hour !== '*' && min !== '*') {
        const h = parseInt(hour, 10);
        const m = pad2(parseInt(min, 10));
        const ampm = h >= 12 ? 'PM' : 'AM';
        const disp = h === 0 ? 12 : h > 12 ? h - 12 : h;
        return `Every day at ${disp}:${m} ${ampm}`;
    }
    // Weekly day name
    if (dom === '*' && mon === '*' && dow !== '*') {
        const days = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
        const index = parseInt(dow, 10);
        const dayName = days[index] || `Day ${dow}`;
        if (min !== '*' && hour !== '*') {
            const h = parseInt(hour, 10);
            const m = pad2(parseInt(min, 10));
            const ampm = h >= 12 ? 'PM' : 'AM';
            const disp = h === 0 ? 12 : h > 12 ? h - 12 : h;
            return `Every ${dayName} at ${disp}:${m} ${ampm}`;
        }
        return `Every ${dayName}`;
    }

    return c; // Fallback
};

const toLocalAndUTC = (iso?: string): { local: string; utc: string } => {
    const now = iso ? getSafeDateTime(iso) : new Date();
    let local = '';
    try {
        local = formatDateTimeWithShortYear(now);
    } catch {
        local = now.toLocaleString();
    }
    const utc = now.toLocaleString(undefined, { timeZone: 'UTC', hour12: true });
    return { local, utc };
};

const useCollapsible = (text: string | undefined, limit = 280) => {
    const [open, setOpen] = React.useState(false);
    const safe = text ?? '';
    const needsClamp = safe.length > limit;
    const shown = open || !needsClamp ? safe : safe.slice(0, limit) + '…';
    return { open, setOpen, shown, needsClamp } as const;
};

const copyToClipboard = async (value: string) => {
    try {
        await navigator.clipboard.writeText(value);
    } catch {
        // no-op (avoid throwing in restricted contexts)
    }
};

const ScheduledTaskExecutionCard: React.FC<ScheduledTaskExecutionCardProps> = ({ task, executionTime, className }) => {
    const styles = useStyles();
    const intl = useIntl();
    const { color, text } = statusToBadge(task.status);
    const scheduleText = cronToHuman(task.cronExpression);
    const times = toLocalAndUTC(executionTime);
    const prompt = useCollapsible(task.agentPrompt, 420);

    return (
        <Card className={mergeClasses(styles.card, className)}>
            <CardHeader
                image={
                    <div className={styles.headerIconWrap}>
                        <Clock16Regular />
                    </div>
                }
                header={<Text className={styles.headerTitle}>{intl.formatMessage(SreAgentResources.scheduledTaskExecutionTitle)}</Text>}
                description={
                    <Caption1 className={styles.headerSubtitle}>
                        {intl.formatMessage(SreAgentResources.executionDetailsAndRequest)}
                    </Caption1>
                }
            />

            <Divider />

            <div className={styles.body}>
                <div className={styles.row}>
                    <Text className={styles.label}>{intl.formatMessage(SreAgentResources.task)}</Text>
                    <Text className={styles.value} weight="semibold">
                        {task.name || '—'}
                    </Text>
                </div>

                <div className={styles.row}>
                    <Text className={styles.label}>{intl.formatMessage(SreAgentResources.scheduleLabel)}</Text>
                    <div className={styles.value}>
                        <Text>{scheduleText}</Text>
                        <div style={{ marginTop: tokens.spacingVerticalXS }}>
                            <Tooltip content={intl.formatMessage(SreAgentResources.cronExpressionLabel)} relationship="label">
                                <span className={styles.codePill}>
                                    <span>{task.cronExpression || '—'}</span>
                                    {task.cronExpression && (
                                        <Button
                                            aria-label={intl.formatMessage(SreAgentResources.copyCron)}
                                            size="small"
                                            appearance="subtle"
                                            icon={<Copy16Regular />}
                                            onClick={() => copyToClipboard(task.cronExpression!)}
                                        />
                                    )}
                                </span>
                            </Tooltip>
                        </div>
                    </div>
                </div>

                <div className={styles.row}>
                    <Text className={styles.label}>{intl.formatMessage(SreAgentResources.executionTimeLabel)}</Text>
                    <div className={styles.value}>
                        <Text>{times.local}</Text>
                        <Caption1 className={styles.faded}>{times.utc} UTC</Caption1>
                    </div>
                </div>

                <div className={styles.row}>
                    <Text className={styles.label}>{intl.formatMessage(SreAgentResources.status)}</Text>
                    <div className={styles.value}>
                        <Badge appearance="filled" color={color}>
                            {text}
                        </Badge>
                    </div>
                </div>

                {task.description && (
                    <>
                        <Divider />
                        <Text className={styles.sectionTitle}>{intl.formatMessage(SreAgentResources.taskDescriptionLabel)}</Text>
                        <Text>{task.description}</Text>
                    </>
                )}

                <>
                    <Divider />
                    <Text className={styles.sectionTitle}>{intl.formatMessage(SreAgentResources.executionRequestLabel)}</Text>
                    <div className={styles.promptBox}>
                        <Text font="monospace">{prompt.shown}</Text>
                    </div>
                    {prompt.needsClamp && (
                        <Button appearance="subtle" size="small" onClick={() => prompt.setOpen(!prompt.open)}>
                            {prompt.open ? intl.formatMessage(SreAgentResources.showLess) : intl.formatMessage(SreAgentResources.showMore)}
                        </Button>
                    )}
                </>
            </div>

            <CardFooter className={styles.footer}>
                <Caption1 className={styles.faded}>ID: {task.id || '—'}</Caption1>
                {task.agentPrompt && (
                    <Button size="small" appearance="secondary" icon={<Copy16Regular />} onClick={() => copyToClipboard(task.agentPrompt!)}>
                        {intl.formatMessage(SreAgentResources.copyRequest)}
                    </Button>
                )}
            </CardFooter>
        </Card>
    );
};

export default ScheduledTaskExecutionCard;
