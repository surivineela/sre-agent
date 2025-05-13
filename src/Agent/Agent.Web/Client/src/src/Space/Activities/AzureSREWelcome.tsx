import {
    Body1,
    Button,
    Card,
    CardHeader,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    Field,
    Input,
    Link,
    ProgressBar,
    Spinner,
    Subtitle1,
    Text,
    Title2,
    Toast,
    ToastTitle,
    makeStyles,
    shorthands,
    tokens,
    useId,
    useToastController,
} from '@fluentui/react-components';
import {
    Alert24Regular,
    AppGeneric24Regular,
    ArrowRight16Regular,
    ArrowSync24Regular,
    CheckmarkCircle24Regular,
    ChevronDown20Regular,
    ChevronRight20Regular,
    Dismiss24Regular,
    DocumentData24Regular,
    GridDots24Regular,
    Link16Regular,
    Screenshot24Regular,
} from '@fluentui/react-icons';
import { Collapse } from '@fluentui/react-motion-components-preview';
import { useEffect, useRef, useState } from 'react';
import { FaGithub } from 'react-icons/fa';
import { getAgentHeaders } from '../../Common/Helpers/headers';

/* ────────────────────────────────  STYLES  ──────────────────────────────── */

const useStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXL,
        fontFamily: tokens.fontFamilyBase,
    },
    headerCard: {
        marginBottom: tokens.spacingVerticalM,
        backgroundColor: tokens.colorNeutralBackground2,
        ...shorthands.borderRadius('15px'), // Updated to match graph styles
        ...shorthands.padding(tokens.spacingVerticalL),
        border: `1px solid ${tokens.colorNeutralStroke2}`, // Added border to match graph styles
    },
    sectionCard: {
        marginBottom: tokens.spacingVerticalL,
        backgroundColor: tokens.colorNeutralBackground2, // Matches graph card style
        ...shorthands.borderRadius('15px'), // Updated to match graph styles
        ...shorthands.padding(tokens.spacingVerticalS),
        border: `1px solid ${tokens.colorNeutralStroke2}`, // Added border to match graph styles
    },
    section: {
        display: 'flex',
        flexDirection: 'column',
    },
    sectionHeader: {
        display: 'flex',
        alignItems: 'center',
        marginBottom: tokens.spacingVerticalM,
        padding: tokens.spacingVerticalL,
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
    },
    collapsibleHeader: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        cursor: 'pointer',
        width: '100%', // Ensure full width
    },
    sectionContent: {
        padding: tokens.spacingVerticalL,
    },
    sectionHeaderIcon: {
        marginRight: tokens.spacingHorizontalS,
        color: tokens.colorBrandForeground1,
        fontSize: '24px',
    },
    welcomeMessage: {
        marginBottom: tokens.spacingVerticalL,
        padding: tokens.spacingHorizontalL,
        fontSize: tokens.fontSizeBase400,
        lineHeight: tokens.lineHeightBase400,
        color: tokens.colorNeutralForeground1,
    },
    featureGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(3, 1fr)',
        gap: tokens.spacingHorizontalM,
        marginTop: tokens.spacingVerticalL,
    },
    featureItem: {
        ...shorthands.padding(tokens.spacingVerticalM, tokens.spacingHorizontalL),
        backgroundColor: tokens.colorNeutralBackground3,
        borderRadius: tokens.borderRadiusMedium,
        display: 'flex',
        justifyContent: 'center',
        alignItems: 'center',
        ...shorthands.border('1px', 'solid', tokens.colorNeutralStroke2),
        cursor: 'pointer',
        transition: 'all 0.2s ease',
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground3Hover,
            transform: 'translateY(-2px)',
        },
    },
    featureIcon: {
        marginRight: tokens.spacingHorizontalS,
        color: tokens.colorBrandForeground1,
    },
    featureDetails: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
        marginTop: tokens.spacingVerticalS,
    },
    featureDetailItem: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
    },
    checkIcon: {
        color: tokens.colorPaletteGreenForeground1,
        marginRight: tokens.spacingHorizontalXS,
    },
    statusCompleteIcon: {
        color: tokens.colorPaletteGreenForeground1,
        marginLeft: tokens.spacingHorizontalS,
    },
    featureContent: {
        marginTop: tokens.spacingVerticalS,
        padding: tokens.spacingVerticalS,
        backgroundColor: tokens.colorNeutralBackground1,
        borderRadius: tokens.borderRadiusMedium,
        ...shorthands.border('1px', 'solid', tokens.colorNeutralStroke1),
    },
    progressContainer: {
        marginBottom: tokens.spacingVerticalM,
    },
    progressHeader: {
        display: 'flex',
        justifyContent: 'space-between',
        marginBottom: tokens.spacingVerticalXS,
    },
    statsGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(3, 1fr)',
        gap: tokens.spacingHorizontalM,
        marginBottom: tokens.spacingVerticalM,
    },
    statCard: {
        backgroundColor: tokens.colorNeutralBackground1,
        ...shorthands.padding(tokens.spacingVerticalL),
        borderRadius: tokens.borderRadiusMedium,
        ...shorthands.border('1px', 'solid', tokens.colorNeutralStroke2),
    },
    statValue: {
        fontSize: '24px',
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorBrandForeground1,
    },
    statLabel: {
        color: tokens.colorNeutralForeground2,
        fontSize: tokens.fontSizeBase300,
        fontWeight: tokens.fontWeightSemibold,
    },
    integrationCard: {
        backgroundColor: tokens.colorNeutralBackground1,
        borderRadius: tokens.borderRadiusMedium,
        ...shorthands.padding(tokens.spacingVerticalM),
        position: 'relative',
        ...shorthands.border('1px', 'solid', tokens.colorNeutralStroke2),
        marginBottom: tokens.spacingVerticalM,
    },
    integrationHeader: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        marginBottom: tokens.spacingVerticalS,
    },
    integrationDetails: {
        marginTop: tokens.spacingVerticalXS,
        marginBottom: tokens.spacingVerticalM,
    },
    integrationAction: {
        marginTop: tokens.spacingVerticalS,
    },
    activeBadge: {
        backgroundColor: tokens.colorPaletteGreenBackground1,
        color: tokens.colorPaletteGreenForeground1,
        ...shorthands.padding('2px', '8px'),
        borderRadius: '12px',
        fontSize: tokens.fontSizeBase100,
        fontWeight: tokens.fontWeightSemibold,
    },
    inactiveBadge: {
        backgroundColor: tokens.colorNeutralBackground4,
        color: tokens.colorNeutralForeground3,
        ...shorthands.padding('2px', '8px'),
        borderRadius: '12px',
        fontSize: tokens.fontSizeBase100,
        fontWeight: tokens.fontWeightSemibold,
    },
    applicationSection: {
        marginTop: tokens.spacingVerticalM,
    },
    applicationList: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalM,
    },
    applicationCard: {
        backgroundColor: tokens.colorNeutralBackground1,
        borderRadius: tokens.borderRadiusMedium,
        ...shorthands.padding(tokens.spacingVerticalM),
        ...shorthands.border('1px', 'solid', tokens.colorNeutralStroke2),
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
    },
    applicationCardLeft: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalM,
    },
    applicationIcon: {
        width: '40px',
        height: '40px',
        borderRadius: '4px',
        display: 'flex',
        justifyContent: 'center',
        alignItems: 'center',
        color: tokens.colorNeutralForeground1,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        backgroundColor: tokens.colorNeutralBackground1,
    },
    applicationInfo: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
    },
    applicationName: {
        fontWeight: tokens.fontWeightSemibold,
        fontSize: tokens.fontSizeBase400,
        textOverflow: 'ellipsis', // Added to match graph styles
        overflow: 'hidden', // Added to match graph styles
        maxWidth: '180px', // Added to prevent long names from breaking layout
    },
    applicationSubtext: {
        color: tokens.colorNeutralForeground2,
        fontSize: tokens.fontSizeBase300,
    },
    resourceLearningMessage: {
        marginBottom: tokens.spacingVerticalM,
        padding: tokens.spacingHorizontalM,
        backgroundColor: tokens.colorNeutralBackground1,
        borderRadius: tokens.borderRadiusMedium,
        border: `1px solid ${tokens.colorNeutralStroke1}`,
    },
    linkStatus: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
        marginTop: tokens.spacingVerticalXS,
    },
    statusDot: {
        width: '8px',
        height: '8px',
        borderRadius: '50%',
    },
    healthTag: {
        display: 'flex',
        alignItems: 'center',
        backgroundColor: tokens.colorNeutralBackground1,
        padding: '4px 8px',
        borderRadius: '12px',
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        fontSize: tokens.fontSizeBase200,
    },
    reposRemainingTag: {
        backgroundColor: tokens.colorNeutralBackground4,
        color: tokens.colorNeutralForeground2,
        ...shorthands.padding('2px', '10px'),
        borderRadius: '12px',
        fontSize: tokens.fontSizeBase200,
        fontWeight: tokens.fontWeightSemibold,
        marginLeft: tokens.spacingHorizontalS,
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
    },
    healthDot: {
        width: '8px',
        height: '8px',
        borderRadius: '50%',
        marginRight: '4px',
    },
    dialogHeader: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        marginBottom: tokens.spacingVerticalM,
    },
    dialogTitle: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
    },
    dialogTitleIcon: {
        color: tokens.colorBrandForeground1,
    },
    dialogCloseButton: {
        backgroundColor: 'transparent',
        border: 'none',
        cursor: 'pointer',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        color: tokens.colorNeutralForeground3,
        ':hover': {
            color: tokens.colorNeutralForeground2,
        },
    },
    reAuthRequired: {
        backgroundColor: tokens.colorPaletteYellowBackground1,
        color: tokens.colorPaletteYellowForeground1,
        ...shorthands.padding('2px', '8px'),
        borderRadius: '12px',
        fontSize: tokens.fontSizeBase100,
        fontWeight: tokens.fontWeightSemibold,
        marginLeft: tokens.spacingHorizontalS,
    },
});

