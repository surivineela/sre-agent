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
    Label,
    Link,
    makeStyles,
    mergeClasses,
    MessageBar,
    MessageBarBody,
    ProgressBar,
    Skeleton,
    SkeletonItem,
    Slider,
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
import { Guid } from '../../Common/Helpers/Guid';
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
    summaryLoader: {
        width: '500px',
    },
    totalActiveFlowLoader: {
        lineHeight: tokens.lineHeightHero700,
        paddingTop: tokens.spacingVerticalXS,
    },
    progressBar: {
        height: '13px',
        '& .fui-ProgressBar__bar': {
            backgroundColor: `${tokens.colorPaletteCornflowerBorderActive} !important`,
        },
    },
    progressBarLabelLoader: {
        width: '100%',
        display: 'flex',
        justifyContent: 'flex-end',
        paddingBottom: tokens.spacingVerticalS,
    },
    sliderContainer: {
        display: 'flex',
        alignItems: 'center',
    },
    dialogDescription: {
        padding: `${tokens.spacingVerticalM} 0px`,
    },
    dialogAction: {
        marginTop: tokens.spacingVerticalL,
    },
});

const getNumberLocale = (num: number) => {
    return num.toLocaleString();
};

const Usage = () => {
    const theme = useTheme();
    const restoreFocusTargetAttribute = useRestoreFocusTarget();
    const intl = useIntl();

    const proxy = useContext(AzPortalContext);
    const { resourceId } = useContext(EnvironmentContext);

    const settingStyles = useSettingsStyles();
    const styles = useStyles();

    const [currentUsage, setCurrentUsage] = useState(1000);
    const [totalLimit, setTotalLimit] = useState(5000);
    const [isDialogOpen, setIsDialogOpen] = useState(false);
    const [isLoading, setIsLoading] = useState(false);
    const [isUpdating, setIsUpdating] = useState(false);

    const totalLimitRef = useRef<number>(totalLimit);
    totalLimitRef.current = totalLimit;

    const disableButtons = useMemo(() => isLoading || isUpdating, [isLoading, isUpdating]);

    const daysLeftOfCurrentMonth = useMemo(() => {
        const today = new Date();
        const endOfMonth = new Date(today.getFullYear(), today.getMonth() + 1, 0);
        return endOfMonth.getDate() - today.getDate();
    }, []);

    // ToDo: add real data
    const usageData = useMemo(() => {
        const today = new Date();
        const oneMonthAgo = new Date();
        oneMonthAgo.setMonth(today.getMonth() - 1);
        const currentDate = new Date(oneMonthAgo);

        const dataPoints: VerticalBarChartDataPoint[] = [];
        const dates: Date[] = [];

        while (currentDate <= today) {
            const date = new Date(currentDate);
            dataPoints.push({
                x: currentDate.toLocaleString(undefined, { month: 'numeric', day: 'numeric' }),
                y: Math.floor(Math.random() * 5000), // Random usage between 0 and 5000
                color: tokens.colorPaletteCornflowerBorderActive,
                legend: intl.formatMessage(UsageResources.aauConsumptionLegendText),
            });
            dates.push(date);
            currentDate.setDate(currentDate.getDate() + 1);
        }

        return dataPoints;
    }, []);

    const onRefresh = useCallback(() => {
        setIsLoading(true);

        //ToDo: add actual api request
        setTimeout(() => {
            setCurrentUsage(1000);
            setTotalLimit(5000);
            setIsLoading(false);
        }, 3000);
    }, []);

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

            try {
                // ToDo: Add actual api request to update AAU allocation
                setTimeout(() => {
                    setIsUpdating(false);
                    proxy.stopNotification(
                        id,
                        true,
                        intl.formatMessage(UsageResources.updateAllocationSuccessDescription, { oldValue, newValue })
                    );
                    onRefresh();
                }, 3000);
            } catch (e: any) {
                setIsUpdating(false);
                proxy.log({
                    action: 'deleteThread',
                    actionModifier: 'failure',
                    logLevel: 'error',
                    resourceId: resourceId,
                    data: {
                        error: e?.message || e?.response?.data,
                    },
                });

                proxy.stopNotification(
                    id,
                    false,
                    intl.formatMessage(UsageResources.updateAllocationFailedDescription, {
                        errorMessage: e?.message || e?.response?.data,
                    })
                );
            }

            //ToDo: add actual api request
        },
        [onRefresh, proxy, resourceId, intl]
    );

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
                            {/** Waiting on a go link from Vineela */}
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
                        onClick={onRefresh}
                    >
                        <Body1>{intl.formatMessage(SreAgentResources.refresh)}</Body1>
                    </ToolbarButton>
                    <ToolbarButton
                        {...restoreFocusTargetAttribute}
                        appearance="transparent"
                        icon={<EditRegular />}
                        className={styles.toolbarButton}
                        disabled={disableButtons}
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
                                    isLoading ? (
                                        <Skeleton className={styles.summaryLoader}>
                                            <SkeletonItem size={32} className={styles.fullWidth} />
                                        </Skeleton>
                                    ) : (
                                        <Body2>
                                            <span className={styles.smallSpaceOnRight}>
                                                {intl.formatMessage(UsageResources.monthlyAAULimitLabel)}
                                            </span>
                                            <Body3Strong>
                                                {intl.formatMessage(UsageResources.billingDescription, { count: totalLimit })}
                                            </Body3Strong>
                                        </Body2>
                                    )
                                }
                            />
                        }
                        className={styles.infoCard}
                    />
                </div>
                <div className={styles.sectionTitle}>
                    {isLoading ? (
                        <Skeleton className={styles.summaryLoader}>
                            <SkeletonItem size={48} />
                        </Skeleton>
                    ) : (
                        <>
                            <Body2Strong block={true}>
                                {new Date().toLocaleString(undefined, { month: 'long', year: 'numeric' })}
                            </Body2Strong>
                            <Caption1>
                                {intl.formatMessage(UsageResources.activeFlowResetMessage, { days: daysLeftOfCurrentMonth })}
                            </Caption1>
                        </>
                    )}
                </div>
                <div className={styles.section}>
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
                        <div className={styles.totalUsageInfoLabel}>
                            <Caption1>{intl.formatMessage(UsageResources.consumptionAAUUsageLabel)}</Caption1>
                            {isLoading ? (
                                <Skeleton className={styles.totalActiveFlowLoader}>
                                    <SkeletonItem size={32} style={{ width: '200px' }} />
                                </Skeleton>
                            ) : (
                                <Title2>
                                    <span style={{ color: tokens.colorPaletteCornflowerBorderActive }}>{getNumberLocale(1000)}</span>
                                    <span>{'/'}</span>
                                    <span>{getNumberLocale(5000)}</span>
                                </Title2>
                            )}
                        </div>
                        <div className={styles.fullWidth}>
                            {isLoading ? (
                                <Skeleton className={styles.progressBarLabelLoader}>
                                    <SkeletonItem style={{ width: '95px' }} />
                                </Skeleton>
                            ) : (
                                <Caption1 className={styles.progressBarField} block={true} align={'end'}>
                                    <Caption1Strong>{getNumberLocale(1000)}</Caption1Strong>
                                    <span>{'/'}</span>
                                    <span className={styles.extraSmallSpaceOnRight}>{getNumberLocale(5000)}</span>
                                    <span>{'AAUs'}</span>
                                </Caption1>
                            )}
                            {isLoading ? (
                                <Skeleton className={styles.fullWidth}>
                                    <SkeletonItem />
                                </Skeleton>
                            ) : (
                                <ProgressBar value={1000} max={5000} className={styles.progressBar}></ProgressBar>
                            )}
                        </div>
                    </EntityCard>
                </div>
                <div className={styles.section}>
                    <EntityCard
                        role="group"
                        entityTitle={<EntityTitle primaryText={intl.formatMessage(UsageResources.dailyActiveFlowConsumptionTitle)} />}
                        style={{ maxWidth: 'unset' }}
                        content={{ style: { maxWidth: 'unset', borderRadius: 'unset', width: '100%', minHeight: '450px' } }}
                    >
                        <div className={styles.fullWidth}>
                            {isLoading ? (
                                <Skeleton className={styles.fullHeight}>
                                    <SkeletonItem className={styles.fullHeight} />
                                </Skeleton>
                            ) : (
                                <VerticalBarChart
                                    culture={typeof window !== 'undefined' ? window.navigator.language : 'en-us'}
                                    data={usageData}
                                    lineLegendText={intl.formatMessage(UsageResources.aauConsumptionLegendText)}
                                    useUTC={false}
                                />
                            )}
                        </div>
                    </EntityCard>
                </div>
            </div>
            {/** Add a key to make sure each time the dialog is opened, it remounts with the initial values */}
            <AllocationChangeDialog
                key={Guid.newGuid()}
                isOpen={isDialogOpen}
                onOpenChange={setIsDialogOpen}
                initialValue={totalLimit}
                currentUsage={currentUsage}
                changeAAUAllocation={onUpdate}
            />
        </CopilotProvider>
    );
};

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
        setErrorMessage(value > 20000 ? intl.formatMessage(UsageResources.usageLimitErrorMessage) : '');
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
                    >
                        <Input
                            type={'number'}
                            min={0}
                            max={20000}
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
                <div className={mergeClasses(styles.section, styles.sliderContainer)}>
                    <Label aria-hidden>{getNumberLocale(0)}</Label>
                    <Slider
                        min={0}
                        max={20000}
                        value={value}
                        className={styles.fullWidth}
                        aria-label={intl.formatMessage(UsageResources.usageLimitSliderAriaLabel)}
                        onChange={(_, data) => setValue(data.value)}
                    />
                    <Label aria-hidden>{getNumberLocale(20000)}</Label>
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
