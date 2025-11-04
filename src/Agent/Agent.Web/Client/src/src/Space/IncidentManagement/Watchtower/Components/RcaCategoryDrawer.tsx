import {
    Body1,
    Button,
    Caption1,
    Drawer,
    DrawerBody,
    DrawerHeader,
    DrawerHeaderTitle,
    Label,
    Link,
    makeStyles,
    mergeClasses,
    tokens,
    Toolbar,
    ToolbarButton,
} from '@fluentui/react-components';
import { Dismiss24Regular } from '@fluentui/react-icons';
import { ConstrainMode, DetailsListLayoutMode, SelectionMode } from '@fluentui/react/lib/DetailsList';
import { ShimmeredDetailsList } from '@fluentui/react/lib/ShimmeredDetailsList';
import { useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { useAzPortalContext } from '../../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { AppInsightsClient } from '../../../../Common/Clients/AppInsightsClient';
import { ThreadClient } from '../../../../Common/Clients/ThreadClient';
import { AiGeneratedBadge } from '../../../../Common/Components/AiGeneratedBadge';
import { ISortedDetailsListColumn } from '../../../../Common/Components/DetailsList/Constants';
import { Thread } from '../../../../Common/Contracts/DataPlane/Thread';
import { getLocalizedMitigatedBy } from '../../../../Common/Helpers/IncidentManagement';
import { useIsDarkMode } from '../../../../Common/Hooks/useIsDarkMode';
import { IncidentManagementResources, SreAgentResources } from '../../../../Strings/SREAgentResources';
import { useIncidentManagementStyles } from '../../../Styles/IncidentManagement.styles';
import IncidentChatDrawer from '../../Common/IncidentChatDrawer';
import { getIncidentsByRootCauseQuery } from '../Queries';

interface RelatedIncident {
    id: string;
    title: string;
    status: string;
    severity: string;
    createdOn: Date;
    mitigatedBy: string;
}

export interface RcaCategoryDrawerProps {
    isOpen: boolean;
    onClose: () => void;
    category: string;
    incidentCount: number;
    rootCauseDescription: string;
    responsePlanName: string;
    timeRange: any;
    appInsightsId: string;
    appInsightsToken: string | null;
}

const useRcaCategoryDrawerStyles = makeStyles({
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
        display: 'flex',
        flexDirection: 'column',
        overflow: 'hidden',
        height: '100%',
    },
    content: {
        display: 'flex',
        flexDirection: 'column',
        gap: '20px',
        flex: 1,
        overflow: 'hidden',
    },
    section: {
        display: 'flex',
        flexDirection: 'column',
        gap: '15px',
    },
    incidentsSection: {
        display: 'flex',
        flexDirection: 'column',
        gap: '15px',
        flex: 1,
        minHeight: 0,
        overflow: 'hidden',
    },
    sectionLabel: {
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground2,
        fontSize: tokens.fontSizeBase400,
    },
    gridStyle: {
        display: 'flex',
        flexDirection: 'column',
    },
    scrollableContainer: {
        overflowY: 'auto',
        flex: 1,
        minHeight: 0,
    },
    detailsListContainer: {
        width: '100%',
        userSelect: 'text',
    },
    closeButtonContainer: {
        paddingTop: '16px',
        display: 'flex',
        justifyContent: 'flex-start',
    },
    summariesBox: {
        display: 'flex',
        flexDirection: 'column',
        gap: '8px',
        padding: '12px',
        borderRadius: tokens.borderRadiusMedium,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        backgroundColor: tokens.colorNeutralBackground1,
        boxShadow: tokens.shadow4,
    },
    incidentCount: {
        fontWeight: tokens.fontWeightSemibold,
        fontSize: tokens.fontSizeBase400,
        color: tokens.colorNeutralForeground2,
    },
});

