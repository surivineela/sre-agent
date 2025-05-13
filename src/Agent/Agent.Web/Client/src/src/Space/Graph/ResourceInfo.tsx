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
import { memo, ReactNode, useContext, useEffect, useState } from 'react';
import { FaGithub } from 'react-icons/fa';
import { useLocation, useNavigate } from 'react-router';
import { getSafeDateTime } from '../../Common/Helpers/Date';
import { getAgentHeaders } from '../../Common/Helpers/headers';
import { GraphContext, GraphNode, ResourceExtended } from '../Contracts/Graph';
import { createThread, getPropertyValue, useResourceInfo } from '../Hooks/useResourceInfo';
import HealthStatus from './HealthStatus';
import { getAppHealthInfo } from './Utility';

const githubRepoRegex = /^https:\/\/github\.com\/[\w-]+\/[\w-]+\.git$/;

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
    section: {
        margin: '20px 0px',
    },
    sectionTitle: {
        lineHeight: '50px',
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

    const { root } = useStyles();

    const handleGitHubLogin = async () => {
        if (!selectedNode?.id) return;

        try {
            const response = await fetch(`../api/v1/github/auth?resourceId=${selectedNode.id}`, {
                headers: getAgentHeaders(),
            });
            if (!response.ok) throw new Error('Failed to get GitHub auth URL');

            const data = await response.json();
            if (data.loginCallbackUrl) {
                window.open(data.loginCallbackUrl, 'githubAuth', 'width=600,height=700');
            }
        } catch (err) {
            console.error('Failed to initiate GitHub login:', err);
        }
    };

    return (
        <div className={root}>
            <ResourceInfoContent selectedNode={selectedNode} onGitHubLogin={handleGitHubLogin} />
        </div>
    );
};

