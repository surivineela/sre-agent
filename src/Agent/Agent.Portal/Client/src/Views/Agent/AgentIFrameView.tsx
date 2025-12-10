import { useMemo } from 'react';
import { useLocation, useParams } from 'react-router-dom';
import { AgentIFrame } from './AgentIFrame';
import { useAgentView } from './useAgentView';

export const AgentIFrameView = () => {
    const { agentId: encodedAgentId } = useParams<{ agentId: string }>();
    const location = useLocation();

    const agentId = useMemo(() => decodeURIComponent(encodedAgentId ?? ''), [encodedAgentId]);

    /**
     * Deep link extraction for Agent.Web iframe navigation
     *
     * Agent.Web uses hash-based routing (e.g., #/views/activities/threads/123)
     * This extracts everything after /agents/{agentId} and passes it to the iframe
     * as a URL hash parameter via buildAgentUxUrl()
     *
     * Example flow:
     * - Portal URL: /agents/subscriptions%2F...%2Fagent/views/activities/threads/t-1
     * - Extracted sreLink: "views/activities/threads/t-1"
     * - Iframe URL: https://agent-site/static/?trustedAuthority=...#/views/activities/threads/t-1
     *
     * Note: This only handles initial page load. Dynamic navigation after iframe load
     * is intentionally not implemented - users navigate within the iframe directly.
     */
    const sreLink = useMemo(() => {
        if (!encodedAgentId) {
            return undefined;
        }

        const baseSegment = `/agents/${encodedAgentId}`;

        if (!location.pathname.startsWith(baseSegment)) {
            return undefined;
        }

        // Extract everything after /agents/{encodedAgentId}
        const pathAfterAgent = location.pathname.slice(baseSegment.length).replace(/^\/+/, '');
        const fullDeepLink = `${pathAfterAgent}${location.search}${location.hash}`;

        return fullDeepLink || undefined;
    }, [encodedAgentId, location.pathname, location.search, location.hash]);

    const { agentUxUrl, agentUrl, isSiteRunning, iframeRef, iframeInitialized, errorBannerMessage, agentLoadError } = useAgentView(agentId ?? '', sreLink);

    return (
        <AgentIFrame
            agentUxUrl={agentUxUrl}
            agentUrl={agentUrl}
            isSiteRunning={isSiteRunning}
            iframeRef={iframeRef}
            iframeInitialized={iframeInitialized}
            errorBannerMessage={errorBannerMessage}
            agentLoadError={agentLoadError}
            resourceId={agentId ?? ''}
        />
    );
};