/* ────────────────────────────────  TYPES  ──────────────────────────────── */

// Source-code linking
export type SourceCodeLinkStatus = 'Linked' | 'RequiresAuth' | 'NotLinked' | 'RequiresReAuth';
export type ResourceHealth = 'Healthy' | 'Warning' | 'Critical' | 'Unknown';

export interface SourceCodeLinkageStatus {
    status: SourceCodeLinkStatus;
    repositoryUrl?: string | null;
    linkedTimestamp?: string | null;
    loginCallbackUrl?: string | null;
}

export interface LogicalApplication {
    resourceId: string;
    name?: string;
    subType: string;
    properties?: {
        type?: string;
        health?: ResourceHealth;
    };
    sourceCodeLinkageStatus: SourceCodeLinkageStatus;
}

export interface IntegrationStatus {
    name: string;
    isActive: boolean;
    details: string;
}

export interface KnowledgeGraphStatus {
    status: string;
    crawlProgress: {
        crawled: number;
        totalResources: number;
        finishedInitialCrawl: boolean;
    };
    crawlProgressByResourceType: Record<
        string,
        {
            crawled: number;
            totalResources: number;
        }
    >;
}

export interface WelcomeMessageResponse {
    knowledgeGraphStatus?: KnowledgeGraphStatus;
    logicalApplications?: LogicalApplication[];
    integrations?: IntegrationStatus[];
}