const RcaCategoryDrawer = ({
    isOpen,
    onClose,
    category,
    incidentCount,
    rootCauseDescription,
    responsePlanName,
    timeRange,
    appInsightsId,
    appInsightsToken,
}: RcaCategoryDrawerProps) => {
    const intl = useIntl();
    const styles = useRcaCategoryDrawerStyles();
    const incidentStyles = useIncidentManagementStyles();
    const isDarkMode = useIsDarkMode();
    const { sreAgentEndpoint, resourceId } = useContext(EnvironmentContext);
    const { log } = useAzPortalContext();

    const [relatedIncidents, setRelatedIncidents] = useState<RelatedIncident[]>([]);
    const [isLoading, setIsLoading] = useState(false);
    const [selectedThread, setSelectedThread] = useState<Thread | undefined>();
    const [isChatDrawerOpen, setIsChatDrawerOpen] = useState(false);

    const handleOpenChatDrawer = useCallback((thread: Thread) => {
        setSelectedThread(thread);
        setIsChatDrawerOpen(true);
    }, []);

    const handleCloseChatDrawer = useCallback(() => {
        setIsChatDrawerOpen(false);
        setSelectedThread(undefined);
    }, []);

    const fetchRelatedIncidents = useCallback(async () => {
        if (!appInsightsToken || !category) return;

        setIsLoading(true);
        const response = await AppInsightsClient.getLogQueryResults(appInsightsId, appInsightsToken, {
            query: getIncidentsByRootCauseQuery(responsePlanName, category, timeRange),
        });

        if (response.isSuccessful) {
            const queryResultRows = response.content?.tables[0]?.rows ?? [];
            const data: RelatedIncident[] = queryResultRows.map(row => ({
                id: row[0] as string,
                title: row[1] as string,
                severity: row[2] as string,
                createdOn: new Date(row[3] ?? Date.now()),
                status: row[4] as string,
                mitigatedBy: row[5] as string,
            }));
            setRelatedIncidents(data);
        } else {
            log({
                action: 'fetchRelatedIncidents',
                actionModifier: 'failed',
                resourceId,
                logLevel: 'error',
                data: {
                    error: response.error?.response?.data?.error,
                },
            });
        }
        setIsLoading(false);
    }, [appInsightsId, appInsightsToken, category, responsePlanName, timeRange, log, resourceId]);

    useEffect(() => {
        if (isOpen) {
            fetchRelatedIncidents();
        }
    }, [isOpen, fetchRelatedIncidents]);

    const handleIncidentTitleClick = useCallback(
        async (incidentId: string) => {
            const threadClient = ThreadClient.getInstance(sreAgentEndpoint);
            const response = await threadClient.getIncidentThreads({
                skip: 0,
                top: 1,
                filter: `incidentId eq '${incidentId}'`,
            });

            if (response.isSuccessful && response.content && response.content.length > 0) {
                handleOpenChatDrawer(response.content[0]);
            } else {
                log({
                    action: 'handleIncidentTitleClick',
                    actionModifier: 'failed',
                    resourceId,
                    logLevel: 'error',
                    data: {
                        incidentId,
                        error: response.error,
                    },
                });
            }
        },
        [sreAgentEndpoint, handleOpenChatDrawer, log, resourceId]
    );

    const onRenderIncidentId = useCallback((item: RelatedIncident) => {
        return <div>{item.id}</div>;
    }, []);

    const onRenderTitle = useCallback(
        (item: RelatedIncident) => {
            return (
                <Link
                    onClick={() => {
                        handleIncidentTitleClick(item.id);
                    }}
                >
                    {item.title}
                </Link>
            );
        },
        [handleIncidentTitleClick]
    );

    const onRenderStatus = useCallback((item: RelatedIncident) => {
        return <Body1>{item.status}</Body1>;
    }, []);

    const onRenderMitigatedBy = useCallback(
        (item: RelatedIncident) => {
            return <Body1>{getLocalizedMitigatedBy(item.mitigatedBy as 'agent' | 'user' | 'inProgress', intl)}</Body1>;
        },
        [intl]
    );

    const columns = useMemo<ISortedDetailsListColumn[]>(() => {
        return [
            {
                key: 'id',
                name: intl.formatMessage(IncidentManagementResources.incidentId),
                isResizable: true,
                minWidth: 125,
                maxWidth: 125,
                onRender: onRenderIncidentId,
            },
            {
                key: 'title',
                name: intl.formatMessage(IncidentManagementResources.incidentTitle),
                isResizable: true,
                minWidth: 120,
                maxWidth: 150,
                onRender: onRenderTitle,
            },
            {
                key: 'status',
                name: intl.formatMessage(IncidentManagementResources.incidentStatus),
                isResizable: true,
                minWidth: 75,
                maxWidth: 100,
                onRender: onRenderStatus,
            },
            {
                key: 'mitigatedBy',
                name: intl.formatMessage(IncidentManagementResources.mitigatedBy),
                isResizable: true,
                minWidth: 80,
                maxWidth: 80,
                onRender: onRenderMitigatedBy,
            },
        ];
    }, [intl, onRenderIncidentId, onRenderTitle, onRenderStatus, onRenderMitigatedBy]);

    return (
        <>
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
                        <div className={styles.titleText}>{category || intl.formatMessage(SreAgentResources.other)}</div>
                    </DrawerHeaderTitle>
                </DrawerHeader>
                <DrawerBody className={styles.body}>
                    <div className={styles.content}>
                        {rootCauseDescription && (
                            <div className={styles.summariesBox}>
                                <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                                    <Caption1 className={styles.sectionLabel}>
                                        {intl.formatMessage(IncidentManagementResources.whatHappened)}
                                    </Caption1>
                                    <AiGeneratedBadge />
                                </div>

                                <Body1>{rootCauseDescription}</Body1>
                            </div>
                        )}

                        <div className={styles.section}>
                            <Caption1 className={styles.sectionLabel}>
                                {intl.formatMessage(IncidentManagementResources.incidentStatus)}
                            </Caption1>
                            <div className={styles.gridStyle}>
                                <Label>{intl.formatMessage(IncidentManagementResources.relatedIncidents)}</Label>
                                <Body1 className={styles.incidentCount}>{incidentCount}</Body1>
                            </div>
                        </div>

                        <div className={styles.incidentsSection}>
                            <Caption1 className={styles.sectionLabel}>
                                {intl.formatMessage(IncidentManagementResources.relatedIncidents)}
                            </Caption1>
                            <div data-is-scrollable="true" user-select="text" className={styles.scrollableContainer}>
                                <ShimmeredDetailsList
                                    columns={columns}
                                    items={relatedIncidents}
                                    constrainMode={ConstrainMode.horizontalConstrained}
                                    layoutMode={DetailsListLayoutMode.justified}
                                    selectionMode={SelectionMode.none}
                                    enableShimmer={isLoading}
                                    className={mergeClasses(
                                        incidentStyles.detailsListBase,
                                        isDarkMode ? incidentStyles.detailsListDarkModeBackground : undefined,
                                        styles.detailsListContainer
                                    )}
                                    compact
                                />
                            </div>
                        </div>

                        <div className={styles.closeButtonContainer}>
                            <Button appearance="secondary" onClick={onClose}>
                                {intl.formatMessage(IncidentManagementResources.close)}
                            </Button>
                        </div>
                    </div>
                </DrawerBody>
            </Drawer>

            <IncidentChatDrawer isOpen={isChatDrawerOpen} onClose={handleCloseChatDrawer} thread={selectedThread} size="large" />
        </>
    );
};

export default RcaCategoryDrawer;
