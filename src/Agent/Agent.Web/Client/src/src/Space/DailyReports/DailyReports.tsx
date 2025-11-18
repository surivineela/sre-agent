import { makeStyles, mergeClasses, tokens } from '@fluentui/react-components';
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
        padding: `10px ${tokens.spacingHorizontalXXL} 0px`,
        height: '100%',
        backgroundColor: tokens.colorNeutralBackground3,
    },
    errorRoot: {
        padding: `10px ${tokens.spacingHorizontalXXL} 0px`,
        height: '100vh',
        backgroundColor: tokens.colorNeutralBackground3,
    },
});

const DailyReports: FC = () => {
    const styles = useStyles();
    const { canReadThreads } = useUserPermissions();
    const { resourceId } = useContext(EnvironmentContext);

    const [selectedThread, setSelectedThread] = useState<Thread | null>(null);

    if (!canReadThreads) {
        return (
            <div className={styles.errorRoot}>
                <NoAccessError requiredPermission={PermissionActions.AgentThreadsRead} resourceId={resourceId} />
            </div>
        );
    }

    return (
        <div className={mergeClasses(styles.root)}>
            <DailyReportThreadDropdown selectedThread={selectedThread} setSelectedThread={setSelectedThread} />
            {selectedThread ? (
                <div key={selectedThread?.id}>
                    <ChatBox
                        addThread={() => {}}
                        updateThreadLastReadTime={() => {}}
                        threadId={selectedThread?.id}
                        threadSource={selectedThread?.source}
                        stylesProps={{
                            chatBox: {
                                height: 'calc(100vh - 130px)',
                            },
                        }}
                        canOpenSidePanel={true}
                    />
                </div>
            ) : (
                <div style={{ height: 'calc(100vh - 130px)' }} />
            )}
        </div>
    );
};

export default DailyReports;
