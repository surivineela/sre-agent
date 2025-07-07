import {
    Button,
    Caption1,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    DialogTrigger,
    Field,
    Image,
    Label,
    Link,
    makeStyles,
    mergeClasses,
    Spinner,
    Text,
    Textarea,
    TextareaOnChangeData,
    Toaster,
    tokens,
} from '@fluentui/react-components';
import axios from 'axios';
import { memo, ReactNode, useContext, useEffect, useMemo, useState } from 'react';
import { FaGithub } from 'react-icons/fa';
import { FormattedMessage, useIntl } from 'react-intl';
import { useLocation, useNavigate } from 'react-router-dom';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { getSafeDateTime } from '../../Common/Helpers/Date';
import { getAgentHeaders } from '../../Common/Helpers/headers';
import { isPaasResourceType } from '../../Common/Helpers/Resources';
import { ResourceInfoResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { GraphContext, GraphNode, ResourceExtended } from '../Contracts/Graph';
import { getPropertyValue, useResourceInfo } from '../Hooks/useResourceInfo';
import HealthStatus from './HealthStatus';
import { getAppHealthInfo } from './Utility';

const githubRepoRegex = /^https:\/\/github\.com\/[\w-]+\/[\w-]+\.git$/;
const azdoRepoRegex = /^https:\/\/(?:dev\.azure\.com\/|[\w-]+\.visualstudio\.com\/)[\w-]+\/[\w-]+\/_git\/[\w.-]+$/;

const isNullOrUndefined = (input?: unknown): boolean => {
    return input === undefined || input === null;
};

const useStyles = makeStyles({
    root: {
        maxWidth: '300px',
        minWidth: '150px',
        padding: '20px',
        height: 'calc(100% - 8px)',
        backgroundColor: tokens.colorNeutralBackground3,
        flex: '1 1 auto',
        overflowY: 'auto',
        position: 'relative',
    },
    infoContent: {
        width: '100%',
        height: '100%',
    },
    title: {
        lineHeight: '20px',
    },
    content: {
        margin: '20px 0px',
    },
    spinner: {
        position: 'absolute',
        top: '50%',
        left: '50%',
    },
    textarea: {
        display: 'block',
        width: '100%',
    },
    textareaInner: {
        width: '100%',
    },
    sectionField: {
        borderBottom: '1px solid rgba(204,204,204,.8)',
        padding: '10px 5px',
    },
    sectionFieldText: {
        wordBreak: 'break-word',
    },
    sectionFieldValueText: {
        padding: '0px 6px',
        gridRowStart: '1',
        gridRowEnd: '-1',
        lineHeight: '20px',
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'center',
    },
    dashboard: {
        display: 'flex',
        flexDirection: 'row',
        alignItems: 'center',
        gap: '5px',
    },
    githubButton: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
    },
    githubIcon: {
        marginRight: '4px',
    },
});

const ResourceInfo = () => {
    const { selectedNode } = useContext(GraphContext);
    const { sreAgentEndpoint } = useContext(EnvironmentContext);

    const { root } = useStyles();

    const handleRepositoryLogin = async () => {
        if (!selectedNode?.id) return;

        try {
            const response = await fetch(`${sreAgentEndpoint}/api/v1/github/auth?resourceId=${selectedNode.id}`, {
                headers: getAgentHeaders(),
            });
            if (!response.ok) throw new Error('Failed to get GitHub auth URL');

            const data = await response.json();
            if (data.loginCallbackUrl) {
                const w = window.open(data.loginCallbackUrl, 'githubAuth', 'width=600,height=700');
                if (w) {
                    const onLoad = () => {
                        w.removeEventListener('load', onLoad);
                        window.location.reload(); // Reload the page to reflect the login
                    };
                    w.addEventListener('load', onLoad);
                }
            }
        } catch (err) {
            console.error('Failed to initiate GitHub login:', err);
        }
    };

    return (
        <div className={root}>
            <ResourceInfoContent selectedNode={selectedNode} onGitHubLogin={handleRepositoryLogin} />
        </div>
    );
};

