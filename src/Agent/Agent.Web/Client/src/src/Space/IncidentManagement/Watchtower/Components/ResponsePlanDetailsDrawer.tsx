import {
    Body1,
    Button,
    Caption1,
    Drawer,
    DrawerBody,
    DrawerFooter,
    DrawerHeader,
    DrawerHeaderTitle,
    Label,
    Skeleton,
    SkeletonItem,
    Toolbar,
    ToolbarButton,
    makeStyles,
    tokens,
} from '@fluentui/react-components';
import { Dismiss24Regular } from '@fluentui/react-icons';
import { useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { useAzPortalContext } from '../../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { IncidentHandlerClient } from '../../../../Common/Clients/IncidentHandlerClient';
import { IncidentFilter, IncidentHandler } from '../../../../Common/Contracts/Azure/IncidentHandler';
import { getLocalizedAgentMode } from '../../../../Common/Helpers/AgentMode';
import { IncidentHandlerCreateResources, IncidentManagementResources, SreAgentResources } from '../../../../Strings/SREAgentResources';

export interface ResponsePlanDetailsDrawerProps {
    isOpen: boolean;
    onClose: () => void;
    responsePlan: { responsePlanName: string; autonomyLevel: string };
    onEditHandler: (filter: IncidentFilter | undefined, handlerId: string | undefined) => void;
    canEdit?: boolean;
}

const ResponsePlanDetailsDrawer = ({ isOpen, onClose, responsePlan, onEditHandler, canEdit = true }: ResponsePlanDetailsDrawerProps) => {
    const intl = useIntl();
    const styles = useResponsePlanDetailsDrawerStyles();
    const { resourceId, sreAgentEndpoint } = useContext(EnvironmentContext);
    const { log } = useAzPortalContext();

    const [handlerDetails, setHandlerDetails] = useState<IncidentHandler | undefined>();
    const [filterDetails, setFilterDetails] = useState<IncidentFilter | undefined>();
    const [handlerLoading, setHandlerLoading] = useState(true);
    const [handlerLoadFailed, setHandlerLoadFailed] = useState(false);
    const [filterLoading, setFilterLoading] = useState(true);
    const [filterLoadFailed, setFilterLoadFailed] = useState(false);

    const incidentHandlerClient = useMemo(() => IncidentHandlerClient.getInstance(sreAgentEndpoint, log), [sreAgentEndpoint, log]);

    const fetchHandlerDetails = useCallback(async () => {
        setHandlerLoading(true);
        setHandlerLoadFailed(false);
        setFilterLoading(true);
        setFilterLoadFailed(false);
        setHandlerDetails(undefined);
        setFilterDetails(undefined);

        const [handlersResponse, filterResponse] = await Promise.all([
            incidentHandlerClient.listHandlers(),
            incidentHandlerClient.getIncidentFilter(responsePlan.responsePlanName),
        ]);

        if (handlersResponse.isSuccessful && handlersResponse.content) {
            const handler = handlersResponse.content.find(h => h.incidentFilterId === responsePlan.responsePlanName);
            if (handler) {
                setHandlerDetails(handler);
            }
        } else {
            setHandlerLoadFailed(true);
            log({
                action: 'fetchHandlerDetails',
                actionModifier: 'listHandlersFailed',
                resourceId,
                logLevel: 'error',
                data: {
                    error: handlersResponse.error,
                    filterId: responsePlan.responsePlanName,
                },
            });
        }
        setHandlerLoading(false);

        if (filterResponse.isSuccessful && filterResponse.content) {
            setFilterDetails(filterResponse.content);
        } else {
            setFilterLoadFailed(true);
            log({
                action: 'fetchHandlerDetails',
                actionModifier: 'getFilterFailed',
                resourceId,
                logLevel: 'error',
                data: {
                    error: filterResponse.error,
                    filterId: responsePlan.responsePlanName,
                },
            });
        }
        setFilterLoading(false);
    }, [incidentHandlerClient, responsePlan.responsePlanName, log, resourceId]);

    useEffect(() => {
        if (isOpen) {
            fetchHandlerDetails();
        }
    }, [isOpen, fetchHandlerDetails]);

    const detailRows = useMemo(() => {
        return [
            {
                label: intl.formatMessage(IncidentManagementResources.incidentType),
                value: filterDetails?.incidentType || intl.formatMessage(IncidentManagementResources.allIncidentTypes),
                isLoaded: !filterLoading,
            },
            {
                label: intl.formatMessage(IncidentManagementResources.impactedService),
                value: filterDetails?.impactedService || intl.formatMessage(IncidentManagementResources.allImpactedServices),
                isLoaded: !filterLoading,
            },
            {
                label: intl.formatMessage(IncidentManagementResources.priority),
                value: filterDetails?.priority || intl.formatMessage(IncidentManagementResources.allPriorities),
                isLoaded: !filterLoading,
            },
            {
                label: intl.formatMessage(IncidentManagementResources.titleContains),
                value: filterDetails?.titleContains || '-',
                isLoaded: !filterLoading,
            },
            {
                label: intl.formatMessage(IncidentHandlerCreateResources.customInstructions),
                value: handlerDetails?.customInstructions
                    ? intl.formatMessage(SreAgentResources.yes)
                    : intl.formatMessage(SreAgentResources.no),
                isLoaded: !handlerLoading,
            },
            {
                label: intl.formatMessage(IncidentManagementResources.autonomyLevel),
                value: getLocalizedAgentMode(responsePlan.autonomyLevel, intl),
                isLoaded: true,
            },
        ];
    }, [filterDetails, handlerDetails, responsePlan.autonomyLevel, intl, filterLoading, handlerLoading]);

    return (
        <Drawer
            modalType="non-modal"
            open={isOpen}
            position="end"
            size="medium"
            className={styles.drawerRoot}
            onOpenChange={(_, data) => {
                if (!data.open) onClose();
            }}
        >
            <DrawerHeader className={styles.header}>
                <DrawerHeaderTitle
                    heading={{
                        className: styles.headingContainer,
                    }}
                    action={
                        <Toolbar>
                            <ToolbarButton
                                aria-label={intl.formatMessage(IncidentManagementResources.closePanel)}
                                appearance="transparent"
                                icon={<Dismiss24Regular />}
                                onClick={onClose}
                            />
                        </Toolbar>
                    }
                >
                    <div className={styles.titleText}>{responsePlan.responsePlanName}</div>
                </DrawerHeaderTitle>
            </DrawerHeader>
            <DrawerBody className={styles.body}>
                <div className={styles.content}>
                    <div className={styles.section}>
                        <Caption1 className={styles.sectionLabel}>
                            {intl.formatMessage(IncidentManagementResources.responsePlanDetails)}
                        </Caption1>
                        <div className={styles.gridStyle}>
                            {detailRows.map((row, index) => (
                                <>
                                    <Label key={`label-${index}`}>{row.label}</Label>
                                    {row.isLoaded ? (
                                        <div key={`value-${index}`}>{row.value}</div>
                                    ) : (
                                        <Skeleton key={`value-${index}`}>
                                            <SkeletonItem />
                                        </Skeleton>
                                    )}
                                </>
                            ))}
                        </div>
                    </div>

                    {handlerDetails?.incidentProcessingGuide && handlerDetails.incidentProcessingGuide.length > 0 && (
                        <div className={styles.section}>
                            <Caption1 className={styles.sectionLabel}>
                                {intl.formatMessage(IncidentHandlerCreateResources.customInstructions)}
                            </Caption1>
                            <div className={styles.instructionsBox}>
                                {handlerDetails.incidentProcessingGuide.map((instruction, index) => (
                                    <Body1 key={index}>{instruction}</Body1>
                                ))}
                            </div>
                        </div>
                    )}
                </div>
            </DrawerBody>
            <DrawerFooter className={styles.footer}>
                <div className={styles.footerActions}>
                    <Button
                        appearance="secondary"
                        disabled={handlerLoading || filterLoading || filterLoadFailed || handlerLoadFailed || !canEdit}
                        onClick={() => {
                            if (!handlerLoading && !filterLoading && !filterLoadFailed && !handlerLoadFailed) {
                                onEditHandler(filterDetails, handlerDetails?.id);
                            }
                        }}
                    >
                        {intl.formatMessage(IncidentManagementResources.editIncidentHandler)}
                    </Button>
                    <Button appearance="secondary" onClick={onClose}>
                        {intl.formatMessage(SreAgentResources.close)}
                    </Button>
                </div>
            </DrawerFooter>
        </Drawer>
    );
};

const useResponsePlanDetailsDrawerStyles = makeStyles({
    drawerRoot: {
        marginTop: '50px',
        marginBottom: '8px',
        borderRadius: '12px',
        paddingRight: '15px',
        paddingLeft: '15px',
    },
    header: {
        padding: '16px 16px 7px 16px',
    },
    headingContainer: {
        display: 'flex',
        flexDirection: 'row',
        gap: '8px',
        alignItems: 'center',
        justifyContent: 'start',
        overflow: 'hidden',
    },
    titleText: {
        whiteSpace: 'nowrap',
        textOverflow: 'ellipsis',
        overflow: 'hidden',
        fontSize: tokens.fontSizeBase600,
    },
    body: {
        padding: '0px 16px 16px 16px',
    },
    content: {
        display: 'flex',
        flexDirection: 'column',
        gap: '20px',
    },
    section: {
        display: 'flex',
        flexDirection: 'column',
        gap: '15px',
    },
    sectionLabel: {
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground2,
        fontSize: tokens.fontSizeBase400,
        paddingTop: '10px',
    },
    gridStyle: {
        display: 'grid',
        gridTemplateColumns: 'auto 1fr',
        columnGap: '80px',
        rowGap: '16px',
        alignItems: 'center',
    },
    instructionsBox: {
        display: 'flex',
        flexDirection: 'column',
        gap: '8px',
        padding: '12px',
        backgroundColor: tokens.colorNeutralBackground3,
        borderRadius: tokens.borderRadiusMedium,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        '& code': {
            fontFamily: tokens.fontFamilyMonospace,
            fontSize: tokens.fontSizeBase200,
            padding: '2px 4px',
            backgroundColor: tokens.colorNeutralBackground1,
            borderRadius: tokens.borderRadiusSmall,
        },
    },
    footer: {
        padding: '12px 16px 16px 16px',
    },
    footerActions: {
        display: 'flex',
        gap: '12px',
        justifyContent: 'flex-end',
    },
});

export default ResponsePlanDetailsDrawer;
