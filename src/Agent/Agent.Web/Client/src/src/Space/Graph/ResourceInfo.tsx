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
import { memo, ReactNode, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { FormattedMessage, useIntl } from 'react-intl';
import { useLocation, useNavigate } from 'react-router-dom';
import { StreamingMessage } from '../../Common/Contracts/DataPlane/Streaming';
import { getSafeDateTime } from '../../Common/Helpers/Date';
import { Guid } from '../../Common/Helpers/Guid';
import { isPaasResourceType } from '../../Common/Helpers/Resources';
import { ResourceInfoResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { StreamingContext } from '../Contracts/Context';
import { GraphContext, GraphNode, ResourceExtended } from '../Contracts/Graph';
import { useAuthenticatedUserInfo } from '../Hooks/useAuthenticatedUserInfo';
import { getPropertyValue, useResourceInfo } from '../Hooks/useResourceInfo';
import HealthStatus from './HealthStatus';
import { ConnectRepositoryLink, getRepoIcon } from './RepositoryConnectionDialog';
import { getAppHealthInfo } from './Utility';

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
    repoButton: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
    },
    repoIcon: {
        marginRight: '4px',
        width: '50px',
        height: '50px',
    },
});

const ResourceInfo = () => {
    const { selectedNode } = useContext(GraphContext);

    const { root } = useStyles();

    return (
        <div className={root}>
            <ResourceInfoContent selectedNode={selectedNode} />
        </div>
    );
};

const ResourceInfoContent = ({ selectedNode }: { selectedNode?: GraphNode }) => {
    const { isLoading, isUpdating, initialRemarks, resource, onSubmit, toasterId } = useResourceInfo(selectedNode);
    const { infoContent, title, spinner, content, dashboard, repoButton } = useStyles();
    const intl = useIntl();

    const properties = resource?.properties;

    const isPaasResource = useMemo<boolean>(() => isPaasResourceType(resource?.type), [resource]);

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
                        {resource?.type?.toLowerCase() === 'microsoft.apimanagement/service/backends' && (
                            <>
                                <SummaryField
                                    label={intl.formatMessage(ResourceInfoResources.apimBackendEndpoint)}
                                    value={getPropertyValue(properties?.apimBackendEndpoint)}
                                />
                                <SummaryField
                                    label={intl.formatMessage(ResourceInfoResources.armResourceId)}
                                    value={getPropertyValue(properties?.armResourceId)}
                                />
                                <SummaryField
                                    label={intl.formatMessage(ResourceInfoResources.connectedApis)}
                                    value={getPropertyValue(properties?.connectedApis)}
                                />
                            </>
                        )}
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
                                        <div className={repoButton}>
                                            {getRepoIcon(resource.sourceCodeLinkageStatus.repositoryUrl)}
                                            <Link href={resource.sourceCodeLinkageStatus.repositoryUrl} target="_blank">
                                                {resource.sourceCodeLinkageStatus.repositoryUrl}
                                            </Link>
                                        </div>
                                    ) : (
                                        <div>
                                            {resource.sourceCodeLinkageStatus.repositoryUrl && (
                                                <div className={repoButton} style={{ marginBottom: '8px' }}>
                                                    {getRepoIcon(resource.sourceCodeLinkageStatus.repositoryUrl)}
                                                    <Link href={resource.sourceCodeLinkageStatus.repositoryUrl} target="_blank">
                                                        {resource.sourceCodeLinkageStatus.repositoryUrl}
                                                    </Link>
                                                </div>
                                            )}
                                            <Button
                                                appearance="primary"
                                                size="small"
                                                icon={getRepoIcon(resource.sourceCodeLinkageStatus.repositoryUrl)}
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
                                    <ConnectRepositoryLink resourceId={selectedNode?.id} />
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
    const { subscribeThreadUpdateEvent, startMessageStreamingOnNewThread } = useContext(StreamingContext);
    const {
        userIdAndDisplayName: { userId, displayName },
    } = useAuthenticatedUserInfo();

    const [isSendingReport, setSendingReport] = useState(false);
    const newThreadId = useRef<string | null>(null);

    const appHealthInfo = getAppHealthInfo(resource);

    useEffect(() => {
        const unsubscribe = subscribeThreadUpdateEvent((message: StreamingMessage) => {
            const threadId = message?.additionalProperties?.threadId;
            if (threadId && newThreadId.current && threadId === newThreadId.current) {
                const currentThreadId = newThreadId.current;
                newThreadId.current = null;
                setSendingReport(false);
                navigate({
                    ...location,
                    pathname: `/views/activities/threads/${currentThreadId}`,
                });
            }
        });

        return () => {
            unsubscribe();
        };
    }, [subscribeThreadUpdateEvent, navigate, location]);

    return (
        appHealthInfo && (
            <div>
                <SummaryField
                    label={intl.formatMessage(ResourceInfoResources.appHealthInfoAvailability)}
                    value={!isNullOrUndefined(appHealthInfo.Availability) ? `${appHealthInfo.Availability ?? '0'}%` : undefined}
                />
                <SummaryField label={intl.formatMessage(ResourceInfoResources.appHealthInfoHealthStatus)}>
                    <HealthStatus
                        health={appHealthInfo.Health}
                        showReportButton={true}
                        onClickReportButton={async () => {
                            newThreadId.current = Guid.newGuid();
                            setSendingReport(true);
                            startMessageStreamingOnNewThread(newThreadId.current, {
                                startMessage: {
                                    text: `Resource ${getPropertyValue(resource?.properties?.resourceId)} is unhealthy could you help diagnose what is wrong?`,
                                    userId,
                                    displayName,
                                },
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
                            ? (() => {
                                  const resourceType = getPropertyValue(resource?.properties?.resourceType).toLowerCase();
                                  return [
                                      'k8s/apps/v1/deployments',
                                      'k8s/apps/v1/statefulsets',
                                      'microsoft.apimanagement/service',
                                  ].includes(resourceType)
                                      ? `${appHealthInfo.AvgMemoryUsage}%`
                                      : `${appHealthInfo.AvgMemoryUsage} bytes`;
                              })()
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
