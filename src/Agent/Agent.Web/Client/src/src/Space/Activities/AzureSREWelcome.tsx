import { CopilotMessageV2 } from '@fluentui-copilot/react-copilot';
import { ShimmeredDetailsList } from '@fluentui/react';
import { Button, Card, Image, Link, mergeClasses, Text, tokens } from '@fluentui/react-components';
import { CheckboxVisibility, ConstrainMode, DetailsListLayoutMode, IColumn } from '@fluentui/react/lib/DetailsList';
import axios from 'axios';
import { useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { useLocation, useNavigate } from 'react-router';
import AzPortalProxy from '../../Common/AzPortalProxy/AzPortalProxy';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { LearnMoreLink } from '../../Common/Components/LearnMoreLink';
import { Pagination } from '../../Common/Components/Pagination';
import { SreAgentFwLinks } from '../../Common/Constants/FwLinks';
import { Thread } from '../../Common/Contracts/Azure/SreAgent';
import { getAgentHeaders } from '../../Common/Helpers/headers';
import { getUserFriendlyLocation } from '../../Common/Helpers/LocationHelper';
import { getResourceTypeFriendlyName, resolveResourceIcon } from '../../Common/Helpers/Resources';
import { SreAgentResources, WelcomeResources } from '../../Strings/SREAgentResources';
import { getSubscriptionId, useResourceGroups } from '../Settings/Hooks/useResourceGroups';
import { useSreAgent } from '../Settings/Hooks/useSreAgent';
import { useSubscriptions } from '../Settings/Hooks/useSubscriptions';
import { ChatBoxStyles } from '../Styles/Activities.styles';
import {
    KnowledgeGraphStatus,
    LogicalAppGridItem,
    LogicalAppGridKey,
    LogicalApplication,
    ResourceGroupGridItem,
    ResourceGroupGridKey,
    ResourceHealth,
    suggestedWelcomePrompts,
    WelcomeMessageResponse,
} from './AzureSREWelcome.Constants';
import { useWelcomeStyles } from './AzureSREWelcome.styles';

const ResourceGroupsCard = () => {
    const { resourceId } = useContext(EnvironmentContext);
    const portalContext = useContext(AzPortalContext);

    const styles = useWelcomeStyles();
    const intl = useIntl();

    const [currentPage, setCurrentPage] = useState(1);
    const itemsPerPage = 5;

    const [sortColumn, setSortColumn] = useState<string | null>(null);
    const [sortDirection, setSortDirection] = useState<'asc' | 'desc'>('asc');

    const { agent } = useSreAgent(resourceId);
    const subscriptionIds = useMemo(() => {
        const subIds = agent?.properties.knowledgeGraphConfiguration?.managedResources?.map(rg => getSubscriptionId(rg)) ?? [];
        return Array.from(new Set(subIds));
    }, [agent]);
    const { subscriptionsList } = useSubscriptions();
    const { resourceGroupsList, resourceGroupsLoading } = useResourceGroups(subscriptionIds, portalContext);

    const rscGrpGridItems = useMemo<ResourceGroupGridItem[]>(() => {
        const managedResourceIds = agent?.properties.knowledgeGraphConfiguration?.managedResources ?? [];

        return (
            resourceGroupsList
                ?.filter(rscGrp => managedResourceIds.includes(rscGrp.id))
                ?.map(rscGrp => ({
                    name: rscGrp.name,
                    subscription: getSubscriptionId(rscGrp.id),
                    region: getUserFriendlyLocation(rscGrp.location),
                })) ?? []
        );
    }, [resourceGroupsList, agent]);

    const sortedItems = useMemo(() => {
        if (!sortColumn) return rscGrpGridItems;

        const sorted = [...rscGrpGridItems].sort((a, b) => {
            let aValue: string;
            let bValue: string;

            switch (sortColumn) {
                case ResourceGroupGridKey.ResourceGroup: {
                    aValue = a.name.toLowerCase();
                    bValue = b.name.toLowerCase();
                    break;
                }
                case ResourceGroupGridKey.Subscription: {
                    const aSubscription = subscriptionsList?.find(sub => sub.subscriptionId === a.subscription);
                    const bSubscription = subscriptionsList?.find(sub => sub.subscriptionId === b.subscription);
                    aValue = (aSubscription?.displayName || '').toLowerCase();
                    bValue = (bSubscription?.displayName || '').toLowerCase();
                    break;
                }
                case ResourceGroupGridKey.Region: {
                    aValue = a.region.toLowerCase();
                    bValue = b.region.toLowerCase();
                    break;
                }
                default:
                    return 0;
            }

            if (aValue < bValue) return sortDirection === 'asc' ? -1 : 1;
            if (aValue > bValue) return sortDirection === 'asc' ? 1 : -1;
            return 0;
        });

        return sorted;
    }, [rscGrpGridItems, sortColumn, sortDirection, subscriptionsList]);

    const handleColumnClick = useCallback(
        (columnKey: string) => {
            if (sortColumn === columnKey) {
                setSortDirection(sortDirection === 'asc' ? 'desc' : 'asc');
            } else {
                setSortColumn(columnKey);
                setSortDirection('asc');
            }
            setCurrentPage(1);
        },
        [sortColumn, sortDirection]
    );

    useEffect(() => {
        setCurrentPage(1);
    }, [sortedItems]);

    const totalPages = Math.ceil(sortedItems.length / itemsPerPage);
    const startIndex = (currentPage - 1) * itemsPerPage;
    const endIndex = startIndex + itemsPerPage;
    const currentItems = sortedItems.slice(startIndex, endIndex);

    const onRenderResourceGroup = useCallback((item: ResourceGroupGridItem) => {
        return (
            <div style={{ display: 'flex', alignItems: 'center' }}>
                <img src="ResourceGroup.svg" alt="Resource Group" style={{ height: 16, width: 16 }} />
                <span style={{ marginLeft: 4 }}>{item.name}</span>
            </div>
        );
    }, []);

    const onRenderSubscription = useCallback(
        (item: ResourceGroupGridItem) => {
            const subscription = subscriptionsList?.find(sub => sub.subscriptionId === item.subscription);

            return <span>{subscription ? subscription.displayName : item.subscription}</span>;
        },
        [subscriptionsList]
    );

    const rscGrpGridColumns = useMemo<IColumn[]>(() => {
        return [
            {
                key: ResourceGroupGridKey.ResourceGroup,
                name: intl.formatMessage(SreAgentResources.resourceGroupName),
                minWidth: 300,
                maxWidth: 300,
                isResizable: true,
                isSorted: sortColumn === ResourceGroupGridKey.ResourceGroup,
                isSortedDescending: sortColumn === ResourceGroupGridKey.ResourceGroup && sortDirection === 'desc',
                onRender: onRenderResourceGroup,
                onColumnClick: () => handleColumnClick(ResourceGroupGridKey.ResourceGroup),
            },
            {
                key: ResourceGroupGridKey.Subscription,
                name: intl.formatMessage(SreAgentResources.subscription),
                minWidth: 300,
                maxWidth: 300,
                isResizable: true,
                isSorted: sortColumn === ResourceGroupGridKey.Subscription,
                isSortedDescending: sortColumn === ResourceGroupGridKey.Subscription && sortDirection === 'desc',
                onRender: onRenderSubscription,
                onColumnClick: () => handleColumnClick(ResourceGroupGridKey.Subscription),
            },
            {
                key: ResourceGroupGridKey.Region,
                name: intl.formatMessage(SreAgentResources.region),
                fieldName: 'region',
                minWidth: 85,
                maxWidth: 250,
                isResizable: true,
                isSorted: sortColumn === ResourceGroupGridKey.Region,
                isSortedDescending: sortColumn === ResourceGroupGridKey.Region && sortDirection === 'desc',
                onColumnClick: () => handleColumnClick(ResourceGroupGridKey.Region),
            },
        ];
    }, [onRenderResourceGroup, onRenderSubscription, sortColumn, sortDirection, handleColumnClick, intl]);

    return (
        <Card className={styles.sectionCard} style={{ backgroundColor: tokens.colorNeutralBackground1 }}>
            <div className={styles.sectionHeader}>
                <Text weight="semibold" size={500}>
                    {intl.formatMessage(SreAgentResources.resourceGroups)}
                </Text>
            </div>

            <div className={styles.sectionContent}>
                <div>
                    <ShimmeredDetailsList
                        columns={rscGrpGridColumns}
                        items={currentItems}
                        constrainMode={ConstrainMode.horizontalConstrained}
                        layoutMode={DetailsListLayoutMode.justified}
                        enableShimmer={resourceGroupsLoading}
                        checkboxVisibility={CheckboxVisibility.hidden}
                        compact
                    />
                    {totalPages > 1 && <Pagination currentPage={currentPage} totalPages={totalPages} onPageChange={setCurrentPage} />}
                </div>
            </div>
        </Card>
    );
};

interface FakeAgentMessageProps {
    content: React.ReactNode;
}

const FakeAgentMessage = ({ content }: FakeAgentMessageProps) => {
    const intl = useIntl();

    return (
        <CopilotMessageV2
            avatar={<Image src="./SreAgent.svg" width={28} height={28} alt={intl.formatMessage(SreAgentResources.sreAgent)} />}
            name={intl.formatMessage(SreAgentResources.sreAgent)}
            mode="canvas"
            disclaimer={null}
            style={{ font: 'Segoe UI', lineHeight: '20px', wordBreak: 'unset', maxWidth: '90%', whiteSpace: 'pre-line' }}
            className={mergeClasses(ChatBoxStyles.agentMessage)}
        >
            {content}
        </CopilotMessageV2>
    );
};

interface AzureSREWelcomeProps {
    threadId?: string | null;
    addThread: (threadId: string, newThreadToSelect?: Thread) => void;
}

const AzureSREWelcome = ({ threadId, addThread }: AzureSREWelcomeProps) => {
    const { sreAgentEndpoint } = useContext(EnvironmentContext);

    const styles = useWelcomeStyles();
    const intl = useIntl();
    const location = useLocation();
    const navigate = useNavigate();

    const [knowledgeGraphStatus, setKnowledgeGraphStatus] = useState<KnowledgeGraphStatus | null>(null);
    const [logicalApps, setLogicalApps] = useState<LogicalApplication[]>([]);

    const [analysisCurrentPage, setAnalysisCurrentPage] = useState(1);
    const analysisItemsPerPage = 5;

    const logicalAppGridItems = useMemo<LogicalAppGridItem[]>(() => {
        return logicalApps.map(logicalApp => ({
            rscName: logicalApp.name ?? '-',
            rscType: logicalApp.properties?.type ?? '-',
            rscSubType: logicalApp.subType,
        }));
    }, [logicalApps]);

    useEffect(() => {
        setAnalysisCurrentPage(1);
    }, [logicalAppGridItems]);

    const analysisTotalPages = Math.ceil(logicalAppGridItems.length / analysisItemsPerPage);
    const analysisStartIndex = (analysisCurrentPage - 1) * analysisItemsPerPage;
    const analysisEndIndex = analysisStartIndex + analysisItemsPerPage;
    const analysisCurrentItems = logicalAppGridItems.slice(analysisStartIndex, analysisEndIndex);

    const onRenderLogicalAppGroup = useCallback((item: LogicalAppGridItem) => {
        return (
            <div style={{ display: 'flex', alignItems: 'center' }}>
                <img src={resolveResourceIcon(item.rscType)} alt={item.rscType} style={{ height: 16, width: 16 }} />
                <span style={{ marginLeft: 4 }}>{item.rscName}</span>
            </div>
        );
    }, []);

    const onRenderPrimaryResourceType = useCallback((item: LogicalAppGridItem) => {
        return <span>{getResourceTypeFriendlyName(item.rscType, item.rscSubType)}</span>;
    }, []);

    const onRenderResourceMap = useCallback(
        (_item: LogicalAppGridItem) => {
            return (
                <Link onClick={() => navigate({ ...location, pathname: '/views/resourcegraph' })}>
                    {intl.formatMessage(SreAgentResources.goToMap)}
                </Link>
            );
        },
        [intl, location, navigate]
    );

    const logicalAppGridColumns = useMemo<IColumn[]>(() => {
        return [
            {
                key: LogicalAppGridKey.CoreApplicationGroup,
                name: intl.formatMessage(SreAgentResources.coreApplicationGroup),
                minWidth: 300,
                maxWidth: 300,
                isResizable: true,
                onRender: onRenderLogicalAppGroup,
            },
            {
                key: LogicalAppGridKey.PrimaryResourceType,
                name: intl.formatMessage(WelcomeResources.primaryResourceType),
                minWidth: 300,
                maxWidth: 300,
                isResizable: true,
                onRender: onRenderPrimaryResourceType,
            },
            {
                key: LogicalAppGridKey.ResourceMap,
                name: intl.formatMessage(SreAgentResources.resourceMap),
                minWidth: 85,
                maxWidth: 250,
                isResizable: true,
                onRender: onRenderResourceMap,
            },
        ];
    }, [intl, onRenderLogicalAppGroup, onRenderPrimaryResourceType, onRenderResourceMap]);

    const createNewThreadWithPrompt = useCallback(
        async (prompt: string) => {
            const url = `${sreAgentEndpoint}/api/v1/threads`;

            const response = await axios.post(
                url,
                {
                    startMessage: {
                        text: prompt,
                        userId: 'web-client-user',
                        displayName: 'Web Client User',
                    },
                },
                {
                    headers: getAgentHeaders(),
                }
            );
            const thread = response?.data;

            if (thread) {
                addThread(thread.id, thread);
            }
        },
        [sreAgentEndpoint, addThread]
    );

    useEffect(() => {
        const fetchWelcomeMessage = async () => {
            try {
                // Only attempt to fetch if threadId exists
                if (!threadId) {
                    console.log('No threadId available, skipping fetch');
                    return;
                }

                const res = await fetch(`${sreAgentEndpoint}/api/v1/threads/${threadId}/welcomeMessage`, {
                    headers: getAgentHeaders(),
                });
                if (!res.ok) throw new Error(await res.text());
                const data: WelcomeMessageResponse = await res.json();

                // Process the data
                if (data) {
                    // Update knowledgeGraphStatus
                    if (data.knowledgeGraphStatus) {
                        setKnowledgeGraphStatus(data.knowledgeGraphStatus);
                    }

                    // data.integrations

                    if (data.logicalApplications) {
                        const enhancedApps = data.logicalApplications.map((app, _) => {
                            // Extract application name from resourceId
                            const resourceParts = app.resourceId.split('/');
                            const name = app.name;

                            // Extract resource type from resourceId
                            const resourceType = resourceParts[resourceParts.length - 2];

                            // Return enhanced app with derived information
                            return {
                                ...app,
                                name: name,
                                properties: {
                                    type: resourceType,
                                    subType: app.subType,
                                    health: 'Unknown' as ResourceHealth,
                                },
                            };
                        });

                        setLogicalApps(enhancedApps);
                    }
                }
            } catch (err) {
                console.error('Failed to fetch welcome message', err);
            }
        };

        fetchWelcomeMessage();
        const intervalId = setInterval(() => fetchWelcomeMessage(), 10000);

        return () => clearInterval(intervalId);
    }, [sreAgentEndpoint, threadId]);

    // Calculate resource counts from knowledgeGraphStatus
    const resourceCounts = useMemo(() => {
        if (!knowledgeGraphStatus) {
            return { total: 0, webApps: 0, databaseResources: 0 };
        }

        const total = knowledgeGraphStatus.crawlProgress.totalResources || 0;

        // NOTE: Currently Function Apps are reported as web apps ("microsoft.web/sites"; subType too)
        let webApps = 0;
        let containerApps = 0;
        let azureKubernetesServices = 0;
        let databases = 0;

        const resourceTypes = knowledgeGraphStatus.crawlProgressByResourceType || {};
        Object.keys(resourceTypes).forEach(type => {
            const lowerType = type.toLowerCase();

            if (lowerType === 'microsoft.web/sites') {
                webApps += resourceTypes[type].totalResources;
            } else if (
                lowerType.includes('sql') ||
                lowerType.includes('cosmos') ||
                lowerType.includes('redis') ||
                lowerType.includes('database')
            ) {
                databases += resourceTypes[type].totalResources;
            } else if (lowerType === 'microsoft.app/containerapps') {
                containerApps += resourceTypes[type].totalResources;
            } else if (lowerType.includes('containerservice') || lowerType.includes('k8s')) {
                azureKubernetesServices += resourceTypes[type].totalResources;
            }
        });

        return { total, webApps, containerApps, azureKubernetesServices, databases };
    }, [knowledgeGraphStatus]);

    return (
        <div className={styles.container}>
            <FakeAgentMessage
                content={
                    <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
                        <div>{intl.formatMessage(WelcomeResources.initialWelcomeMessagePt1)}</div>
                        <div>{intl.formatMessage(WelcomeResources.initialWelcomeMessagePt2)}</div>
                    </div>
                }
            />

            {knowledgeGraphStatus?.status === 'Completed' && (
                <>
                    <FakeAgentMessage content={intl.formatMessage(WelcomeResources.finishedAnalyzingResources)} />

                    {!AzPortalProxy.inStandaloneMode && <ResourceGroupsCard />}

                    <Card className={styles.sectionCard} style={{ backgroundColor: tokens.colorNeutralBackground1 }}>
                        <div className={styles.sectionHeader}>
                            <Text weight="semibold" size={500}>
                                {intl.formatMessage(WelcomeResources.resourceAnalysis)}
                            </Text>
                        </div>

                        <div className={styles.sectionContent}>
                            <div className={styles.statsGrid}>
                                <div className={styles.statCard}>
                                    <Text className={styles.statLabel}>{intl.formatMessage(SreAgentResources.totalResources)}</Text>
                                    <Text className={styles.statValue}>{resourceCounts.total}</Text>
                                </div>
                                <div className={styles.statCard}>
                                    <Text className={styles.statLabel}>Web Apps</Text>
                                    <Text className={styles.statValue}>{resourceCounts.webApps}</Text>
                                </div>
                                <div className={styles.statCard}>
                                    <Text className={styles.statLabel}>Container Apps</Text>
                                    <Text className={styles.statValue}>{resourceCounts.containerApps}</Text>
                                </div>
                                <div className={styles.statCard}>
                                    <Text className={styles.statLabel}>Azure Kubernetes Services</Text>
                                    <Text className={styles.statValue}>{resourceCounts.azureKubernetesServices}</Text>
                                </div>
                                <div className={styles.statCard}>
                                    <Text className={styles.statLabel}>Databases</Text>
                                    <Text className={styles.statValue}>{resourceCounts.databases}</Text>
                                </div>
                            </div>
                            <div>
                                <ShimmeredDetailsList
                                    columns={logicalAppGridColumns}
                                    items={analysisCurrentItems}
                                    constrainMode={ConstrainMode.horizontalConstrained}
                                    layoutMode={DetailsListLayoutMode.justified}
                                    enableShimmer={false}
                                    checkboxVisibility={CheckboxVisibility.hidden}
                                    compact
                                />
                                {analysisTotalPages > 1 && (
                                    <Pagination
                                        currentPage={analysisCurrentPage}
                                        totalPages={analysisTotalPages}
                                        onPageChange={setAnalysisCurrentPage}
                                    />
                                )}
                            </div>
                        </div>
                    </Card>

                    <Card className={styles.sectionCard} style={{ backgroundColor: tokens.colorNeutralBackground1 }}>
                        <div className={styles.sectionHeader}>
                            <Text weight="semibold" size={500}>
                                {intl.formatMessage(WelcomeResources.suggestedPromptsForYourResources)}
                            </Text>
                        </div>

                        <div className={styles.sectionContent}>
                            <div style={{ display: 'flex', flexDirection: 'column', gap: '18px' }}>
                                {suggestedWelcomePrompts.map((prompt, index) => (
                                    <div
                                        key={index}
                                        style={{
                                            display: 'flex',
                                            flexDirection: 'row',
                                            justifyContent: 'space-between',
                                            alignContent: 'center',
                                            padding: '9px 0px',
                                            borderBottom: `1px solid ${tokens.colorNeutralStroke1}`,
                                        }}
                                    >
                                        <Text style={{ color: tokens.colorNeutralForeground2 }}>{prompt}</Text>
                                        <Button onClick={() => createNewThreadWithPrompt(prompt)}>
                                            {intl.formatMessage(SreAgentResources.startChat)}
                                        </Button>
                                    </div>
                                ))}
                            </div>

                            <div style={{ marginTop: 18, display: 'flex', flexDirection: 'row', justifyContent: 'start' }}>
                                <LearnMoreLink
                                    linkText={intl.formatMessage(WelcomeResources.learnMoreAboutPrompts)}
                                    url={SreAgentFwLinks.learnMoreAboutPrompts}
                                    dontShowIcon
                                />
                            </div>
                        </div>
                    </Card>
                </>
            )}
        </div>
    );
};

export default AzureSREWelcome;
