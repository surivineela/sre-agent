import { FC, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { useParams } from 'react-router';
import AzPortalProxy from '../../Common/AzPortalProxy/AzPortalProxy';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ThreadClient } from '../../Common/Clients/ThreadClient';
import NoAccessError from '../../Common/Components/NoAccessError';
import { PermissionActions } from '../../Common/Contracts/Azure/Permission';
import { Thread as ThreadObject } from '../../Common/Contracts/DataPlane/Thread';
import { SettingNames, useConfigSetting } from '../../Common/Hooks/ConfigSettings';
import useUserPermissions from '../../Common/Hooks/useUserPermissions';
import { ChatBoxHandleRef } from '../Contracts/Activities';
import { SreAgentContext, SreAgentSpaceContext } from '../Contracts/Context';
import { TracePanel } from '../Foundry/app/components/shell/playground/tracing/TracePanel';
import { useThreadContentTitleToDoPlanButton } from '../Hooks/useThreadContentTitleToDoPlanButton';
import { ThreadContentStyles } from '../Styles/Activities.styles';
import ChatBox from './ChatBox';
import ThreadContentTitle from './ThreadContentTitle';

const inStandaloneMode = AzPortalProxy.inStandaloneMode;

interface IThreadProps {
    isOverview?: boolean;
}

const Thread: FC<IThreadProps> = ({ isOverview }) => {
    const { threadId } = useParams();

    const { agentObj } = useContext(SreAgentContext);
    const {
        threadsRenderKey,
        selectThread,
        addThread,
        deleteThread,
        updateThreadLastReadTime,
        updateThreadFavorite,
        updateThreadTitle,
        onExpandOrCollapseNavBar,
        subscribeThreadTitleUpdate,
        subscribeThreadFavoriteUpdate,
    } = useContext(SreAgentSpaceContext);
    const { sreAgentEndpoint, resourceId } = useContext(EnvironmentContext);
    const { isCrossTenantPortalMode } = useContext(EnvironmentContext);

    const { canReadThreads } = useUserPermissions();
    const showThreadTraceUI = useConfigSetting(SettingNames.ShowThreadTraceUI);

    const threadClient = ThreadClient.getInstance(sreAgentEndpoint);

    const [showTrace, setShowTrace] = useState(false);
    const [thread, setThread] = useState<ThreadObject | null>(null);
    const [isLoadingThread, setIsLoadingThread] = useState<boolean>(false);
    const [loadingThreadFailed, setLoadingThreadFailed] = useState<boolean>(false);

    const showControlPlaneDependentFeatures = useMemo(() => !inStandaloneMode && !isCrossTenantPortalMode, [isCrossTenantPortalMode]);
    const agentAppInsightsAppId = useMemo<string | undefined>(
        () => agentObj?.properties?.logConfiguration?.applicationInsightsConfiguration?.appId,
        [agentObj]
    );

    const traceFocusRestorationRef = useRef<HTMLButtonElement>(null);
    const chatboxHandleRef = useRef<ChatBoxHandleRef>(null);

    const { hasToDoPlans, isToDoPlanOpen, openToDoPlan, closeToDoPlan, setHasToDoPlans, onOpenSidePanel, onCloseSidePanel } =
        useThreadContentTitleToDoPlanButton(chatboxHandleRef);

    useEffect(() => {
        let isSubscribed = true;
        setIsLoadingThread(true);
        setLoadingThreadFailed(false);

        if (threadId) {
            const getThread = async () => {
                const threadResponse = await threadClient.getThread(threadId);
                if (isSubscribed) {
                    if (threadResponse.isSuccessful && threadResponse.content) {
                        setThread(threadResponse.content);
                        setIsLoadingThread(false);
                        setLoadingThreadFailed(false);
                    } else {
                        setThread(null);
                        setIsLoadingThread(false);
                        setLoadingThreadFailed(true);
                    }
                }
            };
            getThread();
        } else {
            setThread(null);
            setIsLoadingThread(false);
            setLoadingThreadFailed(false);
        }

        return () => {
            isSubscribed = false;
        };
    }, [threadId, threadClient]);

    return canReadThreads ? (
        <div key={threadsRenderKey} className={ThreadContentStyles.root}>
            {!isOverview && (
                <ThreadContentTitle
                    thread={thread}
                    isLoadingThread={isLoadingThread}
                    loadingThreadFailed={loadingThreadFailed}
                    updateThreadTitle={updateThreadTitle}
                    updateThreadFavorite={updateThreadFavorite}
                    subscribeThreadTitleUpdate={subscribeThreadTitleUpdate}
                    subscribeThreadFavoriteUpdate={subscribeThreadFavoriteUpdate}
                    deleteThread={deleteThread}
                    hasToDoPlans={hasToDoPlans}
                    isToDoPlanOpen={isToDoPlanOpen}
                    openToDoPlan={openToDoPlan}
                    closeToDoPlan={closeToDoPlan}
                    showTraceButton={showThreadTraceUI && showControlPlaneDependentFeatures && !!agentAppInsightsAppId}
                    toggleTraceVisibility={() => setShowTrace(!showTrace)}
                    traceFocusRestorationRef={traceFocusRestorationRef}
                />
            )}
            <ChatBox
                threadId={threadId}
                selectThread={selectThread}
                addThread={addThread}
                updateThreadLastReadTime={updateThreadLastReadTime}
                threadSource={thread?.source}
                expandOrCollapseNavBar={onExpandOrCollapseNavBar}
                setHasToDoPlans={setHasToDoPlans}
                onOpenSidePanel={onOpenSidePanel}
                onCloseSidePanel={onCloseSidePanel}
                canOpenSidePanel={true}
                ref={chatboxHandleRef}
                initialRetroModeEnabled={thread?.isIncidentTestModeEnabled}
                isOverview={isOverview}
            />
            {!isOverview && !!thread && showTrace && agentAppInsightsAppId && (
                <TracePanel
                    appInsightsAppId={agentAppInsightsAppId}
                    thread={thread}
                    isOpen={showTrace}
                    onClose={() => setShowTrace(!showTrace)}
                    focusRestorationRef={traceFocusRestorationRef}
                />
            )}
        </div>
    ) : (
        <NoAccessError requiredPermission={PermissionActions.AgentThreadsRead} resourceId={resourceId} />
    );
};

export default Thread;
