import { useEffect, useMemo } from 'react';
import { Helmet } from 'react-helmet-async';
import { useIntl } from 'react-intl';
import { createBrowserRouter, Outlet, RouterProvider, useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from './src/Common/Contexts/AuthContext';
import { PortalResources } from './src/Strings/Resources';
import { AgentIFrameView } from './src/Views/Agent/AgentIFrameView';
import { HomeView } from './src/Views/Home/HomeView';
import { LandingPage } from './src/Views/LandingPage/LandingPage';
import { Navbar } from './src/Views/Navbar';

// TODOs:
// - Authentication (Entra, Graph, ARM/Graph/SreAgent/AppInsights tokens)
// - UserStorage in localStorage - persist theme, locale, directory, subscription + filter
// - Navbar settings menu: Locale + theme infra (prereq: UserStorage in localStorage; dark mode as default)
// - Notifications + notification center (Fluent Toasts recommends having one)
// - Feature flag infra

// Routing:
// - Landing page for signed-out users
// - Home for signed-in users (could also explore going to previously opened agent)

const PortalLayout = () => {
    const intl = useIntl();
    const { status } = useAuth();
    const navigate = useNavigate();
    const location = useLocation();

    const siteTitle = useMemo(() => intl.formatMessage(PortalResources.azureSreAgents), [intl]);

    useEffect(() => {
        if (status === 'unauthenticated' && location.pathname !== '/welcome') {
            navigate('/welcome', { replace: true });
        }

        if (status === 'authenticated' && location.pathname === '/welcome') {
            navigate('/', { replace: true });
        }
    }, [status, location.pathname, navigate]);

    return (
        <>
            <Helmet>
                <title>{siteTitle}</title>
                <meta name="description" content={siteTitle} />
            </Helmet>

            <main style={{ display: 'flex', flexDirection: 'column', height: '100vh' }}>
                <Navbar />

                <div style={{ flex: 1, overflow: 'auto' }}>
                    <Outlet />
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
            { index: true, element: <HomeView /> },
            { path: 'welcome', element: <LandingPage /> },
            { path: 'agents/:agentId', element: <AgentIFrameView /> },
            { path: '*', element: <HomeView /> },
        ],
    },
]);

export const SreAgentPortal = () => {
    return <RouterProvider router={router} />;
};
