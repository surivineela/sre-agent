import { FC, useMemo } from 'react';
import { useLocation } from 'react-router';
import { TelemetrySource } from '../../Common/Constants/Telemetry';
import { AmplitudeContextProvider } from '../../Common/Contexts/AmplitudeContext';
import { parseResourceRoute } from '../../Common/Utilities/ResourceRouting';
import { AgentIFrame } from './AgentIFrame';
import { useAgentView } from './useAgentView';

interface AgentIFrameViewContentProps {
    agentId: string;
    sreLink?: string;
}

/** Inner component that uses the amplitude context */
const AgentIFrameViewContent: FC<AgentIFrameViewContentProps> = ({ agentId, sreLink }) => {
    const { agentUxUrl, agentUrl, isSiteRunning, iframeRef, iframeInitialized, errorBannerMessage, agentLoadError } = useAgentView(
        agentId,
        sreLink
    );

    return (
        <AgentIFrame
            agentUxUrl={agentUxUrl}
            agentUrl={agentUrl}
            isSiteRunning={isSiteRunning}
            iframeRef={iframeRef}
            iframeInitialized={iframeInitialized}
            errorBannerMessage={errorBannerMessage}
            agentLoadError={agentLoadError}
            resourceId={agentId}
        />
    );
};

export const AgentIFrameView = () => {
    const location = useLocation();

    /**
     * Resource ID and deep link extraction for Agent.Web iframe navigation
     *
     * With resource ID-based routing, the ARM resource ID is part of the URL path:
     * /agents/subscriptions/{sub}/resourceGroups/{rg}/providers/{ns}/{type}/{name}/views/thread/t-1
     *
     * The parseResourceRoute utility extracts:
     * - agentId: The full ARM resource ID
     * - sreLink: Everything after the resource ID (deep link for iframe navigation)
     *
     * Agent.Web uses hash-based routing (e.g., #/views/thread/123)
     * The sreLink is passed to the iframe as a URL hash via buildAgentUxUrl()
     *
     * Note: This only handles initial page load. Dynamic navigation after iframe load
     * is intentionally not implemented - users navigate within the iframe directly.
     */
    const { agentId, sreLink } = useMemo(() => {
        const parsed = parseResourceRoute(location.pathname, '/agents');

        if (!parsed) {
            return { agentId: '', sreLink: undefined };
        }

        // Combine deep link path with query string and hash
        const fullDeepLink = parsed.deepLink
            ? `${parsed.deepLink}${location.search}${location.hash}`
            : location.search || location.hash
              ? `${location.search}${location.hash}`.replace(/^\/+/, '')
              : undefined;

        return {
            agentId: parsed.resourceId,
            sreLink: fullDeepLink || undefined,
        };
    }, [location.pathname, location.search, location.hash]);

    return (
        <AmplitudeContextProvider resourceId={agentId ?? ''} telemetrySource={TelemetrySource.AgentIFrameView}>
            <AgentIFrameViewContent agentId={agentId ?? ''} sreLink={sreLink} />
        </AmplitudeContextProvider>
    );
};
