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

    // TODO: Figure out and refine deep link logic
    const sreLink = useMemo(() => {
        if (!agentId) {
            return undefined;
        }

        const baseSegment = `/agents/${agentId}`;
        let remainder = location.pathname.startsWith(baseSegment) ? location.pathname.slice(baseSegment.length) : '';
        remainder = remainder.replace(/^\/+/, '');

        const suffix = `${remainder}${location.search}${location.hash}`;
        return suffix.length > 0 ? suffix : undefined;
    }, [agentId, location.hash, location.pathname, location.search]);

    const { agentUxUrl, isSiteRunning, iframeRef, iframeInitialized, errorBannerMessage } = useAgentView(agentId ?? '', sreLink);

    const iframeId = useMemo(() => newShortGuid(), []);

    return (
        <div>
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
                        position: 'absolute',
                        top: errorBannerMessage ? '60px' : '0',
                        left: 0,
                        height: errorBannerMessage ? 'calc(100% - 60px)' : '100%',
                        width: '100%',
                        border: 'unset',
                    }}
                />
            )}
        </div>
    );
};
