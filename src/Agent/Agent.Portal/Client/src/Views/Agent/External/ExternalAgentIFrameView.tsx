import { useMemo } from 'react';
import { useLocation, useParams } from 'react-router-dom';
import { AgentIFrame } from '../AgentIFrame';
import { useExternalAgentView } from './useExternalAgentView';

export const ExternalAgentIFrameView = () => {
    const { agentName: encodedAgentName, agentUri: encodedAgentUri } = useParams<{ agentName: string; agentUri: string }>();
    const location = useLocation();

    const agentUri = useMemo(() => decodeURIComponent(encodedAgentUri ?? ''), [encodedAgentUri]);

    /**
     * Deep link extraction for external Agent.Web iframe navigation
     *
     * Agent.Web uses hash-based routing (e.g., #/views/activities/threads/123)
     * This extracts everything after /externalagents/{agentName}/{agentUri} and passes it to the iframe
     * as a URL hash parameter via buildAgentUxUrl()
     *
     * Example flow:
     * - Portal URL: /externalagents/ContosoAgent/https%3A%2F%2Fagent.contoso.com/views/activities/threads/t-1
     * - Extracted sreLink: "views/activities/threads/t-1"
     * - Iframe URL: https://agent.contoso.com/static/?trustedAuthority=...#/views/activities/threads/t-1
     *
     * Note: This only handles initial page load. Dynamic navigation after iframe load
     * is intentionally not implemented - users navigate within the iframe directly.
     */
    const sreLink = useMemo(() => {
        if (!encodedAgentName || !encodedAgentUri) {
            return undefined;
        }

        const baseSegment = `/externalagents/${encodedAgentName}/${encodedAgentUri}`;

        if (!location.pathname.startsWith(baseSegment)) {
            return undefined;
        }

        // Extract everything after /externalagents/{encodedAgentName}/{encodedAgentUri}
        const pathAfterAgent = location.pathname.slice(baseSegment.length).replace(/^\/+/, '');
        const fullDeepLink = `${pathAfterAgent}${location.search}${location.hash}`;

        return fullDeepLink || undefined;
    }, [encodedAgentName, encodedAgentUri, location.pathname, location.search, location.hash]);

    const { agentUxUrl, agentUrl, isSiteRunning, iframeRef, iframeInitialized, errorBannerMessage, agentLoadError } = useExternalAgentView(
        agentUri,
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
        />
    );
};
