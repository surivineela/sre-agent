import { MessageBar, MessageBarBody } from '@fluentui/react-components';
import { RefObject, useMemo } from 'react';
import { newShortGuid } from '../../Common/Utilities/Guid';
import MockShimmeredUx from './MockShimmeredUx';

interface AgentIFrameProps {
    agentUxUrl?: string;
    isSiteRunning: boolean;
    iframeRef: RefObject<HTMLIFrameElement>;
    iframeInitialized: boolean;
    errorBannerMessage: string;
}

export const AgentIFrame = ({ agentUxUrl, isSiteRunning, iframeRef, iframeInitialized, errorBannerMessage }: AgentIFrameProps) => {
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