/* ────────────────────────────────  ICON HELPERS  ──────────────────────────────── */
// Icon resolution helpers (copied from GraphCard.tsx)
const ICON_BASE = ''; // eg: assets
const ICON_LOOKUP: Record<string, string> = {
    // Compute / containers
    containerapp: 'ContainerApp.svg',
    containerappjob: 'ContainerAppJob.svg',
    managedenvironment: 'ManagedEnvironment.svg',

    // Kubernetes / orchestrators
    aks: 'AKS.svg',
    managedcluster: 'AKS.svg',
    kubernetes: 'AKS.svg',
    scaleset: 'ScaleSet.svg',

    // Web & Functions
    webapp: 'WebApp.svg',
    functionapp: 'WebApp.svg',
    site: 'WebApp.svg',

    // Databases & caches
    cosmos: 'CosmosDB.svg',
    cosmosdb: 'CosmosDB.svg',
    sql: 'SQLServer.svg',
    sqlserver: 'SQLServer.svg',
    redis: 'AzureRedisCache.svg',
    cache: 'AzureRedisCache.svg',

    // Networking
    vnet: 'Vnet.svg',
    virtualnetwork: 'Vnet.svg',
    subnet: 'Vnet.svg',
    nsg: 'NSG.svg',
    networksecuritygroup: 'NSG.svg',
};

const DEFAULT_ICON = 'azureResource.svg';

const resolveIcon = (azureType?: string): string => {
    if (!azureType) return ICON_BASE + DEFAULT_ICON;
    const t = azureType.toLowerCase();
    const match = Object.keys(ICON_LOOKUP).find(k => t.includes(k));
    return ICON_BASE + (match ? ICON_LOOKUP[match] : DEFAULT_ICON);
};

// Get friendly name for resource type (copied from GraphCard.tsx)
const getFriendlyName = (azureType?: string, subType?: string): string => {
    if (!azureType) return 'Resource';
    const FRIENDLY_NAMES: Record<string, string> = {
        containerapp: 'Container App',
        containerappjob: 'Container App Job',
        managedenvironment: 'Managed Environment',
        aks: 'Kubernetes Service',
        managedcluster: 'Kubernetes Service',
        kubernetes: 'Kubernetes Service',
        scaleset: 'Scale Set',
        webapp: 'Web App',
        functionapp: 'Function App',
        site: 'Web App',
        cosmos: 'Cosmos DB',
        cosmosdb: 'Cosmos DB',
        sql: 'SQL Server',
        sqlserver: 'SQL Server',
        redis: 'Redis Cache',
        cache: 'Redis Cache',
        vnet: 'Virtual Network',
        virtualnetwork: 'Virtual Network',
        subnet: 'Subnet',
        nsg: 'Network Security Group',
        networksecuritygroup: 'Network Security Group',
    };

    if (subType == 'k8s/apps/v1/deployments') {
        return 'Kubernetes Deployment';
    }
    if (subType == 'k8s/apps/v1/statefulsets') {
        return 'Kubernetes StatefulSet';
    }

    const t = azureType.toLowerCase();
    const match = Object.keys(FRIENDLY_NAMES).find(k => t.includes(k));
    if (match) return FRIENDLY_NAMES[match];
    const typeArray = azureType.split('/');
    return typeArray[typeArray.length - 1];
};

/* ────────────────────────────────  COMPONENT  ──────────────────────────────── */

interface AzureSREWelcomeProps {
    threadId?: string | null;
}

