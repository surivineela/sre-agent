import { MessageBar, MessageBarBody } from '@fluentui/react-components';
import { RefObject, useMemo } from 'react';
import { newShortGuid } from '../../Common/Utilities/Guid';
import AgentLoadArmError from './ErrorStates/AgentLoadArmError';
import AgentLoadTimeoutError from './ErrorStates/AgentLoadTimeoutError';
import MockShimmeredUx from './MockShimmeredUx';
import { AgentLoadError } from './Utilities';

interface AgentIFrameProps {
    agentUxUrl?: string;
    agentUrl?: string;
    isSiteRunning: boolean;
    iframeRef: RefObject<HTMLIFrameElement>;
    iframeInitialized: boolean;
    errorBannerMessage: string;
    agentLoadError?: AgentLoadError;
    resourceId?: string;
}

export const AgentIFrame = ({
    agentUxUrl,
    agentUrl,
    isSiteRunning,
    iframeRef,
    iframeInitialized,
    errorBannerMessage,
    agentLoadError,
    resourceId,
}: AgentIFrameProps) => {
    const iframeId = useMemo(() => newShortGuid(), []);

    if (agentLoadError?.type === 'notFound' || agentLoadError?.type === 'accessDenied' || agentLoadError?.type === 'unknown') {
        return <AgentLoadArmError agentLoadError={agentLoadError} resourceId={resourceId ?? ''} />;
    }

    if (agentLoadError?.type === 'timeout') {
        return <AgentLoadTimeoutError resourceId={resourceId} agentSiteUrl={agentUrl} />;
    }

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
                    allow="clipboard-write; local-network-access"
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
