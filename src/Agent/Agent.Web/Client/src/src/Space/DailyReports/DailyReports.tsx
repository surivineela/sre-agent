import { makeStyles, mergeClasses, tokens } from '@fluentui/react-components';
import { FC, useState } from 'react';
import { Thread } from '../../Common/Contracts/DataPlane/Thread';
import ChatBox from '../Activities/ChatBox';
import DailyReportThreadDropdown from './DailyReportThreadDropdown';

const useStyles = makeStyles({
    root: {
        padding: `10px ${tokens.spacingHorizontalXXL} 0px`,
        height: '100%',
        backgroundColor: tokens.colorNeutralBackground3,
    },
});

const DailyReports: FC = () => {
    const styles = useStyles();

    const [selectedThread, setSelectedThread] = useState<Thread | null>(null);

    return (
        <div className={mergeClasses(styles.root)}>
            <DailyReportThreadDropdown selectedThread={selectedThread} setSelectedThread={setSelectedThread} />
            {selectedThread ? (
                <div key={selectedThread?.id}>
                    <ChatBox
                        addThread={() => {}}
                        updateThreadLastReadTime={() => {}}
                        isAgentTaskEnabled={true}
                        threadId={selectedThread?.id}
                        threadSource={selectedThread?.source}
                        stylesProps={{
                            chatBox: {
                                height: 'calc(100vh - 130px)',
                            },
                        }}
                    />
                </div>
            ) : (
                <div style={{ height: 'calc(100vh - 130px)' }} />
            )}
        </div>
    );
};

export default DailyReports;