const ResourceInfoContent = ({ selectedNode }: { selectedNode?: GraphNode; onGitHubLogin: () => void }) => {
    const { isLoading, isUpdating, initialRemarks, resource, onSubmit, toasterId } = useResourceInfo(selectedNode);
    const styles = useStyles();
    const [isLinkDialogOpen, setIsLinkDialogOpen] = useState(false);
    const [repoUrl, setRepoUrl] = useState('');
    const [isLinking, setIsLinking] = useState(false);
    const [repoUrlError, setRepoUrlError] = useState('');

    const handleLinkRepository = async () => {
        if (!selectedNode?.id || !repoUrl) return;

        setIsLinking(true);
        try {
            const response = await fetch('../api/v1/github/link', {
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
    };

    const properties = resource?.properties;
    const { infoContent, title, spinner, content, dashboard } = styles;

    return selectedNode ? (
        <div className={infoContent}>
            <Text as="h2" size={600} weight={'semibold'} className={title}>
                {selectedNode?.name ?? ''}
            </Text>

            <div>
                {isLoading ? (
                    <Spinner size={'large'} className={spinner} />
                ) : (
                    <div className={content}>
                        <Section title={'Resource details'}>
                            <SummaryField label={'Name'} value={getPropertyValue(properties?.resourceName)} />
                            <SummaryField label={'Type'} value={getPropertyValue(properties?.resourceType)} />
                            <SummaryField label={'Resource group'} value={getPropertyValue(properties?.resourceGroupName)} />
                            <SummaryField label={'Subscription ID'} value={getPropertyValue(properties?.subscriptionId)} />
                            <AppHealthInfo resource={resource} />
                            <SummaryField label={'Dashboard URL'}>
                                {resource?.dashboardUrl ? (
                                    <div className={dashboard}>
                                        <Image src="./grafana-logo.svg" width={16} height={16} alt="Grafana logo" />
                                        <Link href={resource.dashboardUrl} target="_blank">
                                            View here
                                        </Link>
                                    </div>
                                ) : null}
                            </SummaryField>
                            <SummaryField label={'Repository Connection'}>
                                {resource?.sourceCodeLinkageStatus ? (
                                    resource.sourceCodeLinkageStatus.status === 'Linked' ? (
                                        <div className={styles.githubButton}>
                                            <FaGithub className={styles.githubIcon} />
                                            <Link href={resource.sourceCodeLinkageStatus.repositoryUrl} target="_blank">
                                                {resource.sourceCodeLinkageStatus.repositoryUrl}
                                            </Link>
                                        </div>
                                    ) : (
                                        <div>
                                            {resource.sourceCodeLinkageStatus.repositoryUrl && (
                                                <div className={styles.githubButton} style={{ marginBottom: '8px' }}>
                                                    <FaGithub className={styles.githubIcon} />
                                                    <Link href={resource.sourceCodeLinkageStatus.repositoryUrl} target="_blank">
                                                        {resource.sourceCodeLinkageStatus.repositoryUrl}
                                                    </Link>
                                                </div>
                                            )}
                                            <Button
                                                appearance="primary"
                                                size="small"
                                                icon={<FaGithub className={styles.githubIcon} />}
                                                onClick={() => {
                                                    const status = resource?.sourceCodeLinkageStatus;
                                                    if (status?.loginCallbackUrl) {
                                                        window.open(status.loginCallbackUrl, 'githubAuth', 'width=600,height=700');
                                                    }
                                                }}
                                            >
                                                Authorize Repository Access
                                            </Button>
                                        </div>
                                    )
                                ) : (
                                    <>
                                        <Button
                                            appearance="primary"
                                            size="small"
                                            icon={<FaGithub className={styles.githubIcon} />}
                                            onClick={() => setIsLinkDialogOpen(true)}
                                        >
                                            Connect Repository
                                        </Button>
                                        <Dialog open={isLinkDialogOpen} onOpenChange={(_, data) => setIsLinkDialogOpen(data.open)}>
                                            <DialogSurface>
                                                <DialogBody>
                                                    <DialogTitle>Link Repository to Resource</DialogTitle>
                                                    <DialogContent>
                                                        <Field
                                                            label="Repository URL"
                                                            validationState={repoUrlError ? 'error' : undefined}
                                                            validationMessage={repoUrlError}
                                                        >
                                                            <Textarea
                                                                placeholder="https://github.com/owner/repo-name.git"
                                                                value={repoUrl}
                                                                onChange={(_, data) => {
                                                                    setRepoUrl(data.value);
                                                                    if (!githubRepoRegex.test(data.value)) {
                                                                        setRepoUrlError(
                                                                            'Repository URL must be like: https://github.com/owner/repo-name.git'
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
                                                            {isLinking ? 'Connecting...' : 'Connect Repository'}
                                                        </Button>
                                                        <Button appearance="secondary" onClick={() => setIsLinkDialogOpen(false)}>
                                                            Cancel
                                                        </Button>
                                                    </DialogActions>
                                                </DialogBody>
                                            </DialogSurface>
                                        </Dialog>
                                    </>
                                )}
                            </SummaryField>
                            <SummaryField label={'Annotation'}>
                                {initialRemarks ? <div>{initialRemarks}</div> : null}
                                <Dialog>
                                    <DialogTrigger disableButtonEnhancement>
                                        <Link>{initialRemarks ? 'Edit annotation' : 'Add annotation'}</Link>
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
                        </Section>
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

    const appHealthInfo = getAppHealthInfo(resource);

    const [isSendingReport, setSendingReport] = useState(false);

    return (
        appHealthInfo && (
            <div>
                <SummaryField
                    label={'Costs for the past 7 days'}
                    value={
                        isNullOrUndefined(appHealthInfo.Costs) || appHealthInfo.Costs === 0
                            ? 'Cost calculation pending'
                            : `${appHealthInfo.Costs} USD`
                    }
                />
                <SummaryField
                    label={'Availability'}
                    value={!isNullOrUndefined(appHealthInfo.Availability) ? `${appHealthInfo.Availability ?? '0'}%` : undefined}
                />
                <SummaryField label={'Health'}>
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
                    label={'Number of transactions for the past 30 minutes'}
                    value={isNullOrUndefined(appHealthInfo.Transactions) ? '0' : appHealthInfo.Transactions.toString()}
                />
                <SummaryField
                    label={'Average latency'}
                    value={
                        !isNullOrUndefined(appHealthInfo.AvgLatencyInMs)
                            ? `${(appHealthInfo.AvgLatencyInMs ?? 0) / 1000} seconds`
                            : undefined
                    }
                />
                <SummaryField
                    label={'Average memory usage'}
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
                    label={'Average CPU usage'}
                    value={!isNullOrUndefined(appHealthInfo.AvgCpuUsage) ? `${appHealthInfo.AvgCpuUsage}%` : undefined}
                />
                <SummaryField
                    label={'Last data capture time'}
                    value={
                        !isNullOrUndefined(appHealthInfo.LastDataCaptureTimeStampInUTC) && appHealthInfo.LastDataCaptureTimeStampInUTC
                            ? getSafeDateTime(appHealthInfo.LastDataCaptureTimeStampInUTC).toLocaleString()
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

        useEffect(() => {
            setRemarks(initialRemarks);
        }, [initialRemarks]);

        return (
            <DialogSurface>
                <DialogBody>
                    <DialogTitle>{'Annotation'}</DialogTitle>
                    <DialogContent>
                        <Textarea
                            textarea={{
                                className: textareaInner,
                            }}
                            disabled={isUpdating}
                            className={textarea}
                            placeholder="Add annotations to your resource"
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
                                {'Save'}
                            </Button>
                        </DialogTrigger>
                        <DialogTrigger disableButtonEnhancement>
                            <Button appearance="secondary">{'Cancel'}</Button>
                        </DialogTrigger>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        );
    }
);

const Section = memo(({ title, children }: { title: string; children: JSX.Element | JSX.Element[] }) => {
    const { section, sectionTitle } = useStyles();

    return (
        <div className={section}>
            <Text weight={'semibold'} size={400} className={sectionTitle}>
                {title}
            </Text>
            {children}
        </div>
    );
});

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
Section.displayName = 'Section';
SummaryField.displayName = 'SummaryField';

export default ResourceInfo;
