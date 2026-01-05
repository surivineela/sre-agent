import { Caption1, tokens } from '@fluentui-copilot/react-copilot';
import {
    Button,
    DrawerBody,
    DrawerHeader,
    DrawerHeaderTitle,
    Link,
    makeStyles,
    MessageBar,
    MessageBarActions,
    MessageBarBody,
    OverlayDrawer,
    useRestoreFocusSource,
    useRestoreFocusTarget,
} from '@fluentui/react-components';
import { Dismiss24Regular, DismissRegular, OpenRegular } from '@fluentui/react-icons';
import { memo, useContext, useEffect, useState } from 'react';
import { useIntl } from 'react-intl';
import { AgentWarningContext } from '../../Space/Contracts/Context';
import { PrimaryNavItemValues, SecondaryNavItemValues } from '../../Space/Contracts/SreAgentSpace';
import { useAgentSiteNavigate } from '../../Space/Hooks/useAgentSiteNavigate';
import { RbacWarningBannerResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { SreAgentFwLinks } from '../Constants/FwLinks';

const useStyles = makeStyles({
    messageBarGroup: {
        padding: `${tokens.spacingHorizontalMNudge} 0px`,
        display: 'flex',
        flexDirection: 'column',
        marginTop: '10px',
        gap: tokens.spacingVerticalXXL,
    },
    buttonGroup: {
        display: 'flex',
        justifyContent: 'end',
        gap: '5px',
    },
});

const WarningBanner = () => {
    const navigate = useAgentSiteNavigate();

    const {
        // Rbac context
        showRbacWarning,
        handleAddAdminClick,
        handleDismissRbacWarning,
        isCheckingRbac,

        // Usage context
        showUsageWarning,
        approachingLimit,
        reachedLimit,
        handleDismissUsageWarning,
        isCheckingUsage,
    } = useContext(AgentWarningContext);

    const [isWarningSidePanelOpen, setIsWarningSidePanelOpen] = useState<boolean>(false);

    useEffect(() => {
        if (!showRbacWarning && !showUsageWarning) {
            setIsWarningSidePanelOpen(false);
        }
    }, [showRbacWarning, showUsageWarning]);

    return (showRbacWarning || showUsageWarning) && !isCheckingRbac && !isCheckingUsage ? (
        <>
            <MessageBar intent={'warning'} shape={'rounded'} style={{ width: '100%', flex: '1 0 auto' }} layout={'multiline'}>
                {showRbacWarning && showUsageWarning ? (
                    <GeneralWarningMessageBarContent
                        showRbacWarning={showRbacWarning}
                        showUsageWarning={showUsageWarning}
                        setIsWarningSidePanelOpen={setIsWarningSidePanelOpen}
                        handleDismissRbacWarning={handleDismissRbacWarning}
                        handleDismissUsageWarning={handleDismissUsageWarning}
                    />
                ) : showRbacWarning ? (
                    <RbacWarningMessageBarContent
                        onClickAssignRole={handleAddAdminClick}
                        onClickDismiss={handleDismissRbacWarning}
                        isInDrawer={false}
                    />
                ) : (
                    <UsageWarningMessageBarContent
                        reachedLimit={reachedLimit}
                        approachingLimit={approachingLimit}
                        onClickAgentConsumptionButton={() =>
                            navigate({
                                primaryNavItemValue: PrimaryNavItemValues.Settings,
                                secondaryNavItemValue: SecondaryNavItemValues.Usage,
                            })
                        }
                        onClickDismiss={handleDismissUsageWarning}
                        isInDrawer={false}
                    />
                )}
            </MessageBar>
            <SidePanel
                isWarningSidePanelOpen={isWarningSidePanelOpen}
                setIsWarningSidePanelOpen={setIsWarningSidePanelOpen}
                showRbacWarning={showRbacWarning}
                showUsageWarning={showUsageWarning}
                handleAddAdminClick={handleAddAdminClick}
                handleDismissRbacWarning={handleDismissRbacWarning}
                handleDismissUsageWarning={handleDismissUsageWarning}
                approachingLimit={approachingLimit}
                reachedLimit={reachedLimit}
            />
        </>
    ) : null;
};

const GeneralWarningMessageBarContent = memo(
    ({
        showRbacWarning,
        showUsageWarning,
        setIsWarningSidePanelOpen,
        handleDismissRbacWarning,
        handleDismissUsageWarning,
    }: {
        showRbacWarning: boolean;
        showUsageWarning: boolean;
        setIsWarningSidePanelOpen: (open: boolean) => void;
        handleDismissRbacWarning: () => void;
        handleDismissUsageWarning: () => void;
    }) => {
        const restoreFocusTargetAttributes = useRestoreFocusTarget();
        const intl = useIntl();

        return (
            <>
                <MessageBarBody>
                    <Caption1>
                        {intl.formatMessage(RbacWarningBannerResources.genericWarningText)}{' '}
                        <Link {...restoreFocusTargetAttributes} onClick={() => setIsWarningSidePanelOpen(true)}>
                            <Caption1>{intl.formatMessage(RbacWarningBannerResources.learnMore)}</Caption1>
                        </Link>
                    </Caption1>
                </MessageBarBody>
                <MessageBarActions
                    containerAction={
                        <Button
                            onClick={() => {
                                if (showRbacWarning) {
                                    handleDismissRbacWarning();
                                }
                                if (showUsageWarning) {
                                    handleDismissUsageWarning();
                                }
                            }}
                            appearance="subtle"
                            icon={<DismissRegular />}
                        >
                            {intl.formatMessage(RbacWarningBannerResources.muteWarnings)}
                        </Button>
                    }
                ></MessageBarActions>
            </>
        );
    }
);

const RbacWarningMessageBarContent = memo(
    ({
        onClickAssignRole,
        onClickDismiss,
        isInDrawer,
    }: {
        onClickAssignRole: () => void;
        onClickDismiss: () => void;
        isInDrawer: boolean;
    }) => {
        const intl = useIntl();

        const buttons = (
            <>
                <Button onClick={onClickAssignRole} appearance="subtle" icon={<OpenRegular />}>
                    {intl.formatMessage(RbacWarningBannerResources.assignRole)}
                </Button>
                <Button onClick={onClickDismiss} appearance="subtle" icon={<DismissRegular />}>
                    {intl.formatMessage(RbacWarningBannerResources.muteThisWarning)}
                </Button>
            </>
        );

        const text = (
            <>
                {`${intl.formatMessage(RbacWarningBannerResources.rbacWarningMessage)} `}
                <Link href={SreAgentFwLinks.sreAgentRbacInfo} target="_blank" rel="noopener noreferrer">
                    {intl.formatMessage(RbacWarningBannerResources.learnMoreAboutRbac)}
                </Link>
            </>
        );

        return (
            <>
                <MessageBarBody>{isInDrawer ? text : <Caption1>{text}</Caption1>}</MessageBarBody>
                <MessageBarActions containerAction={isInDrawer ? undefined : buttons}>{isInDrawer ? buttons : null}</MessageBarActions>
            </>
        );
    }
);

const UsageWarningMessageBarContent = memo(
    ({
        reachedLimit,
        approachingLimit,
        onClickAgentConsumptionButton,
        onClickDismiss,
        isInDrawer,
    }: {
        reachedLimit: boolean;
        approachingLimit: boolean;
        onClickAgentConsumptionButton: () => void;
        onClickDismiss: () => void;
        isInDrawer: boolean;
    }) => {
        const intl = useIntl();

        const buttons = (
            <>
                <Button onClick={onClickAgentConsumptionButton} appearance="subtle" icon={<OpenRegular />}>
                    {intl.formatMessage(RbacWarningBannerResources.goToAgentConsumption)}
                </Button>
                <Button onClick={onClickDismiss} appearance="subtle" icon={<DismissRegular />}>
                    {intl.formatMessage(RbacWarningBannerResources.muteThisWarning)}
                </Button>
            </>
        );

        const text = (
            <>
                {reachedLimit
                    ? intl.formatMessage(RbacWarningBannerResources.usageReachedLimitMessage)
                    : approachingLimit
                      ? intl.formatMessage(RbacWarningBannerResources.usageApproachingLimitMessage)
                      : ''}
            </>
        );

        return (
            <>
                <MessageBarBody>{isInDrawer ? text : <Caption1>{text}</Caption1>}</MessageBarBody>
                <MessageBarActions containerAction={isInDrawer ? undefined : buttons}>{isInDrawer ? buttons : null}</MessageBarActions>
            </>
        );
    }
);

const SidePanel = ({
    handleAddAdminClick,
    handleDismissRbacWarning,
    handleDismissUsageWarning,
    approachingLimit,
    reachedLimit,
    isWarningSidePanelOpen,
    setIsWarningSidePanelOpen,
    showRbacWarning,
    showUsageWarning,
}: {
    isWarningSidePanelOpen: boolean;
    setIsWarningSidePanelOpen: (open: boolean) => void;
    showRbacWarning: boolean;
    showUsageWarning: boolean;
    handleAddAdminClick: () => void;
    handleDismissRbacWarning: () => void;
    handleDismissUsageWarning: () => void;
    approachingLimit: boolean;
    reachedLimit: boolean;
}) => {
    const intl = useIntl();
    const styles = useStyles();
    const navigate = useAgentSiteNavigate();

    const restoreFocusSourceAttributes = useRestoreFocusSource();

    const [showRbac, setShowRbac] = useState<boolean>(showRbacWarning);
    const [showUsage, setShowUsage] = useState<boolean>(showUsageWarning);
    const [isRbacDismissed, setIsRbacDismissed] = useState<boolean>(false);
    const [isUsageDismissed, setIsUsageDismissed] = useState<boolean>(false);

    const onOpenChange = (open: boolean) => {
        setIsWarningSidePanelOpen(open);

        if (!open) {
            if (showRbacWarning && isRbacDismissed) {
                handleDismissRbacWarning();
            }
            if (showUsageWarning && isUsageDismissed) {
                handleDismissUsageWarning();
            }
        }
    };

    useEffect(() => {
        setShowRbac(showRbacWarning);
        setShowUsage(showUsageWarning);
    }, [showRbacWarning, showUsageWarning, isRbacDismissed, isUsageDismissed]);

    return (
        <OverlayDrawer
            as="aside"
            {...restoreFocusSourceAttributes}
            open={isWarningSidePanelOpen}
            onOpenChange={(_, { open }) => {
                onOpenChange(open);
            }}
            size={'medium'}
            position={'end'}
        >
            <DrawerHeader>
                <DrawerHeaderTitle
                    action={
                        <Button
                            appearance="subtle"
                            aria-label={intl.formatMessage(SreAgentResources.close)}
                            icon={<Dismiss24Regular />}
                            onClick={() => onOpenChange(false)}
                        />
                    }
                >
                    {intl.formatMessage(SreAgentResources.warning)}
                </DrawerHeaderTitle>
            </DrawerHeader>

            <DrawerBody>
                <div className={styles.messageBarGroup}>
                    {showRbac && !isRbacDismissed && (
                        <MessageBar key={`rbc`} intent={'warning'} layout={'multiline'} style={{ borderRadius: tokens.borderRadiusXLarge }}>
                            <RbacWarningMessageBarContent
                                onClickAssignRole={() => {
                                    handleAddAdminClick();
                                    onOpenChange(false);
                                }}
                                onClickDismiss={() => {
                                    setIsRbacDismissed(true);
                                }}
                                isInDrawer={true}
                            />
                        </MessageBar>
                    )}
                    {showUsage && !isUsageDismissed && (
                        <MessageBar
                            key={`usage`}
                            intent={'warning'}
                            layout={'multiline'}
                            style={{ borderRadius: tokens.borderRadiusXLarge }}
                        >
                            <UsageWarningMessageBarContent
                                reachedLimit={reachedLimit}
                                approachingLimit={approachingLimit}
                                onClickAgentConsumptionButton={() => {
                                    navigate({
                                        primaryNavItemValue: PrimaryNavItemValues.Settings,
                                        secondaryNavItemValue: SecondaryNavItemValues.Usage,
                                    });
                                    onOpenChange(false);
                                }}
                                onClickDismiss={() => {
                                    setIsUsageDismissed(true);
                                }}
                                isInDrawer={true}
                            />
                        </MessageBar>
                    )}
                </div>
            </DrawerBody>
        </OverlayDrawer>
    );
};

export default memo(WarningBanner);
