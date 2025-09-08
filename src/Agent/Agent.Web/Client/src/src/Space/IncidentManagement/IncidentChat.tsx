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
}

const IncidentChat: FC<IncidentChatProps> = ({ selectedThread, exitToHome, isExpandedView }) => {
    const styles = useIncidentManagementStyles();

    return isExpandedView ? (
        <TitleBarNavigation
            title={selectedThread.title}
            onBackClick={exitToHome}
            titleChildren={
                <ThreadActionsMenu thread={selectedThread} handleThreadDelete={() => {}} hideCopyDeeplink={true} hideDelete={true} />
            }
        >
            <div className={styles.navPanelContent}>
                <div className={styles.incidentChatWrapper}>
                    <IncidentChatInner selectedThread={selectedThread} isAgentTaskEnabled={true} />
                </div>
            </div>
        </TitleBarNavigation>
    ) : (
        <IncidentChatInner selectedThread={selectedThread} isAgentTaskEnabled={false} />
    );
};

export default IncidentChat;

interface IncidentChatInnerProps {
    selectedThread: Thread;
    isAgentTaskEnabled?: boolean;
}

const IncidentChatInner: FC<IncidentChatInnerProps> = ({ selectedThread, isAgentTaskEnabled }) => {
    return (
        <ChatBox
            threadId={selectedThread.id}
            addThread={() => {}}
            updateThreadLastReadTime={() => {}}
            threadSource={selectedThread.source}
            collapseResizables={() => {}}
            isAgentTaskEnabled={!!isAgentTaskEnabled}
            stylesProps={{
                chatBoxAndAgentTask: {
                    boxShadow: 'unset',
                    borderRadius: 'unset',
                    width: '100%',
                    height: '100%',
                    marginBottom: '0px',
                },
                chatBox: {
                    height: '100%',
                },
                chatBoxInner: {
                    borderRadius: 'unset',
                },
                chatContainer: {
                    // marginLeft: 'auto',
                    // marginRight: 'auto',
                },
            }}
        />
    );
};