const ResourceInfoContent = ({ selectedNode }: { selectedNode?: GraphNode; onGitHubLogin: () => void }) => {
    const { isLoading, isUpdating, initialRemarks, resource, onSubmit, toasterId } = useResourceInfo(selectedNode);
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const { infoContent, title, spinner, content, dashboard, githubButton, githubIcon } = useStyles();
    const intl = useIntl();

    const [isLinkDialogOpen, setIsLinkDialogOpen] = useState(false);
    const [repoUrl, setRepoUrl] = useState('');
    const [isLinking, setIsLinking] = useState(false);
    const [repoUrlError, setRepoUrlError] = useState('');

    const properties = resource?.properties;

    const isPaasResource = useMemo<boolean>(() => isPaasResourceType(resource?.type), [resource]);

    const handleLinkRepository = async () => {
        if (!selectedNode?.id || !repoUrl) return;

        setIsLinking(true);

        // If the url matches github fetch here.
        if (githubRepoRegex.test(repoUrl)) {
            try {
                const response = await fetch(`${sreAgentEndpoint}/api/v1/github/link`, {
                    method: 'POST',
                    headers: {
                        ...getAgentHeaders(),
                        'Content-Type': 'application/json',
                    },
                    body: JSON.stringify({
                        resourceId: selectedNode.id,
                        repoUrl: repoUrl,
                        SubType: '',
                        Namespace: '',
                        ResourceName: '',
                    }),
                });

                if (!response.ok) throw new Error('Failed to link repository');

                // Refresh the resource info
                window.location.reload();
            } catch (err) {
                console.error('Failed to link repository:', err);
            } finally {
                setIsLinking(false);
                setIsLinkDialogOpen(false);
            }
        } else if (azdoRepoRegex.test(repoUrl)) {
            try {
                const response = await fetch(`${sreAgentEndpoint}/api/v1/azuredevops/link`, {
                    method: 'POST',
                    headers: {
                        ...getAgentHeaders(),
                        'Content-Type': 'application/json',
                    },
                    body: JSON.stringify({
                        resourceId: selectedNode.id,
                        repoUrl: repoUrl,
                        SubType: '',
                        Namespace: '',
                        ResourceName: '',
                    }),
                });

                if (!response.ok) throw new Error('Failed to link repository');

                // Refresh the resource info
                window.location.reload();
            } catch (err) {
                console.error('Failed to link repository:', err);
            } finally {
                setIsLinking(false);
                setIsLinkDialogOpen(false);
            }
        } else {
            setRepoUrlError(intl.formatMessage(ResourceInfoResources.repositoryUrlErrorMessage));
            setIsLinking(false);
        }
    };

    return selectedNode ? (
        <div className={infoContent}>
            <Text as="h2" size={600} weight="semibold" className={title}>
                {selectedNode?.name ?? ''}
            </Text>

            <div>
                {isLoading ? (
                    <Spinner size="large" className={spinner} />
                ) : (
                    <div className={content}>
                        <SummaryField
                            label={intl.formatMessage(ResourceInfoResources.name)}
                            value={getPropertyValue(properties?.resourceName)}
                        />
                        <SummaryField
                            label={intl.formatMessage(ResourceInfoResources.type)}
                            value={getPropertyValue(properties?.resourceType)}
                        />
                        <SummaryField
                            label={intl.formatMessage(SreAgentResources.resourceGroup)}
                            value={getPropertyValue(properties?.resourceGroupName)}
                        />
                        <SummaryField
                            label={intl.formatMessage(SreAgentResources.subscriptionId)}
                            value={getPropertyValue(properties?.subscriptionId)}
                        />
                        <AppHealthInfo resource={resource} />
                        <SummaryField label={intl.formatMessage(ResourceInfoResources.dashboard)}>
                            {resource?.dashboardUrl ? (
                                <div className={dashboard}>
                                    <Image
                                        src="./grafana-logo.svg"
                                        width={16}
                                        height={16}
                                        alt={intl.formatMessage(ResourceInfoResources.grafanaLogo)}
                                    />
                                    <Link href={resource.dashboardUrl} target="_blank" rel="noopener noreferrer">
                                        <FormattedMessage {...ResourceInfoResources.dashboardLinkText} />
                                    </Link>
                                </div>
                            ) : null}
                        </SummaryField>
                        {isPaasResource && (
                            <SummaryField label={intl.formatMessage(ResourceInfoResources.repositoryConnection)}>
                                {resource?.sourceCodeLinkageStatus ? (
                                    resource.sourceCodeLinkageStatus.status === 'Linked' ? (
                                        <div className={githubButton}>
                                            <FaGithub className={githubIcon} />
                                            <Link href={resource.sourceCodeLinkageStatus.repositoryUrl} target="_blank">
                                                {resource.sourceCodeLinkageStatus.repositoryUrl}
                                            </Link>
                                        </div>
                                    ) : (
                                        <div>
                                            {resource.sourceCodeLinkageStatus.repositoryUrl && (
                                                <div className={githubButton} style={{ marginBottom: '8px' }}>
                                                    <FaGithub className={githubIcon} />
                                                    <Link href={resource.sourceCodeLinkageStatus.repositoryUrl} target="_blank">
                                                        {resource.sourceCodeLinkageStatus.repositoryUrl}
                                                    </Link>
                                                </div>
                                            )}
                                            <Button
                                                appearance="primary"
                                                size="small"
                                                icon={<FaGithub className={githubIcon} />}
                                                onClick={() => {
                                                    const status = resource?.sourceCodeLinkageStatus;
                                                    if (status?.loginCallbackUrl) {
                                                        const w = window.open(
                                                            status.loginCallbackUrl,
                                                            'githubAuth',
                                                            'width=600,height=700'
                                                        );
                                                        if (w) {
                                                            const onLoad = () => {
                                                                w.removeEventListener('load', onLoad);
                                                                window.location.reload(); // Reload the page to reflect the login
                                                            };
                                                            w.addEventListener('load', onLoad);
                                                        }
                                                    }
                                                }}
                                            >
                                                <FormattedMessage {...ResourceInfoResources.authorizeRepositoryAccess} />
                                            </Button>
                                        </div>
                                    )
                                ) : (
                                    <>
                                        <Button
                                            appearance="primary"
                                            size="small"
                                            icon={<FaGithub className={githubIcon} />}
                                            onClick={() => setIsLinkDialogOpen(true)}
                                        >
                                            <FormattedMessage {...ResourceInfoResources.connectRepository} />
                                        </Button>
                                        <Dialog open={isLinkDialogOpen} onOpenChange={(_, data) => setIsLinkDialogOpen(data.open)}>
                                            <DialogSurface>
                                                <DialogBody>
                                                    <DialogTitle>
                                                        <FormattedMessage {...ResourceInfoResources.linkRepositoryToResource} />
                                                    </DialogTitle>
                                                    <DialogContent>
                                                        <Field
                                                            label={intl.formatMessage(ResourceInfoResources.repositoryUrl)}
                                                            validationState={repoUrlError ? 'error' : undefined}
                                                            validationMessage={repoUrlError}
                                                        >
                                                            <Textarea
                                                                placeholder="https://github.com/owner/repo-name.git or https://dev.azure.com/organization/project/_git/repo or https://organization.visualstudio.com/project/_git/repository-name"
                                                                value={repoUrl}
                                                                onChange={(_, data) => {
                                                                    setRepoUrl(data.value);

                                                                    if (
                                                                        !azdoRepoRegex.test(data.value) &&
                                                                        !githubRepoRegex.test(data.value)
                                                                    ) {
                                                                        setRepoUrlError(
                                                                            intl.formatMessage(
                                                                                ResourceInfoResources.repositoryUrlErrorMessage
                                                                            )
                                                                        );
                                                                    } else {
                                                                        setRepoUrlError('');
                                                                    }
                                                                }}
                                                                style={{ direction: 'ltr' }}
                                                            />
                                                        </Field>
                                                    </DialogContent>
                                                    <DialogActions>
                                                        <Button
                                                            appearance="primary"
                                                            disabled={!repoUrl || !!repoUrlError || isLinking}
                                                            onClick={handleLinkRepository}
                                                        >
                                                            {isLinking ? (
                                                                <FormattedMessage {...ResourceInfoResources.connecting} />
                                                            ) : (
                                                                <FormattedMessage {...ResourceInfoResources.connectRepository} />
                                                            )}
                                                        </Button>
                                                        <Button appearance="secondary" onClick={() => setIsLinkDialogOpen(false)}>
                                                            <FormattedMessage {...SreAgentResources.cancel} />
                                                        </Button>
                                                    </DialogActions>
                                                </DialogBody>
                                            </DialogSurface>
                                        </Dialog>
                                    </>
                                )}
                            </SummaryField>
                        )}
                        <SummaryField label={intl.formatMessage(ResourceInfoResources.annotation)}>
                            {initialRemarks ? <div>{initialRemarks}</div> : null}
                            <Dialog>
                                <DialogTrigger disableButtonEnhancement>
                                    <Link>
                                        {initialRemarks ? (
                                            <FormattedMessage {...ResourceInfoResources.editAnnotation} />
                                        ) : (
                                            <FormattedMessage {...ResourceInfoResources.addAnnotation} />
                                        )}
                                    </Link>
                                </DialogTrigger>
                                <AnnotationDialogSurface
                                    initialRemarks={initialRemarks}
                                    isUpdating={isUpdating}
                                    onSubmit={async (remarks: string) => {
                                        await onSubmit(remarks);
                                    }}
                                />
                            </Dialog>
                        </SummaryField>
                    </div>
                )}
            </div>
            <Toaster toasterId={toasterId} />
        </div>
    ) : null;
};

