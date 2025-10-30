import {
    Body1,
    Body2,
    Body2Strong,
    Body3Strong,
    Caption1,
    Caption1Strong,
    CopilotProvider,
    CopilotTheme,
    EntityCard,
    EntityTitle,
} from '@fluentui-copilot/react-copilot';
import { VerticalBarChart, VerticalBarChartDataPoint } from '@fluentui/react-charts';
import {
    Button,
    Dialog,
    DialogActions,
    DialogSurface,
    DialogTitle,
    DialogTrigger,
    Field,
    Input,
    Link,
    makeStyles,
    MessageBar,
    MessageBarBody,
    ProgressBar,
    Skeleton,
    SkeletonItem,
    Text,
    Title2,
    tokens,
    Toolbar,
    ToolbarButton,
    useRestoreFocusTarget,
    webDarkTheme,
    webLightTheme,
} from '@fluentui/react-components';
import { ArrowCounterclockwiseRegular, EditRegular } from '@fluentui/react-icons';
import { useTheme } from '@fluentui/react/lib/Theme';
import { memo, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { getErrorMessage } from '../../Common/Clients/ArmClient';
import SreAgentClient from '../../Common/Clients/SreAgentClient';
import { DailyUsage } from '../../Common/Contracts/Azure/SreAgent';
import { getSafeDateTime } from '../../Common/Helpers/Date';
import { Guid } from '../../Common/Helpers/Guid';
import { resolveResourceIcon } from '../../Common/Helpers/Resources';
import { SettingsTabResources, SreAgentResources, UsageResources } from '../../Strings/SREAgentResources';
import { useSettingsStyles } from './Styles/Settings.styles';

const useStyles = makeStyles({
    root: {
        maxWidth: '80%',
        minWidth: '500px',
    },
    title: {
        marginBottom: '20px',
    },
    toolbar: {
        padding: `${tokens.spacingVerticalS} 0px`,
    },
    toolbarButton: {
        padding: ' 5px 0px',
    },
    sectionTitle: {
        paddingTop: tokens.spacingVerticalL,
        paddingLeft: tokens.spacingHorizontalS,
    },
    section: {
        padding: `${tokens.spacingVerticalM} 0px`,
    },
    fullWidth: {
        width: '100%',
    },
    fullHeight: {
        height: '100%',
    },
    infoCard: {
        maxWidth: 'unset',
    },
    totalUsageInfoLabel: {
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'space-between',
        alignItems: 'flex-start',
        borderLeft: `${tokens.strokeWidthThickest} solid ${tokens.colorPaletteCornflowerBorderActive}`,
        padding: `0px ${tokens.spacingHorizontalM}`,
    },
    progressBarField: {
        marginBottom: tokens.spacingVerticalS,
    },
    extraSmallSpaceOnRight: {
        paddingRight: tokens.spacingHorizontalXS,
    },
    smallSpaceOnRight: {
        paddingRight: tokens.spacingHorizontalS,
    },
    progressBar: {
        height: '13px',
        '& .fui-ProgressBar__bar': {
            backgroundColor: `${tokens.colorPaletteCornflowerBorderActive} !important`,
        },
    },
    totalConsumptionLoader: {
        height: '240px',
    },
    dailyConsumptionLoader: {
        height: '550px',
    },
    dialogDescription: {
        padding: `${tokens.spacingVerticalM} 0px`,
    },
    dialogAction: {
        marginTop: tokens.spacingVerticalL,
    },
    noDataContainer: {
        position: 'relative',
        width: '100%',
        minHeight: '100px',
    },
    noDataContent: {
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'center',
        alignItems: 'center',
        position: 'absolute',
        top: '50%',
        left: '50%',
        transform: 'translate(-50%, -50%)',
    },
});

const getNumberLocale = (num: number) => {
    return num.toLocaleString();
};

const MAX_LIMIT = 200000;

const Usage = () => {
    const theme = useTheme();
    const restoreFocusTargetAttribute = useRestoreFocusTarget();
    const intl = useIntl();

    const proxy = useContext(AzPortalContext);
    const { resourceId } = useContext(EnvironmentContext);

    const settingStyles = useSettingsStyles();
    const styles = useStyles();

    const [currentUsage, setCurrentUsage] = useState<number>();
    const [totalLimit, setTotalLimit] = useState<number>();
    const [dailyUsagesDataPoint, setDailyUsagesDataPoint] = useState<VerticalBarChartDataPoint[]>([]);
    const [isDialogOpen, setIsDialogOpen] = useState(false);
    const [isLoadingMonthlyUsage, setIsLoadingMonthlyUsage] = useState(false);
    const [isLoadingDailyUsage, setIsLoadingDailyUsage] = useState(false);
    const [isUpdating, setIsUpdating] = useState(false);
    const [monthlyUsageError, setMonthlyUsageError] = useState<boolean>(false);
    const [dailyUsageError, setDailyUsageError] = useState<boolean>(false);
    const [currentDate, setCurrentDate] = useState<Date>(new Date());

    const dailyUsageChartRef = useRef<HTMLDivElement>(null);
    const totalLimitRef = useRef<number | undefined>(totalLimit);
    totalLimitRef.current = totalLimit;

    const disableButtons = useMemo(
        () => isLoadingMonthlyUsage || isLoadingDailyUsage || isUpdating,
        [isLoadingMonthlyUsage, isLoadingDailyUsage, isUpdating]
    );
    const disableChangeButton = useMemo(() => disableButtons || monthlyUsageError, [disableButtons, monthlyUsageError]);

    const currentUsageDisplayData = useMemo(() => {
        return currentUsage !== undefined ? getNumberLocale(currentUsage) : '';
    }, [currentUsage]);

    const totalLimitDisplayData = useMemo(() => {
        return totalLimit !== undefined ? getNumberLocale(totalLimit) : '';
    }, [totalLimit]);

    const daysLeftOfCurrentMonth = useMemo(() => {
        const endOfMonth = new Date(currentDate.getFullYear(), currentDate.getMonth() + 1, 0);
        return endOfMonth.getDate() - currentDate.getDate();
    }, [currentDate]);

    const fetchData = useCallback(async () => {
        const getMonthlyUsage = async (resourceId: string) => {
            setIsLoadingMonthlyUsage(true);

            const response = await SreAgentClient.getMonthlyUsage(resourceId);
            const result = response.data.value?.[0];
            const usage = result?.currentValue || undefined;
            const limit = result?.limit || undefined;

            if (response.metadata.success && usage !== undefined && limit !== undefined) {
                setMonthlyUsageError(false);
                setCurrentUsage(usage);
                setTotalLimit(limit);
            } else {
                setMonthlyUsageError(true);
                proxy.log({
                    action: 'getMonthlyUsage',
                    actionModifier: 'failed',
                    resourceId,
                    logLevel: 'error',
                    data: {
                        error: getErrorMessage(response.metadata.error),
                    },
                });
            }

            setIsLoadingMonthlyUsage(false);
        };

        const getDailyUsages = async (resourceId: string) => {
            setIsLoadingDailyUsage(true);

            const response = await SreAgentClient.getDailyUsages(resourceId);
            const dailyUsages = response.data.value || [];

            if (response.metadata.success) {
                setDailyUsageError(false);
                setDailyUsagesDataPoint(processDailyUsages(dailyUsages));
            } else {
                setDailyUsageError(true);
                proxy.log({
                    action: 'getDailyUsages',
                    actionModifier: 'failed',
                    resourceId,
                    logLevel: 'error',
                    data: {
                        error: getErrorMessage(response.metadata.error),
                    },
                });
            }

            setIsLoadingDailyUsage(false);
        };

        const processDailyUsages = (dailyUsages: DailyUsage[]): VerticalBarChartDataPoint[] => {
            const dataPoints: VerticalBarChartDataPoint[] = dailyUsages.map(usage => {
                return {
                    x: getSafeDateTime(usage.date).toLocaleString(undefined, { month: 'numeric', day: 'numeric' }),
                    y: usage.value,
                    color: tokens.colorPaletteCornflowerBorderActive,
                    legend: intl.formatMessage(UsageResources.aauConsumptionLegendText),
                };
            });
            return dataPoints;
        };

        setCurrentDate(new Date());

        if (resourceId) {
            getMonthlyUsage(resourceId);
            getDailyUsages(resourceId);
        }
    }, [resourceId, proxy, intl]);

    const onUpdate = useCallback(
        async (newValue: number) => {
            setIsUpdating(true);

            const oldValue = totalLimitRef.current;

            const id = proxy.startNotification(
                intl.formatMessage(UsageResources.updateAllocationTitle),
                intl.formatMessage(UsageResources.updateAllocationInProgressDescription, {
                    oldValue,
                    newValue,
                })
            );

            const response = await SreAgentClient.patchAgent(resourceId, { properties: { monthlyAgentUnitLimit: newValue } });

            setIsUpdating(false);

            if (response.metadata.success) {
                proxy.stopNotification(
                    id,
                    true,
                    intl.formatMessage(UsageResources.updateAllocationSuccessDescription, { oldValue, newValue })
                );
                fetchData();
            } else {
                const errorMessage = getErrorMessage(response.metadata.error);

                proxy.log({
                    action: 'deleteThread',
                    actionModifier: 'failure',
                    logLevel: 'error',
                    resourceId: resourceId,
                    data: {
                        error: errorMessage,
                    },
                });

                proxy.stopNotification(id, false, intl.formatMessage(UsageResources.updateAllocationFailedDescription, { errorMessage }));
            }
        },
        [fetchData, proxy, resourceId, intl]
    );

    useEffect(() => {
        fetchData();
    }, [fetchData]);

    // ToDo: Set CopilotProvider on the app root level to apply copilot them to all components
    return (
        <CopilotProvider {...CopilotTheme} mode={'canvas'} theme={theme.isInverted ? webDarkTheme : webLightTheme}>
            <div className={styles.root}>
                <div className={styles.title}>
                    <div style={{ ...settingStyles.generalSettingsHeader, marginBottom: tokens.spacingVerticalS }}>
                        {intl.formatMessage(SettingsTabResources.usage)}
                    </div>
                    <div>
                        <Body1>
                            <span className={styles.smallSpaceOnRight}>{intl.formatMessage(UsageResources.description)}</span>
                            <Link href="https://go.microsoft.com/fwlink/?linkid=2339567" target="_blank">
                                {intl.formatMessage(UsageResources.descriptionLinkText)}
                            </Link>
                        </Body1>
                    </div>
                </div>
                <Toolbar size="small" className={styles.toolbar}>
                    <ToolbarButton
                        appearance="transparent"
                        icon={<ArrowCounterclockwiseRegular />}
                        className={styles.toolbarButton}
                        disabled={disableButtons}
                        onClick={() => fetchData()}
                    >
                        <Body1>{intl.formatMessage(SreAgentResources.refresh)}</Body1>
                    </ToolbarButton>
                    <ToolbarButton
                        {...restoreFocusTargetAttribute}
                        appearance="transparent"
                        icon={<EditRegular />}
                        className={styles.toolbarButton}
                        disabled={disableChangeButton}
                        onClick={() => setIsDialogOpen(true)}
                    >
                        <Body1>{intl.formatMessage(UsageResources.changeAAUAllocationText)}</Body1>
                    </ToolbarButton>
                </Toolbar>
                <div className={styles.section}>
                    <EntityCard
                        role="group"
                        entityTitle={
                            <EntityTitle
                                primaryText={
                                    <Body2>
                                        <span className={styles.smallSpaceOnRight}>
                                            {intl.formatMessage(UsageResources.monthlyAAULimitLabel)}
                                        </span>
                                        {totalLimit !== undefined && !monthlyUsageError && (
                                            <Body3Strong>
                                                {intl.formatMessage(UsageResources.billingDescription, { count: totalLimit })}
                                            </Body3Strong>
                                        )}
                                    </Body2>
                                }
                            />
                        }
                        className={styles.infoCard}
                    />
                </div>
                <div className={styles.sectionTitle}>
                    <Body2Strong block={true}>{currentDate.toLocaleString(undefined, { month: 'long', year: 'numeric' })}</Body2Strong>
                    <Caption1>{intl.formatMessage(UsageResources.activeFlowResetMessage, { days: daysLeftOfCurrentMonth })}</Caption1>
                </div>
                <div className={styles.section}>
                    {isLoadingMonthlyUsage ? (
                        <Skeleton>
                            <SkeletonItem className={styles.totalConsumptionLoader} />
                        </Skeleton>
                    ) : (
                        <EntityCard
                            role="group"
                            entityTitle={<EntityTitle primaryText={intl.formatMessage(UsageResources.totalActiveFlowConsumptionTitle)} />}
                            style={{ maxWidth: 'unset' }}
                            content={{
                                style: {
                                    maxWidth: 'unset',
                                    borderRadius: 'unset',
                                    width: '100%',
                                    display: 'flex',
                                    flexDirection: 'column',
                                    alignItems: 'flex-start',
                                    gap: tokens.spacingVerticalXXL,
                                    padding: `${tokens.spacingVerticalXXL} 0px ${tokens.spacingVerticalS} 0px`,
                                },
                            }}
                        >
                            {monthlyUsageError ? (
                                <NoDataMessage />
                            ) : (
                                <>
                                    <div className={styles.totalUsageInfoLabel}>
                                        <Caption1>{intl.formatMessage(UsageResources.consumptionAAUUsageLabel)}</Caption1>

                                        <Title2>
                                            <span style={{ color: tokens.colorPaletteCornflowerBorderActive }}>
                                                {currentUsageDisplayData}
                                            </span>
                                            <span>{'/'}</span>
                                            <span>{totalLimitDisplayData}</span>
                                        </Title2>
                                    </div>
                                    <div className={styles.fullWidth}>
                                        <Caption1 className={styles.progressBarField} block={true} align={'end'}>
                                            <Caption1Strong>{currentUsageDisplayData}</Caption1Strong>
                                            <span>{'/'}</span>
                                            <span className={styles.extraSmallSpaceOnRight}>{totalLimitDisplayData}</span>
                                            <span>{'AAUs'}</span>
                                        </Caption1>
                                        <ProgressBar value={1000} max={totalLimit} className={styles.progressBar}></ProgressBar>
                                    </div>
                                </>
                            )}
                        </EntityCard>
                    )}
                </div>
                <div className={styles.section}>
                    {isLoadingDailyUsage ? (
                        <Skeleton>
                            <SkeletonItem className={styles.dailyConsumptionLoader} />
                        </Skeleton>
                    ) : (
                        <EntityCard
                            role="group"
                            entityTitle={<EntityTitle primaryText={intl.formatMessage(UsageResources.dailyActiveFlowConsumptionTitle)} />}
                            style={{ maxWidth: 'unset' }}
                            content={{
                                ref: dailyUsageChartRef,
                                style: { maxWidth: 'unset', borderRadius: 'unset', width: '100%', minHeight: '450px' },
                            }}
                        >
                            {dailyUsageError ? (
                                <NoDataMessage />
                            ) : (
                                <div className={styles.fullWidth}>
                                    <VerticalBarChart
                                        culture={typeof window !== 'undefined' ? window.navigator.language : 'en-us'}
                                        data={dailyUsagesDataPoint}
                                        lineLegendText={intl.formatMessage(UsageResources.aauConsumptionLegendText)}
                                        useUTC={false}
                                        parentRef={dailyUsageChartRef.current}
                                    />
                                </div>
                            )}
                        </EntityCard>
                    )}
                </div>
            </div>
            {/** Add a key to make sure each time the dialog is opened, it remounts with the initial values */}
            <AllocationChangeDialog
                key={Guid.newGuid()}
                isOpen={isDialogOpen}
                onOpenChange={setIsDialogOpen}
                initialValue={totalLimit || 0}
                currentUsage={currentUsage || 0}
                changeAAUAllocation={onUpdate}
            />
        </CopilotProvider>
    );
};

const NoDataMessage = memo(() => {
    const styles = useStyles();
    const intl = useIntl();

    return (
        <div className={styles.noDataContainer}>
            <div className={styles.noDataContent}>
                <img
                    src={resolveResourceIcon('usagewarning')}
                    style={{ width: '60px', height: '60px' }}
                    alt={intl.formatMessage(SreAgentResources.warning)}
                />
                <Body2Strong>{intl.formatMessage(UsageResources.dataLoadErrorTitle)} </Body2Strong>
                <Caption1>{intl.formatMessage(UsageResources.dataLoadErrorDescription)}</Caption1>
            </div>
        </div>
    );
});

const AllocationChangeDialog = ({
    isOpen,
    onOpenChange,
    initialValue,
    currentUsage,
    changeAAUAllocation,
}: {
    isOpen: boolean;
    onOpenChange: (open: boolean) => void;
    initialValue: number;
    currentUsage: number;
    changeAAUAllocation: (newValue: number) => void;
}) => {
    const [value, setValue] = useState<number>(initialValue);
    const [errorMessage, setErrorMessage] = useState<string>('');
    const [isDirty, setIsDirty] = useState<boolean>(false);

    const styles = useStyles();

    const intl = useIntl();

    useEffect(() => {
        setValue(initialValue);
    }, [initialValue]);

    useEffect(() => {
        setIsDirty(value !== initialValue);
    }, [initialValue, value]);

    useEffect(() => {
        setErrorMessage(value > MAX_LIMIT ? intl.formatMessage(UsageResources.usageLimitErrorMessage) : '');
    }, [value, intl]);

    return (
        <Dialog
            open={isOpen}
            onOpenChange={(_, data) => {
                onOpenChange(data.open);
            }}
        >
            <DialogSurface>
                <DialogTitle>{intl.formatMessage(UsageResources.changeAAUAllocationText)}</DialogTitle>
                <div className={styles.dialogDescription}>
                    <Body1>{intl.formatMessage(UsageResources.dialogDescription)}</Body1>
                </div>
                {value < currentUsage && (
                    <MessageBar layout={'multiline'} intent={'warning'}>
                        <MessageBarBody>
                            <Text>{intl.formatMessage(UsageResources.usageLimitWarningMessage)}</Text>
                        </MessageBarBody>
                    </MessageBar>
                )}
                <div className={styles.section}>
                    <Field
                        label={intl.formatMessage(UsageResources.monthlyAAULimitLabel)}
                        orientation={'horizontal'}
                        required
                        validationState={errorMessage ? 'error' : undefined}
                        validationMessage={errorMessage}
                        hint={'Maximum 200,000 AAUs'}
                    >
                        <Input
                            type={'number'}
                            min={0}
                            max={MAX_LIMIT}
                            value={value.toString()}
                            onChange={(_, data) => {
                                if (!data.value) {
                                    setValue(0);
                                    return;
                                }
                                try {
                                    const newValue = parseInt(data.value);
                                    if (!isNaN(newValue)) {
                                        setValue(newValue);
                                    }
                                } catch {
                                    //todo: log error
                                }
                            }}
                        />
                    </Field>
                </div>
                <div className={styles.section}>
                    <Body1>
                        <span className={styles.smallSpaceOnRight}>{intl.formatMessage(UsageResources.monthlyAAULimitLabel)}</span>
                        <Body2Strong>{intl.formatMessage(UsageResources.billingDescription, { count: value })}</Body2Strong>
                    </Body1>
                </div>
                <DialogActions className={styles.dialogAction}>
                    <Button
                        appearance={'primary'}
                        disabled={!!errorMessage || !isDirty}
                        onClick={() => {
                            changeAAUAllocation(value);
                            onOpenChange(false);
                        }}
                    >
                        {intl.formatMessage(SreAgentResources.save)}
                    </Button>
                    <DialogTrigger disableButtonEnhancement>
                        <Button>{intl.formatMessage(SreAgentResources.cancel)}</Button>
                    </DialogTrigger>
                </DialogActions>
            </DialogSurface>
        </Dialog>
    );
};

export default memo(Usage);
