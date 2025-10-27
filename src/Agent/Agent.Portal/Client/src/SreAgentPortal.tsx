import { useEffect, useMemo } from 'react';
import { Helmet } from 'react-helmet-async';
import { useIntl } from 'react-intl';
import { createBrowserRouter, Navigate, Outlet, RouterProvider, useLocation } from 'react-router-dom';
import { TelemetrySource } from './Common/Constants/Telemetry';
import { useAuth } from './Common/Contexts/AuthContext';
import { useTelemetry } from './Common/Hooks/useTelemetry';
import { PortalResources } from './Strings/Resources';
import { AgentIFrameView } from './Views/Agent/AgentIFrameView';
import { HomeBrowseView } from './Views/Home/HomeBrowseView';
import { LandingPage } from './Views/LandingPage/LandingPage';
import { Navbar } from './Views/Navbar/Navbar';
import { NotificationToastContainer } from './Views/Notifications/NotificationToastContainer';

// Routing:
// - Landing page for signed-out users
// - Home for signed-in users (could also explore going to previously opened agent)

const PortalLayout = () => {
    const intl = useIntl();
    const { isAuthenticated, isLoading: isLoadingAuth } = useAuth();
    const location = useLocation();

    const siteTitle = useMemo(() => intl.formatMessage(PortalResources.azureSreAgents), [intl]);

    const shouldRedirectUnauthenticated = useMemo(
        () => !isAuthenticated && location.pathname !== '/welcome',
        [isAuthenticated, location.pathname]
    );
    const shouldRedirectAuthenticated = useMemo(
        () => isAuthenticated && location.pathname === '/welcome',
        [isAuthenticated, location.pathname]
    );

    return (
        <>
            <Helmet>
                <title>{siteTitle}</title>
                <meta name="description" content={siteTitle} />
            </Helmet>

            <NotificationToastContainer />

            <main style={{ display: 'flex', flexDirection: 'column', height: '100vh' }}>
                <Navbar />

                <div style={{ flex: 1, overflow: 'auto' }}>
                    {isLoadingAuth ? null : shouldRedirectUnauthenticated ? (
                        <Navigate to="/welcome" replace />
                    ) : shouldRedirectAuthenticated ? (
                        <Navigate to="/" replace />
                    ) : (
                        <Outlet />
                    )}
                </div>
            </main>
        </>
    );
};

const router = createBrowserRouter([
    {
        path: '/',
        element: <PortalLayout />,
        children: [
            { index: true, element: <HomeBrowseView /> },
            { path: 'welcome', element: <LandingPage /> },
            { path: 'agents/:agentId', element: <AgentIFrameView /> },
            { path: '*', element: <HomeBrowseView /> },
        ],
    },
]);

export const SreAgentPortal = () => {
    const { logEvent } = useTelemetry(TelemetrySource.PortalLayout, undefined);

    useEffect(() => {
        const logSiteVersion = () => {
            const version = import.meta.env.SRE_AGENT_PORTAL_VERSION;
            if (version) {
                console.log(`
                    ╔═════════════════════════════════════════════╗
                       🤖🌀 SRE Agent Portal Version: ${version}
                    ╚═════════════════════════════════════════════╝
                `);

                logEvent({
                    action: 'AgentSiteVersion',
                    actionModifier: 'info',
                    additionalData: { version },
                });
            }
        };

        // Log initial and every 60 minutes (for long-running sessions)
        logSiteVersion();
        const interval = setInterval(logSiteVersion, 60 * 60 * 1000);

        return () => clearInterval(interval);
    }, [logEvent]);

    return <RouterProvider router={router} />;
};