const AppHealthInfo = memo(({ resource }: { resource?: ResourceExtended }) => {
    const location = useLocation();
    const navigate = useNavigate();
    const intl = useIntl();
    const { sreAgentEndpoint } = useContext(EnvironmentContext);

    const appHealthInfo = getAppHealthInfo(resource);

    const [isSendingReport, setSendingReport] = useState(false);

    const createThread = async (resourceId: string) => {
        const url = `${sreAgentEndpoint}/api/v1/threads`;

        const response = await axios.post(
            url,
            {
                startMessage: {
                    text: `Resource ${resourceId} is unhealthy could you help diagnose what is wrong?`,
                    userId: 'web-client-user',
                    displayName: 'Web Client User',
                },
            },
            {
                headers: getAgentHeaders(),
            }
        );
        return response?.data;
    };

    return (
        appHealthInfo && (
            <div>
                <SummaryField
                    label={intl.formatMessage(ResourceInfoResources.appHealthInfoCost)}
                    value={
                        isNullOrUndefined(appHealthInfo.Costs) || appHealthInfo.Costs === 0
                            ? intl.formatMessage(ResourceInfoResources.appHealthInfoCostCalculationPending)
                            : `${appHealthInfo.Costs} USD`
                    }
                />
                <SummaryField
                    label={intl.formatMessage(ResourceInfoResources.appHealthInfoAvailability)}
                    value={!isNullOrUndefined(appHealthInfo.Availability) ? `${appHealthInfo.Availability ?? '0'}%` : undefined}
                />
                <SummaryField label={intl.formatMessage(ResourceInfoResources.appHealthInfoHealthStatus)}>
                    <HealthStatus
                        health={appHealthInfo.Health}
                        showReportButton={true}
                        onClickReportButton={async () => {
                            setSendingReport(true);
                            const thread = await createThread(getPropertyValue(resource?.properties?.resourceId));
                            setSendingReport(false);
                            navigate({
                                ...location,
                                pathname: thread?.id ? `/views/activities/threads/${thread.id}` : '/views/activities',
                            });
                        }}
                        isSendingReport={isSendingReport}
                    />
                </SummaryField>
                <SummaryField
                    label={intl.formatMessage(ResourceInfoResources.appHealthInfoTransactionCount)}
                    value={isNullOrUndefined(appHealthInfo.Transactions) ? '0' : appHealthInfo.Transactions.toString()}
                />
                <SummaryField
                    label={intl.formatMessage(ResourceInfoResources.appHealthInfoAverageLatency)}
                    value={
                        !isNullOrUndefined(appHealthInfo.AvgLatencyInMs)
                            ? `${(appHealthInfo.AvgLatencyInMs ?? 0) / 1000} seconds`
                            : undefined
                    }
                />
                <SummaryField
                    label={intl.formatMessage(ResourceInfoResources.appHealthInfoAverageMemoryUsage)}
                    value={
                        !isNullOrUndefined(appHealthInfo.AvgMemoryUsage)
                            ? getPropertyValue(resource?.properties?.resourceType) === 'k8s/apps/v1/deployments' ||
                              getPropertyValue(resource?.properties?.resourceType) === 'k8s/apps/v1/statefulsets'
                                ? `${appHealthInfo.AvgMemoryUsage}%`
                                : `${appHealthInfo.AvgMemoryUsage} bytes`
                            : undefined
                    }
                />
                <SummaryField
                    label={intl.formatMessage(ResourceInfoResources.appHealthInfoAverageCPUUsage)}
                    value={!isNullOrUndefined(appHealthInfo.AvgCpuUsage) ? `${appHealthInfo.AvgCpuUsage}%` : undefined}
                />
                <SummaryField
                    label={intl.formatMessage(ResourceInfoResources.appHealthInfoLastDataCaptureTime)}
                    value={
                        !isNullOrUndefined(appHealthInfo.LastDataCaptureTimeStampInUTC) && appHealthInfo.LastDataCaptureTimeStampInUTC
                            ? getSafeDateTime(appHealthInfo.LastDataCaptureTimeStampInUTC).toLocaleString(undefined, {
                                  year: '2-digit',
                                  month: 'numeric',
                                  day: 'numeric',
                                  hour: 'numeric',
                                  minute: 'numeric',
                                  second: 'numeric',
                              })
                            : undefined
                    }
                />
            </div>
        )
    );
});

