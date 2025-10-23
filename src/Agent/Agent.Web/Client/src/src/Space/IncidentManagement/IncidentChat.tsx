import { FC } from 'react';
import { Thread } from '../../Common/Contracts/DataPlane/Thread';
import { ChatBox } from '../Activities/ChatBox';
import ThreadActionsMenu from '../Activities/ThreadActionsMenu';
import { useIncidentManagementStyles } from '../Styles/IncidentManagement.styles';
import TitleBarNavigation from './Common/TitleBarNavigation';

export interface IncidentChatProps {
    selectedThread: Thread;
    exitToHome: () => void;
    isExpandedView?: boolean;
    handleThreadDelete: () => void;
    titleActions?: React.ReactNode;
    openThreadFullScreen?: () => void;
}

const IncidentChat: FC<IncidentChatProps> = ({
    selectedThread,
    exitToHome,
    isExpandedView,
    handleThreadDelete,
    titleActions,
    openThreadFullScreen,
}) => {
    const styles = useIncidentManagementStyles();

    return isExpandedView ? (
        <TitleBarNavigation
            title={selectedThread.title}
            onBackClick={exitToHome}
            titleChildren={<ThreadActionsMenu thread={selectedThread} handleThreadDelete={handleThreadDelete} hideCopyDeeplink={true} />}
            titleActions={titleActions}
        >
            <div className={styles.navPanelContent}>
                <div className={styles.incidentChatWrapper}>
                    <IncidentChatInner selectedThread={selectedThread} canOpenAgentTaskPanel={true} />
                </div>
            </div>
        </TitleBarNavigation>
    ) : (
        <IncidentChatInner selectedThread={selectedThread} canOpenAgentTaskPanel={false} openThreadFullScreen={openThreadFullScreen} />
    );
};

export default IncidentChat;

interface IncidentChatInnerProps {
    selectedThread: Thread;
    canOpenAgentTaskPanel: boolean;
    openThreadFullScreen?: () => void;
}

const IncidentChatInner: FC<IncidentChatInnerProps> = ({ selectedThread, canOpenAgentTaskPanel, openThreadFullScreen }) => {
    return (
        <ChatBox
            threadId={selectedThread.id}
            addThread={() => {}}
            updateThreadLastReadTime={() => {}}
            threadSource={selectedThread.source}
            canOpenAgentTaskPanel={canOpenAgentTaskPanel}
            onOpenAgentTaskPanel={openThreadFullScreen}
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
