import { useRef } from 'react';

export const useAgentView = (resourceId: string, sreLink?: string) => {
    const iframeRef = useRef<HTMLIFrameElement>(null);

    console.log('useAgentView called with', resourceId, sreLink);

    return {
        agentUxUrl: 'https://eastus2-agent-1--d7e3ef0a.60d80d9b.eastus2.azuresre.ai/static/',
        isSiteRunning: true,
        iframeRef,
        iframeInitialized: true,
        errorBannerMessage: '',
    };
};

/*
// TODO: Lots of cleanup and portal-conversion

export interface PendingNotificationEntry {
    title: string;
    notification: PendingNotification;
}

export const useAgentView = (resourceId: string, sreLink?: string) => {
    const { logNavigationEvent, logOperationEvent, logControlEvent } = useAmplitudeTelemetry();
    const telemetry = useMemo(() => new Telemetry(TelemetrySource.AgentFrameBladeReactView, resourceId), [resourceId]);

    const iframeRef = useRef<HTMLIFrameElement>(null);

    const initTimeoutId = useRef<ReturnType<typeof setTimeout>>();
    const notificationMap = useRef<{ [key: string]: PendingNotificationEntry }>({}); // Use this to clean up notifications if user closes blade

    const initialEndpointsPromise = useRef(getEndpoints());
    const initialThemePromise = useRef(getTheme());
    const initialUserInfoPromise = useRef(getUserInfo());

    const [agentUxUrl, setAgentUxUrl] = useState<string>();
    const [uxOrigin, setUxOrigin] = useState<string>();
    const [agentUrl, setAgentUrl] = useState<string>('');
    const [isSiteRunning, setIsSiteRunning] = useState<boolean>(false);
    const [iframeInitialized, setIframeInitialized] = useState(false);
    const [errorBannerMessage, setErrorBannerMessage] = useState<string>('');

    const postMessage = useCallback(
        (verb: string, data: object) => {
            if (agentUxUrl && iframeRef.current?.contentWindow) {
                telemetry.log({
                    resourceId: resourceId,
                    action: 'send-host-to-iframe',
                    actionModifier: verb,
                    data: {
                        message: `[send-host-to-iframe] ${verb}`,
                    },
                });

                iframeRef.current.contentWindow.postMessage(
                    {
                        kind: verb,
                        data: data,
                        signature: 'FxFrameBlade',
                    },
                    agentUxUrl
                );
            } else {
                telemetry.log({
                    resourceId: resourceId,
                    action: 'send-host-to-iframe',
                    actionModifier: 'failed',
                    logLevel: LogLevel.Error,
                    data: {
                        message: `[send-host-to-iframe] Failed to send message because agentUxUrl is not set or iframeRef contentWindow is not available`,
                        uxUrlDefined: !!agentUxUrl,
                        iframeRefDefined: !!iframeRef.current,
                        iframeRefContentWindowDefined: !!iframeRef.current?.contentWindow,
                    },
                });
            }
        },
        [resourceId, telemetry, agentUxUrl]
    );

    const authTokenManager = useAuthTokenManager({
        telemetry,
        resourceId,
        postMessage,
        initialTokenTypes: ['arm', 'sreagent'],
    });

    const readyForDataCallback = useCallback(() => {
        window.clearTimeout(initTimeoutId.current);

        initialEndpointsPromise.current.then(endpoints => {
            const armUrl = new URL(endpoints.arm);
            const environmentInfo: IEnvironmentInfo = {
                effectiveLocale: displayLanguage,
                resourceId: resourceId,
                armEndpoint: armUrl.origin,
                sreAgentEndpoint: agentUrl || '',
            };

            postMessage(AzPortalToAgentSiteVerbs.sendEnvironmentInfo, environmentInfo);
        });

        authTokenManager.handleInitialTokenSetup();

        initialThemePromise.current.then(theme => {
            postMessage(AzPortalToAgentSiteVerbs.sendTheme, theme);
        });

        initialUserInfoPromise.current.then(userInfo => {
            postMessage(AzPortalToAgentSiteVerbs.sendUserInfo, { userInfo: userInfo });
        });

        setIframeInitialized(true);
        logOperationEvent({
            targetType: 'load',
            targetAction: 'loaded',
            targetName: 'agentSite',
            targetFriendlyName: 'Agent site',
        });
    }, [postMessage, authTokenManager, logOperationEvent, resourceId, agentUrl]);

    const logCallback = useCallback(
        (telemetryObj: TelemetryInfo) => {
            console.log('[SRE Agent iframe telemetry]', telemetryObj);
            telemetry.log(telemetryObj);
        },
        [telemetry]
    );

    const logAmplitudeOperationCallback = useCallback(
        (amplitudeEvent: AmplitudeOperationEvent & { errorInfo?: ILogBladeErrorInfo } & { metadata?: Record<string, unknown> }) => {
            const { errorInfo, metadata, ...actualAmplitudeEvent } = amplitudeEvent;
            logOperationEvent(actualAmplitudeEvent, errorInfo, metadata);
        },
        [logOperationEvent]
    );

    const logAmplitudeControlCallback = useCallback(
        (amplitudeEvent: AmplitudeControlEvent & { metadata?: Record<string, unknown> }) => {
            const { metadata, ...actualAmplitudeEvent } = amplitudeEvent;
            logControlEvent(actualAmplitudeEvent, metadata);
        },
        [logControlEvent]
    );

    const logAmplitudeNavigationCallback = useCallback(
        (amplitudeEvent: AmplitudeNavigationEvent & { metadata?: Record<string, unknown> }) => {
            const { metadata, ...actualAmplitudeEvent } = amplitudeEvent;
            logNavigationEvent(actualAmplitudeEvent, metadata);
        },
        [logNavigationEvent]
    );

    const openBladeCallback = useCallback(
        (info: IOpenBladeRequest) => {
            const bladeReference: BladeReference = {
                extensionName: info.extension,
                bladeName: info.detailBlade,
                parameters: info.detailBladeInputs,
                onClosed: (r: BladeClosedReason, data: unknown) => {
                    const dataToSend: IBladeClosedResult = {
                        operationId: info.operationId,
                        reason: r === BladeClosedReason.ChildClosedSelf ? 'childClosedSelf' : 'userNavigation',
                        data: data,
                    };

                    postMessage(AzPortalToAgentSiteVerbs.bladeClosed, dataToSend);
                },
            };

            if (info.asContextBlade) {
                openContextPane(bladeReference);
            } else {
                openBlade(bladeReference, { asSubJourney: !!info.asSubJourney });
            }
        },
        [postMessage]
    );

    const updateNotificationCallback = useCallback(
        (info: INotificationInfo) => {
            if (!info.id) {
                telemetry.log({
                    action: 'updateNotification',
                    actionModifier: 'failed',
                    resourceId: resourceId,
                    logLevel: LogLevel.Error,
                    data: {
                        message: 'Notification ID is required but not provided',
                    },
                });
                return;
            }

            if (info.state === 'start') {
                const notification = publishPendingNotification({
                    title: info.title,
                    description: info.description,
                });
                notificationMap.current[info.id] = {
                    notification,
                    title: info.title,
                };
                return;
            } else {
                const { notification, title } = notificationMap.current[info.id];

                if (!notification) {
                    telemetry.log({
                        action: 'updateNotification',
                        actionModifier: 'failed',
                        resourceId: resourceId,
                        logLevel: LogLevel.Error,
                        data: {
                            message: 'Could not find cached notification object',
                        },
                    });

                    return;
                }

                publishCompletePendingNotification(
                    notification,
                    {
                        title,
                        description: info.description,
                    },
                    info.state === 'success' ? NotificationStatus.Success : NotificationStatus.Error
                );

                delete notificationMap.current[info.id];
            }
        },
        [telemetry, resourceId]
    );

    const requestTokenCallback = useCallback(
        (tokenType: TokenTypes) => {
            authTokenManager.handleTokenRequest(tokenType);
        },
        [authTokenManager]
    );

    const receiveMessage = useCallback(
        (event: MessageEvent) => {
            const messageCallbackMap: Record<string, (data: unknown) => void> = {
                [AgentSiteToAzPortalVerbs.readyForData]: readyForDataCallback,
                [AgentSiteToAzPortalVerbs.log]: logCallback,
                [AgentSiteToAzPortalVerbs.logAmplitudeControlEvent]: logAmplitudeControlCallback,
                [AgentSiteToAzPortalVerbs.logAmplitudeNavigationEvent]: logAmplitudeNavigationCallback,
                [AgentSiteToAzPortalVerbs.logAmplitudeOperationEvent]: logAmplitudeOperationCallback,
                [AgentSiteToAzPortalVerbs.openBlade]: openBladeCallback,
                [AgentSiteToAzPortalVerbs.updateNotification]: updateNotificationCallback,
                [AgentSiteToAzPortalVerbs.requestToken]: requestTokenCallback,
            };

            // Validate the origin and signature of the incoming message
            if (event.origin !== uxOrigin || event.data?.signature !== 'FxFrameBlade') {
                telemetry.log({
                    action: 'received-host-from-iframe',
                    actionModifier: 'invalid-origin-or-signature',
                    logLevel: LogLevel.Verbose,
                    data: {
                        message: 'Invalid origin or signature for message',
                        origin: event.origin,
                        signature: event.data?.signature,
                    },
                });
                return;
            }

            const verb = event.data?.kind;

            telemetry.log({
                resourceId: resourceId,
                action: 'received-host-from-iframe',
                actionModifier: verb,
                logLevel: LogLevel.Verbose,
                data: {
                    message: `[received-host-from-iframe] ${verb}`,
                },
            });

            const incomingData = event.data?.data || {};

            const callback = messageCallbackMap[verb];
            if (callback) {
                callback(incomingData);
            } else {
                telemetry.log({
                    action: 'received-host-from-iframe',
                    actionModifier: 'failure',
                    logLevel: LogLevel.Error,
                    data: `Could not find callback for ${verb}`,
                });
            }
        },
        [
            uxOrigin,
            telemetry,
            resourceId,
            readyForDataCallback,
            logCallback,
            openBladeCallback,
            updateNotificationCallback,
            requestTokenCallback,
            logAmplitudeControlCallback,
            logAmplitudeNavigationCallback,
            logAmplitudeOperationCallback,
        ]
    );

    useEffect(() => {
        // We need to ping the site because if the site is cold-starting and fails for some reason then the iframe will hang
        const pingSite = async () => {
            if (!agentUxUrl) {
                return;
            }

            const agentSitePingLimit = 60; // Timeout after 60 seconds
            const agentSitePingSleep = 1000;
            let pingIndex = 0;
            const startTime = Date.now();

            for (pingIndex; pingIndex < agentSitePingLimit; pingIndex++) {
                try {
                    const response = await fetch(agentUxUrl as string, { method: 'GET', mode: 'cors' });
                    if (response.ok) {
                        setIsSiteRunning(true);
                        break;
                    }

                    // Check for NoRegisteredProviderFound error on first ping
                    if (pingIndex === 0 && !response.ok) {
                        try {
                            const errorJson = await response.json();
                            if (errorJson?.error?.code === 'NoRegisteredProviderFound') {
                                setErrorBannerMessage(SreAgentResources.whitelistErrorMessage);
                                log([
                                    {
                                        area: TelemetrySource.AgentFrameBladeReactView,
                                        args: [agentUxUrl],
                                        level: LogEntryLevel.Warning,
                                        message: `Subscription needs to be whitelisted - NoRegisteredProviderFound error`,
                                        timestamp: Date.now(),
                                    },
                                ]);
                                break;
                            }
                        } catch (_parseError) {
                            // Placeholder
                        }
                    }

                    //Check for iframe unauthorized error
                    if (response.status === 403) {
                        const serverHeader = response.headers.get('Server');
                        if (serverHeader?.includes('Zscaler')) {
                            telemetry.log({
                                action: 'iframe-unauthorized',
                                actionModifier: 'zscaler',
                                logLevel: LogLevel.Error,
                                data: {
                                    message: `Zscaler detected for URL`,
                                    agentUxUrl,
                                },
                            });
                            setErrorBannerMessage(format(SreAgentResources.iframeZscalerUnauthorizedMessage, agentUxUrl));
                            break;
                        } else {
                            telemetry.log({
                                action: 'iframe-unauthorized',
                                actionModifier: 'forbidden',
                                logLevel: LogLevel.Error,
                                data: {
                                    message: `Forbidden access to URL ${agentUxUrl}`,
                                    agentUxUrl,
                                },
                            });
                            setErrorBannerMessage(format(SreAgentResources.iframeUnauthorizedMessage, agentUxUrl));
                            break;
                        }
                    }
                } catch (error) {
                    // Placeholder
                    console.log(error);
                }

                await new Promise(r => setTimeout(r, agentSitePingSleep));
            }

            const totalTime = Date.now() - startTime;
            if (pingIndex >= agentSitePingLimit || totalTime >= 60000) {
                const errorMessage =
                    pingIndex >= agentSitePingLimit
                        ? `Agent site failed to respond within ${agentSitePingLimit} pings. Blade timeout.`
                        : `Agent site failed to respond (${totalTime}ms). Blade timeout.`;
                log([
                    {
                        area: TelemetrySource.AgentFrameBladeReactView,
                        args: [agentUxUrl],
                        level: LogEntryLevel.Error,
                        message: errorMessage,
                        timestamp: Date.now(),
                    },
                ]);
            }
        };

        pingSite();
    }, [agentUxUrl, telemetry, resourceId]);

    useEffect(() => {
        if (agentUxUrl && isSiteRunning && iframeRef.current) {
            telemetry.log({
                action: 'received-message-handler',
                actionModifier: 'added',
                logLevel: LogLevel.Verbose,
            });

            console.log('adding event listener');
            window.addEventListener('message', receiveMessage);

            return () => {
                telemetry.log({
                    action: 'received-message-handler',
                    actionModifier: 'removed',
                    logLevel: LogLevel.Verbose,
                });

                console.log('removing eventlistener');
                window.removeEventListener('message', receiveMessage);
            };
        }
    }, [receiveMessage, agentUxUrl, isSiteRunning, telemetry]);

    useEffect(() => {
        setOnThemeChange(theme => {
            postMessage(AzPortalToAgentSiteVerbs.sendTheme, theme);
        });
    }, [postMessage]);

    useEffect(() => {
        return () => {
            // Clean up the timeout if the component unmounts
            // eslint-disable-next-line react-hooks/exhaustive-deps -- Need latest on unmount
            const currentInitTimeoutId = initTimeoutId.current;
            if (currentInitTimeoutId) {
                clearTimeout(currentInitTimeoutId);
            }

            // Clean up notifications
            // eslint-disable-next-line react-hooks/exhaustive-deps
            Object.values(notificationMap.current).forEach(notificationEntry => {
                notificationEntry.notification.complete({
                    status: NotificationStatus.Warning,
                    description: SreAgentResources.operationCancelled,
                });
            });
        };
    }, []);

    useEffect(() => {
        const armId = ArmId.parse(resourceId);
        setTitle({
            title: armId.resourceName,
            subtitle: SreAgentResources.sreAgent,
        });
    }, [resourceId]);

    useEffect(() => {
        let subscribed = true;

        if (resourceId) {
            resolveAgentSiteUrl(resourceId, sreLink).then(resolvedUrl => {
                if (!subscribed) {
                    return;
                }
                setAgentUxUrl(resolvedUrl.agentUxUrl);
                setUxOrigin(resolvedUrl.uxOrigin);
                setAgentUrl(resolvedUrl.agentUrl);
            });
        }

        return () => {
            subscribed = false;
        };
    }, [resourceId, sreLink]);

    useEffect(() => {
        // Logging nav event from link here so it has the resource info attached
        logNavigationEvent({
            targetType: 'link',
            targetAction: 'openBlade',
            targetName: 'AgentFrameBlade.ReactView',
            targetFriendlyName: 'Agent name link',
        });
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);

    return {
        agentUxUrl,
        isSiteRunning,
        iframeRef,
        iframeInitialized,
        errorBannerMessage,
    };
};
*/
