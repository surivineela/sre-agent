import { memo, useContext, useMemo, useRef, useState } from 'react';
import AzPortalProxy from '../../Common/AzPortalProxy/AzPortalProxy';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { SettingNames, useConfigSetting } from '../../Common/Hooks/ConfigSettings';
import { ChatBoxHandleRef, IThreadContentProps } from '../Contracts/Activities';
import { AgentContext, SreAgentContext } from '../Contracts/Context';
import { TracePanel } from '../Foundry/app/components/shell/playground/tracing/TracePanel';
import { useThreadContentTitleToDoPlanButton } from '../Hooks/useThreadContentTitleToDoPlanButton';
import { ThreadContentStyles } from '../Styles/Activities.styles';
import ChatBox from './ChatBox';
import ThreadContentTitle from './ThreadContentTitle';

const inStandaloneMode = AzPortalProxy.inStandaloneMode;

export const ThreadContent = memo(({ thread, addThread, deleteThread, updateThreadLastReadTime }: IThreadContentProps) => {
    const showThreadTraceUI = useConfigSetting(SettingNames.ShowThreadTraceUI);
    const { threadContentAndActionKey, setMenuCollapsed } = useContext(AgentContext);

    const [showTrace, setShowTrace] = useState(false);
    const { isCrossTenantPortalMode } = useContext(EnvironmentContext);
    const showControlPlaneDependentFeatures = useMemo(() => !inStandaloneMode && !isCrossTenantPortalMode, [isCrossTenantPortalMode]);

    const { agentObj } = useContext(SreAgentContext);
    const agentAppInsightsAppId = useMemo<string | undefined>(
        () => agentObj?.properties?.logConfiguration?.applicationInsightsConfiguration?.appId,
        [agentObj]
    );
    const traceFocusRestorationRef = useRef<HTMLButtonElement>(null);
    const chatboxHandleRef = useRef<ChatBoxHandleRef>(null);

    const { hasToDoPlans, isToDoPlanOpen, openToDoPlan, closeToDoPlan, setHasToDoPlans, onOpenSidePanel, onCloseSidePanel } =
        useThreadContentTitleToDoPlanButton(chatboxHandleRef);

    return (
        <div className={ThreadContentStyles.root} key={threadContentAndActionKey}>
            <ThreadContentTitle
                thread={thread}
                deleteThread={deleteThread}
                hasToDoPlans={hasToDoPlans}
                isToDoPlanOpen={isToDoPlanOpen}
                openToDoPlan={openToDoPlan}
                closeToDoPlan={closeToDoPlan}
                showTraceButton={showThreadTraceUI && showControlPlaneDependentFeatures && !!agentAppInsightsAppId}
                toggleTraceVisibility={() => setShowTrace(!showTrace)}
                traceFocusRestorationRef={traceFocusRestorationRef}
            />
            <ChatBox
                threadId={thread?.id}
                addThread={addThread}
                updateThreadLastReadTime={updateThreadLastReadTime}
                threadSource={thread?.source}
                setMenuCollapsed={setMenuCollapsed}
                setHasToDoPlans={setHasToDoPlans}
                onOpenSidePanel={onOpenSidePanel}
                onCloseSidePanel={onCloseSidePanel}
                ref={chatboxHandleRef}
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
