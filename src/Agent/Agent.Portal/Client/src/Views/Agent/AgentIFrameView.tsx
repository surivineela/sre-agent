import { MessageBar, MessageBarBody } from '@fluentui/react-components';
import { useMemo } from 'react';
import { useLocation, useParams } from 'react-router-dom';
import { newShortGuid } from '../../Common/Utilities/Guid';
import MockShimmeredUx from './MockShimmeredUx';
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

    const { agentUxUrl, isSiteRunning, iframeRef, iframeInitialized, errorBannerMessage } = useAgentView(agentId ?? '', sreLink);

    const iframeId = useMemo(() => newShortGuid(), []);

    return (
        <div style={{ display: 'flex', flexDirection: 'column', flex: 1 }}>
            {errorBannerMessage && (
                <MessageBar intent="error" layout="multiline">
                    <MessageBarBody>{errorBannerMessage}</MessageBarBody>
                </MessageBar>
            )}

            {!iframeInitialized && <MockShimmeredUx />}

            {agentUxUrl && isSiteRunning && (
                <iframe
                    id={iframeId}
                    ref={iframeRef}
                    src={agentUxUrl}
                    allow="clipboard-write"
                    style={{
                        flex: 1,
                        width: '100%',
                        border: 'unset',
                    }}
                />
            )}
        </div>
    );
};
