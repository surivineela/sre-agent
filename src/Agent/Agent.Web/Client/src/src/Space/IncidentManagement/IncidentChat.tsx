import { FC, useCallback, useContext } from 'react';
import { Thread } from '../../Common/Contracts/DataPlane/Thread';
import { ChatBox } from '../Activities/ChatBox';
import ThreadActionsMenu from '../Activities/ThreadActionsMenu';
import { ChatBoxSidePanelData, ChatBoxSidePanelType } from '../Contracts/Activities';
import { IncidentsOverviewContext } from '../Contracts/Context';
import { useIncidentManagementStyles } from '../Styles/IncidentManagement.styles';
import TitleBarNavigation from './Common/TitleBarNavigation';

export interface IncidentChatProps {
    selectedThread: Thread;
    exitToHome: () => void;
    isExpandedView?: boolean;
    handleThreadDelete: () => void;
    onEnterFullScreen?: () => void;
    titleActions?: React.ReactNode;
}

const IncidentChat: FC<IncidentChatProps> = ({
    selectedThread,
    exitToHome,
    isExpandedView,
    handleThreadDelete,
    titleActions,
    onEnterFullScreen,
}) => {
    const styles = useIncidentManagementStyles();

    return isExpandedView ? (
        <TitleBarNavigation
            title={selectedThread.title}
            onBackClick={exitToHome}
            titleChildren={
                <ThreadActionsMenu
                    thread={selectedThread}
                    handleThreadDelete={handleThreadDelete}
                    hideCopyDeeplink={true}
                    hideFavorite={true}
                />
            }
            titleActions={titleActions}
        >
            <div className={styles.navPanelContent}>
                <div className={styles.incidentChatWrapper}>
                    <IncidentChatInner selectedThread={selectedThread} isExpandedView={!!isExpandedView} />
                </div>
            </div>
        </TitleBarNavigation>
    ) : (
        <IncidentChatInner selectedThread={selectedThread} onEnterFullScreen={onEnterFullScreen} isExpandedView={!!isExpandedView} />
    );
};

export default IncidentChat;

interface IncidentChatInnerProps {
    selectedThread: Thread;
    onEnterFullScreen?: () => void;
    isExpandedView: boolean;
}

const IncidentChatInner: FC<IncidentChatInnerProps> = ({ selectedThread, onEnterFullScreen, isExpandedView }) => {
    const { initialSidePanelDataMap, onInitialSidePanelDataChanged } = useContext(IncidentsOverviewContext);

    const onOpenSidePanel = useCallback(
        (_panelType: ChatBoxSidePanelType, data: ChatBoxSidePanelData) => {
            onInitialSidePanelDataChanged(selectedThread.id, data);

            if (!isExpandedView) {
                onEnterFullScreen?.();
            }
        },
        [onEnterFullScreen, isExpandedView, onInitialSidePanelDataChanged, selectedThread.id]
    );

    const onCloseSidePanel = useCallback(
        (_panelType: ChatBoxSidePanelType) => {
            onInitialSidePanelDataChanged(selectedThread.id, undefined);
        },
        [onInitialSidePanelDataChanged, selectedThread.id]
    );

    return (
        <ChatBox
            threadId={selectedThread.id}
            addThread={() => {}}
            updateThreadLastReadTime={() => {}}
            threadSource={selectedThread.source}
            onOpenSidePanel={onOpenSidePanel}
            onCloseSidePanel={onCloseSidePanel}
            canOpenSidePanel={isExpandedView}
            initialSidePanelData={isExpandedView ? initialSidePanelDataMap.get(selectedThread.id) : undefined}
            stylesProps={{
                rootStyle: {
                    height: '100%',
                },
                chatBoxAndAgentTask: {
                    boxShadow: 'unset',
                    borderRadius: 'unset',
                    width: '100%',
                    height: '100%',
                    marginBottom: '0px',
                },
                chatBoxInner: {
                    borderRadius: 'unset',
                },
            }}
        />
    );
};
