import { tokens } from '@fluentui/react-components';
import { useEffect, useMemo } from 'react';
import { Helmet } from 'react-helmet-async';
import { useIntl } from 'react-intl';
import { createBrowserRouter, Navigate, Outlet, RouterProvider, useLocation } from 'react-router-dom';
import { RouteErrorBoundary } from './Common/Components/RouteErrorBoundary';
import { TelemetrySource } from './Common/Constants/Telemetry';
import { useAuth } from './Common/Contexts/AuthContext';
import { useIsInternal } from './Common/Hooks/useIsInternal';
import { useTelemetry } from './Common/Hooks/useTelemetry';
import { PortalResources } from './Strings/Resources';
import { AgentIFrameView } from './Views/Agent/AgentIFrameView';
import { ExternalAgentIFrameView } from './Views/Agent/External/ExternalAgentIFrameView';
import { HomeBrowseView } from './Views/Home/HomeBrowseView';
import { LandingPage } from './Views/LandingPage/LandingPage';
import { Navbar } from './Views/Navbar/Navbar';
import { NotificationToastContainer } from './Views/Notifications/NotificationToastContainer';

// Routing:
// - Landing page for signed-out users
// - Home for signed-in users (could also explore going to previously opened agent)

const PortalLayout = () => {
    const intl = useIntl();
    const { user, isAuthenticated, isLoading: isLoadingAuth } = useAuth();
    const location = useLocation();
    const { logEvent } = useTelemetry(TelemetrySource.PortalLayout, undefined);

    const siteTitle = useMemo(() => intl.formatMessage(PortalResources.azureSreAgents), [intl]);

    const shouldRedirectUnauthenticated = useMemo(
        () => !isAuthenticated && location.pathname !== '/welcome',
        [isAuthenticated, location.pathname]
    );
    const shouldRedirectAuthenticated = useMemo(
        () => isAuthenticated && location.pathname === '/welcome',
        [isAuthenticated, location.pathname]
    );

    // Log route/view changes
    useEffect(() => {
        logEvent({
            action: 'route-navigation',
            actionModifier: 'view',
            additionalData: {
                hostname: window.location.hostname,
                pathname: location.pathname,
                referrer: document.referrer,
                isAuthenticated,
                userTenantId: user?.tenantId,
            },
        });
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [location, logEvent]);

    return (
        <>
            <Helmet>
                <title>{siteTitle}</title>
                <meta name="description" content={siteTitle} />
            </Helmet>

            <NotificationToastContainer />

            <main style={{ display: 'flex', flexDirection: 'column', height: '100vh', backgroundColor: tokens.colorNeutralBackground2 }}>
                <Navbar />

                <div style={{ display: 'flex', flexDirection: 'column', flex: 'auto', overflow: 'auto' }}>
                    {isLoadingAuth ? null : shouldRedirectUnauthenticated ? (
                        <Navigate to={`/welcome${location.search}`} replace />
                    ) : shouldRedirectAuthenticated ? (
                        <Navigate to={`/${location.search}`} replace />
                    ) : (
                        <Outlet />
                    )}
                </div>
            </main>
        </>
    );
};

const router = createBrowserRouter(
    [
        {
            path: '/',
            element: <PortalLayout />,
            errorElement: <RouteErrorBoundary />,
            children: [
                { index: true, element: <HomeBrowseView /> },
                { path: 'welcome', element: <LandingPage /> },
                { path: 'agents/:agentId', element: <AgentIFrameView /> },
                { path: 'externalagents/:agentName/:agentUri', element: <ExternalAgentIFrameView /> },
                { path: '*', element: <HomeBrowseView /> },
            ],
        },
    ],
    {
        basename: import.meta.env.BASE_URL || '/',
    }
);

export const SreAgentPortal = () => {
    const { logEvent } = useTelemetry(TelemetrySource.PortalLayout, undefined);
    const { isInternalDevTenant, isInternalTenant, isInternalProdTenant } = useIsInternal();

    useEffect(() => {
        const logSiteVersion = () => {
            const version = import.meta.env.SRE_AGENT_PORTAL_VERSION || '(Local dev build)';
            console.log(`
                    ╔═════════════════════════════════════════════╗
                       🤖🌀 SRE Agent Portal Version: ${version}
                       ${isInternalDevTenant ? 'Dev tenant' : isInternalTenant ? 'MSFT tenant' : isInternalProdTenant ? 'MSFT prod tenant' : ''}
                    ╚═════════════════════════════════════════════╝
                `);

            logEvent({
                action: 'AgentPortalVersion',
                actionModifier: 'info',
                additionalData: { version },
            });
        };

        // Log initial and every 60 minutes (for long-running sessions)
        logSiteVersion();
        const interval = setInterval(logSiteVersion, 60 * 60 * 1000);

        return () => clearInterval(interval);
    }, [logEvent, isInternalDevTenant, isInternalTenant, isInternalProdTenant]);

    return <RouterProvider router={router} />;
};