const AnnotationDialogSurface = memo(
    ({
        initialRemarks,
        isUpdating,
        onSubmit,
    }: {
        initialRemarks: string;
        isUpdating: boolean;
        onSubmit: (remarks: string) => Promise<void>;
    }) => {
        const [remarks, setRemarks] = useState<string>('');

        const { textarea, textareaInner } = useStyles();

        const intl = useIntl();

        const isEditMode = useMemo(() => initialRemarks !== '', [initialRemarks]);

        useEffect(() => {
            setRemarks(initialRemarks);
        }, [initialRemarks]);

        return (
            <DialogSurface>
                <DialogBody>
                    <DialogTitle>
                        {isEditMode ? (
                            <FormattedMessage {...ResourceInfoResources.editAnnotation} />
                        ) : (
                            <FormattedMessage {...ResourceInfoResources.addAnnotation} />
                        )}
                    </DialogTitle>
                    <DialogContent>
                        <Text block style={{ marginBottom: 10 }}>
                            {intl.formatMessage(ResourceInfoResources.addAnnotationDescription)}
                        </Text>
                        <Textarea
                            textarea={{
                                className: textareaInner,
                            }}
                            disabled={isUpdating}
                            className={textarea}
                            placeholder={intl.formatMessage(SreAgentResources.enterADescription)}
                            value={remarks}
                            onChange={(_, data: TextareaOnChangeData) => {
                                setRemarks(data.value);
                            }}
                        />
                    </DialogContent>
                    <DialogActions>
                        <DialogTrigger>
                            <Button
                                appearance="primary"
                                disabled={remarks === initialRemarks || isUpdating}
                                onClick={async () => {
                                    onSubmit(remarks);
                                }}
                            >
                                <FormattedMessage {...SreAgentResources.save} />
                            </Button>
                        </DialogTrigger>
                        <DialogTrigger disableButtonEnhancement>
                            <Button appearance="secondary">
                                <FormattedMessage {...SreAgentResources.cancel} />
                            </Button>
                        </DialogTrigger>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        );
    }
);

const SummaryField = memo(({ label, value, children }: { label: string; value?: string; children?: ReactNode }) => {
    const { sectionField, sectionFieldText, sectionFieldValueText } = useStyles();

    return (
        (value || children) && (
            <div className={sectionField}>
                <Field
                    label={
                        <Label className={sectionFieldText}>
                            <Caption1>{label}</Caption1>
                        </Label>
                    }
                    orientation={'horizontal'}
                >
                    <div className={mergeClasses(sectionFieldText, sectionFieldValueText)}>{value ?? children}</div>
                </Field>
            </div>
        )
    );
});

AppHealthInfo.displayName = 'AppHealthInfo';
AnnotationDialogSurface.displayName = 'AnnotationDialogSurface';
SummaryField.displayName = 'SummaryField';

export default ResourceInfo;
