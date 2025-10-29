import { useCallback, useEffect, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { getCloudEndpoints } from '../../Common/Auth/cloudConfig';
import { TelemetrySource } from '../../Common/Constants/Telemetry';
import { useAuth } from '../../Common/Contexts/AuthContext';
import { useNotifications } from '../../Common/Contexts/NotificationContext';
import { useUserPreferences } from '../../Common/Contexts/UserPreferencesContext';
import { ILogEvent, LogLevel } from '../../Common/Contracts/Telemetry';
import { AuthScopeIdentifier, useAuthTokenManager } from '../../Common/Hooks/useAuthTokenManager';
import { useTelemetry } from '../../Common/Hooks/useTelemetry';
import { buildBladeUrl, IOpenBladeInfo } from '../../Common/Utilities/Url';
import { PortalResources } from '../../Strings/Resources';
import {
    AgentSiteToAzPortalVerbs,
    AzPortalToAgentSiteVerbs,
    IEnvironmentInfo,
    IFrameTelemetryInfo,
    IFrameUserInfo,
    INotificationInfo,
    TokenTypes,
} from './AgentIFrameContracts';
import { resolveAgentSiteUrl } from './Utilities';

export const useAgentView = (resourceId: string, sreLink?: string) => {
    const intl = useIntl();
    const { user } = useAuth();
    const { logEvent } = useTelemetry(TelemetrySource.AgentIFrameView, resourceId);
    const { resolvedTheme, locale } = useUserPreferences();
    const { start, succeed, fail } = useNotifications();

    const iframeRef = useRef<HTMLIFrameElement>(null);

    const initTimeoutId = useRef<ReturnType<typeof setTimeout>>();
    const notificationIdMap = useRef<{ [iframeNotificationId: string]: string }>({});

    const [agentUxUrl, setAgentUxUrl] = useState<string>();
    const [uxOrigin, setUxOrigin] = useState<string>();
    const [agentUrl, setAgentUrl] = useState<string>('');
    const [isSiteRunning, setIsSiteRunning] = useState<boolean>(false);
    const [iframeInitialized, setIframeInitialized] = useState(false);
    const [errorBannerMessage, setErrorBannerMessage] = useState<string>('');

    const postMessage = useCallback(
        (verb: string, data: object) => {
            if (agentUxUrl && iframeRef.current?.contentWindow) {
                logEvent({
                    action: 'send-host-to-iframe',
                    actionModifier: verb,
                    additionalData: {
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
                logEvent({
                    action: 'send-host-to-iframe',
                    actionModifier: 'failed',
                    logLevel: LogLevel.Error,
                    additionalData: {
                        message: `[send-host-to-iframe] Failed to send message because agentUxUrl is not set or iframeRef contentWindow is not available`,
                        uxUrlDefined: !!agentUxUrl,
                        iframeRefDefined: !!iframeRef.current,
                        iframeRefContentWindowDefined: !!iframeRef.current?.contentWindow,
                    },
                });
            }
        },
        [logEvent, agentUxUrl]
    );

    const authTokenManager = useAuthTokenManager({
        telemetrySource: TelemetrySource.AgentIFrameView,
        resourceId,
        postMessage,
        initialTokenTypes: ['arm', 'sreAgent'],
    });

    const sendThemeCallback = useCallback(() => {
        const isDarkTheme = resolvedTheme === 'dark';
        postMessage(AzPortalToAgentSiteVerbs.sendTheme, {
            name: isDarkTheme ? 'dark' : 'light',
            mode: isDarkTheme ? 1 : 0,
        });
    }, [postMessage, resolvedTheme]);

    const sendUserInfoCallback = useCallback(() => {
        if (!user) return;

        const userInfo: IFrameUserInfo = {
            email: user.email,
            givenName: user.name,
            directoryId: user.tenantId,
            objectId: user.objectId, // Only actually use this one in agent site atm
        };

        postMessage(AzPortalToAgentSiteVerbs.sendUserInfo, { userInfo });
    }, [postMessage, user]);

    const readyForDataCallback = useCallback(() => {
        window.clearTimeout(initTimeoutId.current);

        const armUrl = new URL(getCloudEndpoints().arm);
        const environmentInfo: IEnvironmentInfo = {
            effectiveLocale: locale,
            resourceId,
            armEndpoint: armUrl.origin,
            sreAgentEndpoint: agentUrl || '',
        };

        postMessage(AzPortalToAgentSiteVerbs.sendEnvironmentInfo, environmentInfo);

        authTokenManager.handleInitialTokenSetup();

        sendThemeCallback();

        sendUserInfoCallback();

        setIframeInitialized(true);
    }, [postMessage, authTokenManager, resourceId, agentUrl, locale, sendThemeCallback, sendUserInfoCallback]);

    const logCallback = useCallback(
        (telemetryObj: IFrameTelemetryInfo) => {
            console.log('[SRE Agent iframe telemetry]', telemetryObj);

            const logLevelMap: Record<string, LogLevel> = {
                error: LogLevel.Error,
                warning: LogLevel.Warning,
                info: LogLevel.Info,
                verbose: LogLevel.Verbose,
            };

            const formattedTelemetryEvent: ILogEvent = {
                action: telemetryObj.action,
                actionModifier: telemetryObj.actionModifier,
                logLevel: telemetryObj.logLevel ? logLevelMap[telemetryObj.logLevel] : undefined,
                additionalData: typeof telemetryObj.data === 'string' ? { message: telemetryObj.data } : telemetryObj.data,
            };
            logEvent(formattedTelemetryEvent);
        },
        [logEvent]
    );

    const openBladeCallback = useCallback(
        (info: IOpenBladeInfo) => {
            const bladeUrl = buildBladeUrl(info);

            logEvent({
                action: 'open-blade',
                actionModifier: 'info',
                additionalData: {
                    extension: info.extension,
                    blade: info.detailBlade,
                },
            });

            window.open(bladeUrl, '_blank', 'noopener,noreferrer');
        },
        [logEvent]
    );

    const updateNotificationCallback = useCallback(
        (info: INotificationInfo) => {
            if (!info.id) {
                logEvent({
                    action: 'updateNotification',
                    actionModifier: 'failed',
                    logLevel: LogLevel.Error,
                    additionalData: {
                        message: 'Notification ID is required but not provided',
                    },
                });
                return;
            }

            if (info.state === 'start') {
                // Start a new notification and track the mapping
                const portalNotificationId = start(info.title, info.description);
                notificationIdMap.current[info.id] = portalNotificationId;
                return;
            }

            // Get the portal notification ID for this iframe notification
            const portalNotificationId = notificationIdMap.current[info.id];

            if (!portalNotificationId) {
                logEvent({
                    action: 'updateNotification',
                    actionModifier: 'failed',
                    logLevel: LogLevel.Error,
                    additionalData: {
                        message: 'Could not find cached notification ID',
                        iframeNotificationId: info.id,
                    },
                });
                return;
            }

            // Complete the notification based on state
            if (info.state === 'success') {
                succeed(portalNotificationId, info.title, info.description);
            } else {
                fail(portalNotificationId, info.title, info.description);
            }

            // Clean up the mapping
            delete notificationIdMap.current[info.id];
        },
        [logEvent, start, succeed, fail]
    );

    const requestTokenCallback = useCallback(
        (tokenType: TokenTypes) => {
            const tokenTypeToAuthScopeMap: Record<TokenTypes, AuthScopeIdentifier> = {
                arm: 'arm',
                sreagent: 'sreAgent',
                applicationinsightapi: 'appInsights',
            };

            authTokenManager.handleTokenRequest(tokenTypeToAuthScopeMap[tokenType]);
        },
        [authTokenManager]
    );

    const receiveMessage = useCallback(
        (event: MessageEvent) => {
            const messageCallbackMap: Record<string, (data: any) => void> = {
                [AgentSiteToAzPortalVerbs.readyForData]: readyForDataCallback,
                [AgentSiteToAzPortalVerbs.log]: logCallback,
                [AgentSiteToAzPortalVerbs.logAmplitudeControlEvent]: () => undefined,
                [AgentSiteToAzPortalVerbs.logAmplitudeNavigationEvent]: () => undefined,
                [AgentSiteToAzPortalVerbs.logAmplitudeOperationEvent]: () => undefined,
                [AgentSiteToAzPortalVerbs.openBlade]: openBladeCallback,
                [AgentSiteToAzPortalVerbs.updateNotification]: updateNotificationCallback,
                [AgentSiteToAzPortalVerbs.requestToken]: requestTokenCallback,
            };

            // Validate the origin and signature of the incoming message
            if (event.origin !== uxOrigin || event.data?.signature !== 'FxFrameBlade') {
                logEvent({
                    action: 'received-host-from-iframe',
                    actionModifier: 'invalid-origin-or-signature',
                    logLevel: LogLevel.Verbose,
                    additionalData: {
                        message: 'Invalid origin or signature for message',
                        origin: event.origin,
                        signature: event.data?.signature,
                    },
                });
                return;
            }

            const verb = event.data?.kind;

            logEvent({
                action: 'received-host-from-iframe',
                actionModifier: verb,
                logLevel: LogLevel.Verbose,
                additionalData: {
                    message: `[received-host-from-iframe] ${verb}`,
                },
            });

            const incomingData = event.data?.data || {};

            const callback = messageCallbackMap[verb];
            if (callback) {
                callback(incomingData);
            } else {
                logEvent({
                    action: 'received-host-from-iframe',
                    actionModifier: 'failure',
                    logLevel: LogLevel.Error,
                    additionalData: {
                        message: `Could not find callback for ${verb}`,
                    },
                });
            }
        },
        [uxOrigin, logEvent, readyForDataCallback, logCallback, openBladeCallback, updateNotificationCallback, requestTokenCallback]
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
                                setErrorBannerMessage(intl.formatMessage(PortalResources.whitelistErrorMessage));
                                logEvent({
                                    action: 'Subscription needs to be whitelisted - NoRegisteredProviderFound error',
                                    actionModifier: 'whitelist-error',
                                    logLevel: LogLevel.Warning,
                                    additionalData: {
                                        agentUxUrl,
                                    },
                                });
                                break;
                            }
                        } catch (_parseError) {
                            // Placeholder
                        }
                    }

                    // Check for iframe unauthorized error
                    if (response.status === 403) {
                        const serverHeader = response.headers.get('Server');
                        if (serverHeader?.includes('Zscaler')) {
                            logEvent({
                                action: 'iframe-unauthorized',
                                actionModifier: 'zscaler',
                                logLevel: LogLevel.Error,
                                additionalData: {
                                    message: `Zscaler detected for URL`,
                                    agentUxUrl,
                                },
                            });
                            setErrorBannerMessage(intl.formatMessage(PortalResources.iframeZscalerUnauthorizedMessage, { agentUxUrl }));
                            break;
                        } else {
                            logEvent({
                                action: 'iframe-unauthorized',
                                actionModifier: 'forbidden',
                                logLevel: LogLevel.Error,
                                additionalData: {
                                    message: `Forbidden access to URL ${agentUxUrl}`,
                                    agentUxUrl,
                                },
                            });
                            setErrorBannerMessage(intl.formatMessage(PortalResources.iframeUnauthorizedMessage, { agentUxUrl }));
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
                logEvent({
                    action: errorMessage,
                    actionModifier: 'timeout',
                    logLevel: LogLevel.Error,
                    additionalData: {
                        agentUxUrl,
                    },
                });
            }
        };

        pingSite();
    }, [intl, agentUxUrl, logEvent, resourceId]);

    useEffect(() => {
        if (agentUxUrl && isSiteRunning && iframeRef.current) {
            logEvent({
                action: 'received-message-handler',
                actionModifier: 'added',
                logLevel: LogLevel.Verbose,
            });

            console.log('adding event listener');
            window.addEventListener('message', receiveMessage);

            return () => {
                logEvent({
                    action: 'received-message-handler',
                    actionModifier: 'removed',
                    logLevel: LogLevel.Verbose,
                });

                console.log('removing eventlistener');
                window.removeEventListener('message', receiveMessage);
            };
        }
    }, [receiveMessage, agentUxUrl, isSiteRunning, logEvent]);

    useEffect(() => {
        sendThemeCallback();
    }, [sendThemeCallback]);

    useEffect(() => {
        return () => {
            // Clean up the timeout if the component unmounts
            // eslint-disable-next-line react-hooks/exhaustive-deps -- Need latest on unmount
            const currentInitTimeoutId = initTimeoutId.current;
            if (currentInitTimeoutId) {
                clearTimeout(currentInitTimeoutId);
            }

            // Clean up in-progress notifications by marking them as failed
            // eslint-disable-next-line react-hooks/exhaustive-deps -- Need latest on unmount
            Object.values(notificationIdMap.current).forEach(portalNotificationId => {
                fail(portalNotificationId, undefined, intl.formatMessage(PortalResources.operationCancelled));
            });
        };
    }, [fail, intl]);

    useEffect(() => {
        let subscribed = true;

        if (resourceId) {
            resolveAgentSiteUrl(resourceId, sreLink).then(resolvedUrl => {
                if (!subscribed) {
                    return;
                }
                setAgentUxUrl(resolvedUrl.uxUrl);
                setUxOrigin(resolvedUrl.uxOrigin);
                setAgentUrl(resolvedUrl.agentUrl);
            });
        }

        return () => {
            subscribed = false;
        };
    }, [resourceId, sreLink]);

    return {
        agentUxUrl,
        isSiteRunning,
        iframeRef,
        iframeInitialized,
        errorBannerMessage,
    };
};
