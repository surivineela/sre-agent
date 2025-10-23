import { memo, useContext, useMemo, useRef, useState } from 'react';
import AzPortalProxy from '../../Common/AzPortalProxy/AzPortalProxy';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import Url from '../../Common/Helpers/Url';
import { IThreadContentProps } from '../Contracts/Activities';
import { AgentContext, SreAgentContext } from '../Contracts/Context';
import { TracePanel } from '../Foundry/app/components/shell/playground/tracing/TracePanel';
import { useTodoPlanDrawer } from '../Hooks/useTodoPlanDrawer';
import { ThreadContentStyles } from '../Styles/Activities.styles';
import ChatBox from './ChatBox';
import ThreadContentTitle from './ThreadContentTitle';

const inStandaloneMode = AzPortalProxy.inStandaloneMode;
const showThreadTraceUI = Url.getFeatureValue('showThreadTraceUI') === 'true';

export const ThreadContent = memo(({ thread, addThread, deleteThread, updateThreadLastReadTime }: IThreadContentProps) => {
    const { threadContentAndActionKey, activeThreadId, setMenuCollapsed } = useContext(AgentContext);

    const todoPlanDrawer = useTodoPlanDrawer(activeThreadId, setMenuCollapsed, false);

    const [showTrace, setShowTrace] = useState(false);
    const { isCrossTenantPortalMode } = useContext(EnvironmentContext);
    const showControlPlaneDependentFeatures = useMemo(() => !inStandaloneMode && !isCrossTenantPortalMode, [isCrossTenantPortalMode]);

    const { agentObj } = useContext(SreAgentContext);
    const agentAppInsightsAppId = useMemo<string | undefined>(
        () => agentObj?.properties?.logConfiguration?.applicationInsightsConfiguration?.appId,
        [agentObj]
    );
    const traceFocusRestorationRef = useRef<HTMLButtonElement>(null);

    return (
        <div className={ThreadContentStyles.root} key={threadContentAndActionKey}>
            <ThreadContentTitle
                thread={thread}
                deleteThread={deleteThread}
                hasExistingPlans={todoPlanDrawer.hasExistingPlans}
                showTraceButton={showThreadTraceUI && showControlPlaneDependentFeatures && !!agentAppInsightsAppId}
                toggleTraceVisibility={() => setShowTrace(!showTrace)}
                traceFocusRestorationRef={traceFocusRestorationRef}
            />
            <ChatBox
                threadId={thread?.id}
                addThread={addThread}
                updateThreadLastReadTime={updateThreadLastReadTime}
                threadSource={thread?.source}
                canOpenAgentTaskPanel={true}
                todoPlanDrawer={todoPlanDrawer}
                setMenuCollapsed={setMenuCollapsed}
            />
            {!!thread && showTrace && agentAppInsightsAppId && (
                <TracePanel
                    appInsightsAppId={agentAppInsightsAppId}
                    thread={thread}
                    isOpen={showTrace}
                    onClose={() => setShowTrace(!showTrace)}
                    focusRestorationRef={traceFocusRestorationRef}
                />
            )}
        </div>
    );
});

ThreadContent.displayName = 'ThreadContent';
