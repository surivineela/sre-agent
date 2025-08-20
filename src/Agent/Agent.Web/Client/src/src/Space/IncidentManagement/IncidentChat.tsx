import { Breadcrumb, BreadcrumbButton, BreadcrumbDivider, BreadcrumbItem } from '@fluentui/react-components';
import { FC } from 'react';
import useIntl from 'react-intl/src/components/useIntl';
import { Thread } from '../../Common/Contracts/DataPlane/Thread';
import { IncidentManagementResources } from '../../Strings/SREAgentResources';
import { ChatBox } from '../Activities/ChatBox';
import { useIncidentManagementStyles } from '../Styles/IncidentManagement.styles';
import { DirtyStateConfirmationWrapper } from './CreateIncidentHandler/DirtyStateConfirmationDialog';

export interface IncidentChatProps {
    selectedThread: Thread;
    exitToHome: () => void;
}

const IncidentChat: FC<IncidentChatProps> = ({ selectedThread, exitToHome }) => {
    const styles = useIncidentManagementStyles();
    const intl = useIntl();

    return (
        <div className={styles.breadCrumbAndPanelWrapper}>
            <Breadcrumb className={styles.breadcrumb}>
                <BreadcrumbItem>
                    <DirtyStateConfirmationWrapper isDirty={false} onConfirm={exitToHome}>
                        <BreadcrumbButton>{intl.formatMessage(IncidentManagementResources.incidentsOverview)}</BreadcrumbButton>
                    </DirtyStateConfirmationWrapper>
                </BreadcrumbItem>
                <BreadcrumbDivider />
                <BreadcrumbItem style={{ marginLeft: 6 }}>{selectedThread.title}</BreadcrumbItem>
            </Breadcrumb>
            <div className={styles.navPanelWrapper}>
                <div className={styles.navPanelContent}>
                    <div className={styles.incidentChatWrapper}>
                        <ChatBox
                            threadId={selectedThread.id}
                            addThread={() => {}}
                            updateThreadLastReadTime={() => {}}
                            threadSource={selectedThread.source}
                            collapseResizables={() => {}}
                            isAgentTaskEnabled={true}
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
                    </div>
                </div>
            </div>
        </div>
    );
};

export default IncidentChat;