const AzureSREWelcome = ({ threadId }: AzureSREWelcomeProps) => {
    const styles = useStyles();
    const inputId = useId('github-repo-input');
    const { dispatchToast } = useToastController();

    /* --------------------------------------------------------------------- */
    /*  1.  STATE                                                            */
    /* --------------------------------------------------------------------- */

    const [knowledgeGraphStatus, setKnowledgeGraphStatus] = useState<KnowledgeGraphStatus | null>(null);
    const [integrations, setIntegrations] = useState<IntegrationStatus[]>([]);
    const [logicalApps, setLogicalApps] = useState<LogicalApplication[]>([]);
    const [expandedFeature, setExpandedFeature] = useState<string | null>(null);
    const [selectedAppIndex, setSelectedAppIndex] = useState(0);

    // Collapsible sections state
    const [isAnalysisCollapsed, setIsAnalysisCollapsed] = useState(false);
    const [isAppsCollapsed, setIsAppsCollapsed] = useState(false);
    const [isIntegrationsCollapsed, setIsIntegrationsCollapsed] = useState(false);

    const hasAutoCollapsed = useRef({
        analysis: false,
        apps: false,
        integrations: false,
    });

    // GitHub linking dialog state
    const [linkDialogOpen, setLinkDialogOpen] = useState(false);
    const [reAuthDialogOpen, setReAuthDialogOpen] = useState(false);
    const [repoUrl, setRepoUrl] = useState('');
    const [repoUrlError, setRepoUrlError] = useState<string | null>(null);
    const [isLinking, setIsLinking] = useState(false);

    /* --------------------------------------------------------------------- */
    /*  2.  DATA FETCHING (poll every 10 s)                                  */
    /* --------------------------------------------------------------------- */

    useEffect(() => {
        const fetchWelcomeMessage = async () => {
            try {
                // Only attempt to fetch if threadId exists
                if (!threadId) {
                    console.log('No threadId available, skipping fetch');
                    return;
                }

                const res = await fetch(`../api/v1/threads/${threadId}/welcomeMessage`, {
                    headers: getAgentHeaders(),
                });
                if (!res.ok) throw new Error(await res.text());
                const data: WelcomeMessageResponse = await res.json();

                // Process the data
                if (data) {
                    // Update knowledgeGraphStatus
                    if (data.knowledgeGraphStatus) {
                        setKnowledgeGraphStatus(data.knowledgeGraphStatus);

                        // Auto-collapse analysis section if knowledge graph is complete
                        if (data.knowledgeGraphStatus.status === 'Completed' && !hasAutoCollapsed.current.analysis) {
                            setIsAnalysisCollapsed(true);
                            hasAutoCollapsed.current.analysis = true;
                        }
                    }

                    // Update integrations
                    if (data.integrations) {
                        setIntegrations(data.integrations);

                        // Auto-collapse integrations section if all are active
                        const allIntegrationsActive =
                            data.integrations.length > 0 && data.integrations.every(integration => integration.isActive);
                        if (allIntegrationsActive && !hasAutoCollapsed.current.integrations) {
                            setIsIntegrationsCollapsed(true);
                            hasAutoCollapsed.current.integrations = true;
                        }
                    }

                    // Update logical applications with enhanced information
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

                        // Auto-collapse applications section if all repos are linked
                        const allReposLinked =
                            enhancedApps.length > 0 && enhancedApps.every(app => app.sourceCodeLinkageStatus?.status === 'Linked');
                        if (allReposLinked && !hasAutoCollapsed.current.apps) {
                            setIsAppsCollapsed(true);
                            hasAutoCollapsed.current.apps = true;
                        }
                    }
                }
            } catch (err) {
                console.error('Failed to fetch welcome message', err);
            }
        };

        // initial fetch
        fetchWelcomeMessage();
        // start polling
        const intervalId = setInterval(fetchWelcomeMessage, 10000);

        return () => clearInterval(intervalId);
    }, [threadId]);

    /* --------------------------------------------------------------------- */
    /*  3.  HELPERS                                                          */
    /* --------------------------------------------------------------------- */

    const validateGitHubUrl = (url: string): boolean => {
        const githubUrlRegex = /^https:\/\/github\.com[/:][\w.-]+\/[\w.-]+\.?(?:git)?$/;
        return githubUrlRegex.test(url);
    };

    const remainingRepos = logicalApps.filter(
        a =>
            a.sourceCodeLinkageStatus?.status === 'NotLinked' ||
            a.sourceCodeLinkageStatus?.status === 'RequiresAuth' ||
            a.sourceCodeLinkageStatus?.status === 'RequiresReAuth'
    ).length;

    /* --------------------------------------------------------------------- */
    /*  4.  EVENT HANDLERS                                                   */
    /* --------------------------------------------------------------------- */

    const handleRepoUrlChange = (e: React.ChangeEvent<HTMLInputElement>) => {
        const url = e.target.value;
        setRepoUrl(url);
        if (url && !validateGitHubUrl(url)) {
            setRepoUrlError('Repository URL must be of the form https://github.com/owner/repo-name.git');
        } else {
            setRepoUrlError(null);
        }
    };

    const handleOpenLinkDialog = (resourceId: string) => {
        setRepoUrl('');
        setRepoUrlError(null);
        setLinkDialogOpen(true);
        // store the resourceId in state via selectedAppIndex
        const idx = logicalApps.findIndex(a => a.resourceId === resourceId);
        if (idx !== -1) setSelectedAppIndex(idx);
    };

    const handleOpenReAuthDialog = (resourceId: string) => {
        setReAuthDialogOpen(true);
        // store the resourceId in state via selectedAppIndex
        const idx = logicalApps.findIndex(a => a.resourceId === resourceId);
        if (idx !== -1) setSelectedAppIndex(idx);
    };

    const handleCloseLinkDialog = () => {
        setLinkDialogOpen(false);
        setRepoUrl('');
        setRepoUrlError(null);
    };

    const handleCloseReAuthDialog = () => {
        setReAuthDialogOpen(false);
    };

    const handleLinkRepo = async () => {
        if (!validateGitHubUrl(repoUrl)) {
            setRepoUrlError('Repository URL must be of the form https://github.com/owner/repo-name.git');
            return;
        }

        // Make sure we have a valid selectedApp before proceeding
        const selectedApp = logicalApps[selectedAppIndex];
        if (!selectedApp) {
            console.error('No application selected for linking');
            return;
        }

        const resourceId = selectedApp.resourceId;

        setIsLinking(true);
        try {
            const response = await fetch('../api/v1/github/link', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    ...getAgentHeaders(),
                },
                body: JSON.stringify({ ResourceId: resourceId, RepoUrl: repoUrl }),
            });
            if (!response.ok) {
                const errorData = await response.json().catch(() => ({}));
                throw new Error(errorData.message || 'Failed to link repository');
            }

            // optimistic UI update with proper null checks
            setLogicalApps(prev =>
                prev.map(app => {
                    if (app.resourceId === resourceId) {
                        return {
                            ...app,
                            sourceCodeLinkageStatus: {
                                status: 'Linked',
                                repositoryUrl: repoUrl,
                                linkedTimestamp: new Date().toISOString(),
                            },
                        };
                    }
                    return app;
                })
            );

            setLinkDialogOpen(false);
            dispatchToast(
                <Toast>
                    <ToastTitle>Repository linked successfully</ToastTitle>
                </Toast>,
                { intent: 'success', timeout: 3000 }
            );
        } catch (err) {
            console.error(err);
            dispatchToast(
                <Toast>
                    <ToastTitle>Failed to link repository</ToastTitle>
                </Toast>,
                { intent: 'error', timeout: 5000 }
            );
        } finally {
            setIsLinking(false);
        }
    };

    const handleReAuth = async () => {
        // Make sure we have a valid selectedApp before proceeding
        const selectedApp = logicalApps[selectedAppIndex];
        if (!selectedApp || !selectedApp.sourceCodeLinkageStatus.loginCallbackUrl) {
            console.error('No valid application or login URL for re-authentication');
            return;
        }

        // Open the authentication window
        window.open(selectedApp.sourceCodeLinkageStatus.loginCallbackUrl, 'githubAuth', 'width=600,height=700');

        setReAuthDialogOpen(false);
    };

    // Toggle section collapses
    const toggleFeature = (feature: string) => setExpandedFeature(expandedFeature === feature ? null : feature);
    const toggleAnalysisSection = () => setIsAnalysisCollapsed(prev => !prev);
    const toggleAppsSection = () => setIsAppsCollapsed(prev => !prev);
    const toggleIntegrationsSection = () => setIsIntegrationsCollapsed(prev => !prev);

    // Calculate resource counts from knowledgeGraphStatus
    const getResourceCounts = () => {
        if (!knowledgeGraphStatus) {
            return { total: 0, webApps: 0, databaseResources: 0 };
        }

        // Attempt to calculate total resources from crawlProgress
        const total = knowledgeGraphStatus.crawlProgress.totalResources || 0;

        // Look for web apps and database resources in crawlProgressByResourceType
        let webApps = 0;
        let databaseResources = 0;

        const resourceTypes = knowledgeGraphStatus.crawlProgressByResourceType || {};
        Object.keys(resourceTypes).forEach(type => {
            if (type.toLowerCase().includes('web') || type.toLowerCase().includes('site')) {
                webApps += resourceTypes[type].totalResources;
            } else if (
                type.toLowerCase().includes('sql') ||
                type.toLowerCase().includes('cosmos') ||
                type.toLowerCase().includes('redis') ||
                type.toLowerCase().includes('database')
            ) {
                databaseResources += resourceTypes[type].totalResources;
            }
        });

        return { total, webApps, databaseResources };
    };

    const resourceCounts = getResourceCounts();

    // Calculate scan progress percentage
    const getScanProgress = () => {
        if (!knowledgeGraphStatus) return 0;

        if (knowledgeGraphStatus.status === 'Completed') return 100;

        // Calculate percentage from crawlProgress
        const { crawled, totalResources } = knowledgeGraphStatus.crawlProgress;
        if (totalResources === 0) return 0;

        return Math.round((crawled / totalResources) * 100);
    };

    const scanProgress = getScanProgress();

    // Helper to get resource learning message
    const getResourceLearningMessage = () => {
        if (!knowledgeGraphStatus) return null;

        if (knowledgeGraphStatus.status === 'Completed') {
            return 'I have learned about your resources and can now provide insights and assistance tailored to your environment.';
        } else {
            return "I'm currently learning about your resources and analyzing your environment to provide better assistance...";
        }
    };

    /* --------------------------------------------------------------------- */
    /*  5.  RENDER                                                           */
    /* --------------------------------------------------------------------- */

    return (
        <div className={styles.container}>
            {/* Welcome Header */}
            <Card className={styles.headerCard}>
                <CardHeader
                    header={
                        <Title2 style={{ fontWeight: tokens.fontWeightSemibold, color: tokens.colorNeutralForeground1 }}>
                            👋 Hi, I'm your Azure SRE Partner!
                        </Title2>
                    }
                />
                <div className={styles.welcomeMessage}>
                    <Body1>
                        I'm here to help monitor your applications and keep everything running smoothly. Think of me as your reliable
                        sidekick for all things related to system reliability and operations. I've already started scanning your environment
                        and will proactively work on your behalf!
                    </Body1>
                    {/* Feature Grid (interactive) */}
                    <div className={styles.featureGrid}>
                        <div className={styles.featureItem} onClick={() => toggleFeature('monitoring')}>
                            <Screenshot24Regular className={styles.featureIcon} />
                            <Text size={300} weight="semibold">
                                24/7 Monitoring
                            </Text>
                        </div>
                        <div className={styles.featureItem} onClick={() => toggleFeature('incident')}>
                            <Alert24Regular className={styles.featureIcon} />
                            <Text size={300} weight="semibold">
                                Incident Response
                            </Text>
                        </div>
                        <div className={styles.featureItem} onClick={() => toggleFeature('integrations')}>
                            <AppGeneric24Regular className={styles.featureIcon} />
                            <Text size={300} weight="semibold">
                                Built-in Integrations
                            </Text>
                        </div>
                    </div>
                    {/* Expanded Feature Details */}
                    {expandedFeature === 'monitoring' && (
                        <div className={styles.featureContent}>
                            <Subtitle1>24/7 Monitoring</Subtitle1>
                            <div className={styles.featureDetails}>
                                {[
                                    'Real-time performance tracking across all your applications',
                                    'Customizable alert thresholds for critical services',
                                    'Proactive identification of potential issues',
                                ].map((t, i) => (
                                    <div key={i} className={styles.featureDetailItem}>
                                        <CheckmarkCircle24Regular className={styles.checkIcon} />
                                        <Text size={200}>{t}</Text>
                                    </div>
                                ))}
                            </div>
                        </div>
                    )}
                    {expandedFeature === 'incident' && (
                        <div className={styles.featureContent}>
                            <Subtitle1>Incident Response</Subtitle1>
                            <div className={styles.featureDetails}>
                                {[
                                    'Automated incident detection and triage',
                                    'Guided troubleshooting for faster resolution',
                                    'Post-incident analysis and recommendations',
                                ].map((t, i) => (
                                    <div key={i} className={styles.featureDetailItem}>
                                        <CheckmarkCircle24Regular className={styles.checkIcon} />
                                        <Text size={200}>{t}</Text>
                                    </div>
                                ))}
                            </div>
                        </div>
                    )}
                    {expandedFeature === 'integrations' && (
                        <div className={styles.featureContent}>
                            <Subtitle1>Built-in Integrations</Subtitle1>
                            <div className={styles.featureDetails}>
                                {[
                                    'PagerDuty for alerting and on-call management',
                                    'Azure Monitor for telemetry analysis',
                                    'GitHub for deployment tracking and issue management',
                                ].map((t, i) => (
                                    <div key={i} className={styles.featureDetailItem}>
                                        <CheckmarkCircle24Regular className={styles.checkIcon} />
                                        <Text size={200}>{t}</Text>
                                    </div>
                                ))}
                            </div>
                        </div>
                    )}
                </div>
            </Card>

            {/* Environment Analysis */}
            <Card className={styles.sectionCard}>
                <div className={styles.sectionHeader}>
                    <div className={styles.collapsibleHeader} onClick={toggleAnalysisSection}>
                        <div style={{ display: 'flex', alignItems: 'center' }}>
                            <DocumentData24Regular className={styles.sectionHeaderIcon} />
                            <Text weight="semibold" size={500}>
                                Environment Analysis
                            </Text>
                            {/* Add green check icon when analysis is complete */}
                            {knowledgeGraphStatus?.status === 'Completed' && (
                                <CheckmarkCircle24Regular className={styles.statusCompleteIcon} />
                            )}
                        </div>
                        {isAnalysisCollapsed ? <ChevronRight20Regular /> : <ChevronDown20Regular />}
                    </div>
                </div>
                <Collapse visible={!isAnalysisCollapsed}>
                    <div className={styles.sectionContent}>
                        {/* Add learning resources message */}
                        {getResourceLearningMessage() && (
                            <div className={styles.resourceLearningMessage}>
                                <Text size={300}>{getResourceLearningMessage()}</Text>
                            </div>
                        )}
                        <div className={styles.progressContainer}>
                            <div className={styles.progressHeader}>
                                <Text
                                    size={300}
                                    weight="medium"
                                    style={{
                                        color:
                                            knowledgeGraphStatus?.status === 'Completed' ? tokens.colorPaletteGreenForeground1 : undefined,
                                    }}
                                >
                                    Status: {knowledgeGraphStatus?.status || 'Building...'}
                                </Text>
                                <Text size={300} weight="medium">
                                    {scanProgress}%
                                </Text>
                            </div>
                            <ProgressBar
                                value={scanProgress / 100}
                                color={knowledgeGraphStatus?.status === 'Completed' ? 'success' : 'brand'}
                                thickness="large"
                                style={{ height: '8px' }}
                            />
                        </div>
                        <div className={styles.statsGrid}>
                            <div className={styles.statCard}>
                                <Text className={styles.statValue}>{resourceCounts.total}</Text>
                                <Text className={styles.statLabel}> Total Resources</Text>
                            </div>
                            <div className={styles.statCard}>
                                <Text className={styles.statValue}>{resourceCounts.webApps}</Text>
                                <Text className={styles.statLabel}> Web Apps</Text>
                            </div>
                            <div className={styles.statCard}>
                                <Text className={styles.statValue}>{resourceCounts.databaseResources}</Text>
                                <Text className={styles.statLabel}> Database Resources</Text>
                            </div>
                        </div>
                    </div>
                </Collapse>
            </Card>

            {/* Logical Applications section */}
            <Card className={styles.sectionCard}>
                <div className={styles.sectionHeader}>
                    <div className={styles.collapsibleHeader} onClick={toggleAppsSection}>
                        <div style={{ display: 'flex', alignItems: 'center' }}>
                            <GridDots24Regular className={styles.sectionHeaderIcon} />
                            <Text weight="semibold" size={500}>
                                Logical Applications
                            </Text>
                            {/* Show completed status icon when all repos are linked */}
                            {logicalApps.length > 0 && remainingRepos === 0 && (
                                <CheckmarkCircle24Regular className={styles.statusCompleteIcon} />
                            )}
                        </div>
                        {isAppsCollapsed ? <ChevronRight20Regular /> : <ChevronDown20Regular />}
                    </div>
                </div>
                <Collapse visible={!isAppsCollapsed}>
                    <div className={styles.sectionContent}>
                        {/* Show spinner if still loading */}
                        {logicalApps.length === 0 && (
                            <div style={{ padding: tokens.spacingVerticalL, display: 'flex', justifyContent: 'center' }}>
                                <Spinner label="Loading applications" />
                            </div>
                        )}
                        {logicalApps.length > 0 && remainingRepos > 0 && (
                            <div
                                className={styles.reposRemainingTag}
                                style={{
                                    marginBottom: tokens.spacingVerticalM,
                                    display: 'inline-flex',
                                }}
                            >
                                <Link16Regular />
                                <Text size={200}>
                                    {remainingRepos} repo{remainingRepos > 1 ? 's' : ''} remaining
                                </Text>
                            </div>
                        )}
                        {/* Application List */}
                        {logicalApps.length > 0 && (
                            <div className={styles.applicationList}>
                                {logicalApps.map((app, index) => (
                                    <div key={index} className={styles.applicationCard}>
                                        <div className={styles.applicationCardLeft}>
                                            {/* Updated to use resource type icon */}
                                            <div className={styles.applicationIcon}>
                                                {app.properties?.type ? (
                                                    <img
                                                        src={resolveIcon(app.properties.type)}
                                                        alt={getFriendlyName(app.properties.type, app.subType)}
                                                        width={24}
                                                        height={24}
                                                    />
                                                ) : (
                                                    <AppGeneric24Regular />
                                                )}
                                            </div>
                                            <div className={styles.applicationInfo}>
                                                <Text className={styles.applicationName}>{app.name || 'Resource'}</Text>
                                                <Text className={styles.applicationSubtext}>
                                                    {getFriendlyName(app.properties?.type, app.subType)}
                                                </Text>

                                                {/* Repository Link Status */}
                                                {app.sourceCodeLinkageStatus?.status === 'Linked' &&
                                                    app.sourceCodeLinkageStatus?.repositoryUrl && (
                                                        <div className={styles.linkStatus}>
                                                            <div
                                                                className={styles.statusDot}
                                                                style={{
                                                                    backgroundColor:
                                                                        app.sourceCodeLinkageStatus.status === 'Linked'
                                                                            ? tokens.colorPaletteGreenForeground1
                                                                            : tokens.colorPaletteYellowForeground1,
                                                                }}
                                                            />
                                                            <Link
                                                                href={app.sourceCodeLinkageStatus.repositoryUrl}
                                                                target="_blank"
                                                                style={{ fontSize: tokens.fontSizeBase300 }}
                                                            >
                                                                <FaGithub style={{ marginRight: '4px' }} />
                                                                {app.sourceCodeLinkageStatus.repositoryUrl}
                                                            </Link>
                                                        </div>
                                                    )}

                                                {/* Repository Re-Auth Required Status */}
                                                {app.sourceCodeLinkageStatus?.status === 'RequiresReAuth' && (
                                                    <div className={styles.linkStatus}>
                                                        <div
                                                            className={styles.statusDot}
                                                            style={{ backgroundColor: tokens.colorPaletteYellowForeground1 }}
                                                        />
                                                        <Text size={200} style={{ color: tokens.colorPaletteYellowForeground1 }}>
                                                            Authentication expired
                                                        </Text>
                                                    </div>
                                                )}
                                            </div>
                                        </div>
                                        <div style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalL }}>
                                            {/* Link Repository Button */}
                                            {app.sourceCodeLinkageStatus?.status === 'NotLinked' && (
                                                <Button
                                                    appearance="primary"
                                                    size="medium"
                                                    icon={<Link16Regular />}
                                                    onClick={() => handleOpenLinkDialog(app.resourceId)}
                                                >
                                                    Link Repository
                                                </Button>
                                            )}

                                            {/* Re-Authentication Required Button */}
                                            {app.sourceCodeLinkageStatus?.status === 'RequiresReAuth' && (
                                                <Button
                                                    appearance="primary"
                                                    size="medium"
                                                    icon={<ArrowRight16Regular />}
                                                    onClick={() => handleOpenReAuthDialog(app.resourceId)}
                                                >
                                                    Re-Authenticate
                                                </Button>
                                            )}

                                            {/* Authentication Required Button */}
                                            {app.sourceCodeLinkageStatus?.status === 'RequiresAuth' &&
                                                app.sourceCodeLinkageStatus?.loginCallbackUrl && (
                                                    <>
                                                        {app.sourceCodeLinkageStatus.repositoryUrl && (
                                                            <div style={{ marginBottom: '8px', display: 'flex', alignItems: 'center' }}>
                                                                <FaGithub style={{ margin: '0 4px 0 0' }} />
                                                                <a
                                                                    href={app.sourceCodeLinkageStatus.repositoryUrl}
                                                                    target="_blank"
                                                                    rel="noopener noreferrer"
                                                                >
                                                                    {app.sourceCodeLinkageStatus.repositoryUrl}
                                                                </a>
                                                            </div>
                                                        )}

                                                        <Button
                                                            appearance="primary"
                                                            size="medium"
                                                            icon={<ArrowRight16Regular />}
                                                            onClick={() =>
                                                                window.open(
                                                                    app.sourceCodeLinkageStatus.loginCallbackUrl!,
                                                                    'githubAuth',
                                                                    'width=600,height=700'
                                                                )
                                                            }
                                                        >
                                                            Login to allow access
                                                        </Button>
                                                    </>
                                                )}
                                        </div>
                                    </div>
                                ))}
                            </div>
                        )}
                    </div>
                </Collapse>
            </Card>

            {/* Active Integrations Section */}
            <Card className={styles.sectionCard}>
                <div className={styles.sectionHeader}>
                    <div className={styles.collapsibleHeader} onClick={toggleIntegrationsSection}>
                        <div style={{ display: 'flex', alignItems: 'center' }}>
                            <ArrowSync24Regular className={styles.sectionHeaderIcon} />
                            <Text weight="semibold" size={500}>
                                Active Integrations
                            </Text>
                            {integrations.length > 0 && (
                                <div className={styles.reposRemainingTag} style={{ marginLeft: tokens.spacingHorizontalM }}>
                                    <ArrowSync24Regular style={{ fontSize: '14px' }} />
                                    <Text size={200}>
                                        {integrations.filter(i => i.isActive).length}/{integrations.length} active
                                    </Text>
                                </div>
                            )}
                            {/* Show completed status icon when all integrations are active */}
                            {integrations.length > 0 && integrations.every(i => i.isActive) && (
                                <CheckmarkCircle24Regular className={styles.statusCompleteIcon} />
                            )}
                        </div>
                        {isIntegrationsCollapsed ? <ChevronRight20Regular /> : <ChevronDown20Regular />}
                    </div>
                </div>
                <Collapse visible={!isIntegrationsCollapsed}>
                    <div className={styles.sectionContent}>
                        {integrations.length === 0 ? (
                            <div style={{ padding: tokens.spacingVerticalM, textAlign: 'center' }}>
                                <Text>No integrations configured</Text>
                            </div>
                        ) : (
                            integrations.map((integration, index) => (
                                <div key={index} className={styles.integrationCard}>
                                    <div className={styles.integrationHeader}>
                                        <Text weight="semibold" size={300}>
                                            {integration.name}
                                        </Text>
                                        <div className={integration.isActive ? styles.activeBadge : styles.inactiveBadge}>
                                            {integration.isActive ? 'Active' : 'Inactive'}
                                        </div>
                                    </div>
                                    <div className={styles.integrationDetails}>
                                        <Text size={200} color="subtle">
                                            {integration.details}
                                        </Text>
                                    </div>
                                    <div className={styles.integrationAction}>
                                        <Button
                                            appearance={integration.isActive ? 'primary' : 'secondary'}
                                            size="small"
                                            icon={<ArrowRight16Regular />}
                                        >
                                            {integration.isActive ? 'View Dashboard' : 'Configure'}
                                        </Button>
                                    </div>
                                </div>
                            ))
                        )}
                    </div>
                </Collapse>
            </Card>

            {/* GitHub Link Dialog */}
            <Dialog
                open={linkDialogOpen}
                onOpenChange={(_, data) => {
                    if (!data.open) handleCloseLinkDialog();
                }}
            >
                <DialogSurface>
                    <DialogBody>
                        <div className={styles.dialogHeader}>
                            <div className={styles.dialogTitle}>
                                <FaGithub className={styles.dialogTitleIcon} />
                                <DialogTitle>Link GitHub Repository</DialogTitle>
                            </div>
                            <button className={styles.dialogCloseButton} onClick={handleCloseLinkDialog} aria-label="Close">
                                <Dismiss24Regular />
                            </button>
                        </div>
                        <DialogContent>
                            <Field
                                label="GitHub Repository URL"
                                validationMessage={repoUrlError}
                                validationState={repoUrlError ? 'error' : 'none'}
                                hint="Format: https://github.com/owner/repo-name.git"
                            >
                                <Input
                                    id={inputId}
                                    placeholder="https://github.com/owner/repo-name.git"
                                    value={repoUrl}
                                    onChange={handleRepoUrlChange}
                                    disabled={isLinking}
                                />
                            </Field>
                        </DialogContent>
                        <DialogActions>
                            <Button appearance="secondary" onClick={handleCloseLinkDialog} disabled={isLinking}>
                                Cancel
                            </Button>
                            <Button
                                appearance="primary"
                                onClick={handleLinkRepo}
                                disabled={isLinking || !!repoUrlError || !repoUrl}
                                icon={isLinking ? <Spinner size="tiny" /> : null}
                            >
                                {isLinking ? 'Linking…' : 'Link Repository'}
                            </Button>
                        </DialogActions>
                    </DialogBody>
                </DialogSurface>
            </Dialog>

            {/* GitHub Re-Authentication Dialog */}
            <Dialog
                open={reAuthDialogOpen}
                onOpenChange={(_, data) => {
                    if (!data.open) handleCloseReAuthDialog();
                }}
            >
                <DialogSurface>
                    <DialogBody>
                        <div className={styles.dialogHeader}>
                            <div className={styles.dialogTitle}>
                                <FaGithub className={styles.dialogTitleIcon} />
                                <DialogTitle>Re-Authenticate GitHub</DialogTitle>
                            </div>
                            <button className={styles.dialogCloseButton} onClick={handleCloseReAuthDialog} aria-label="Close">
                                <Dismiss24Regular />
                            </button>
                        </div>
                        <DialogContent>
                            <Text>
                                Your GitHub authentication has expired or been revoked. You need to re-authenticate to continue accessing
                                the repository.
                            </Text>
                            {selectedAppIndex !== -1 && logicalApps[selectedAppIndex]?.sourceCodeLinkageStatus?.repositoryUrl && (
                                <div style={{ marginTop: tokens.spacingVerticalM }}>
                                    <Text weight="semibold">Repository:</Text>
                                    <Link
                                        href={logicalApps[selectedAppIndex].sourceCodeLinkageStatus.repositoryUrl ?? undefined}
                                        target="_blank"
                                        style={{ display: 'flex', alignItems: 'center', marginTop: tokens.spacingVerticalXS }}
                                    >
                                        <FaGithub style={{ marginRight: '4px' }} />
                                        {logicalApps[selectedAppIndex].sourceCodeLinkageStatus.repositoryUrl}
                                    </Link>
                                </div>
                            )}
                        </DialogContent>
                        <DialogActions>
                            <Button appearance="secondary" onClick={handleCloseReAuthDialog}>
                                Cancel
                            </Button>
                            <Button appearance="primary" onClick={handleReAuth}>
                                Re-Authenticate
                            </Button>
                        </DialogActions>
                    </DialogBody>
                </DialogSurface>
            </Dialog>
        </div>
    );
};

export default AzureSREWelcome;
