import { makeStyles, tokens } from '@fluentui/react-components';
import { FC, useContext, useState } from 'react';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { NoAccessError } from '../../Common/Components/NoAccessError';
import { PermissionActions } from '../../Common/Contracts/Azure/Permission';
import { Thread } from '../../Common/Contracts/DataPlane/Thread';
import { useUserPermissions } from '../../Common/Hooks/useUserPermissions';
import ChatBox from '../Activities/ChatBox';
import DailyReportThreadDropdown from './DailyReportThreadDropdown';

const useStyles = makeStyles({
    root: {
        flex: '1 1 auto',
        overflowY: 'hidden',
        display: 'flex',
        flexDirection: 'column',
    },
    error: {
        padding: `10px ${tokens.spacingHorizontalXXL} 0px`,
        height: '100vh',
        backgroundColor: tokens.colorNeutralBackground3,
    },
    chatBoxContainer: {
        flex: 1,
        minHeight: 0,
        display: 'flex',
    },
});

const DailyReports: FC = () => {
    const styles = useStyles();
    const { canReadThreads } = useUserPermissions();
    const { resourceId } = useContext(EnvironmentContext);

    const [selectedThread, setSelectedThread] = useState<Thread | null>(null);

    if (!canReadThreads) {
        return (
            <div className={styles.error}>
                <NoAccessError requiredPermission={PermissionActions.AgentThreadsRead} resourceId={resourceId} />
            </div>
        );
    }

    return (
        <div className={styles.root}>
            <DailyReportThreadDropdown selectedThread={selectedThread} setSelectedThread={setSelectedThread} />
            {selectedThread ? (
                <div key={selectedThread?.id} className={styles.chatBoxContainer}>
                    <ChatBox
                        addThread={() => {}}
                        selectThread={() => {}}
                        updateThreadLastReadTime={() => {}}
                        threadId={selectedThread?.id}
                        threadSource={selectedThread?.source}
                        canOpenSidePanel={true}
                    />
                </div>
            ) : null}
        </div>
    );
};

export default DailyReports;
