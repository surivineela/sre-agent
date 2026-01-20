import { makeStyles } from '@fluentui/react-components';
import { FC } from 'react';
import { IChatBoxFooterProps } from '../../Contracts/Activities';
import ChatBoxFooter from '../ChatBoxFooter';
import { SreAgentBranding } from './SreAgentBranding';

const useStyles = makeStyles({
    root: {
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        overflow: 'hidden',
    },
    chat: {
        flex: '0 0 auto',
        display: 'flex',
        flexDirection: 'column',
        justifySelf: 'center',
        padding: '50px 0px',
    },
    dashboard: {
        flex: '1 1 auto',
        minHeight: 0,
        overflow: 'auto',
    },
});

export const OverviewChatBox: FC<IChatBoxFooterProps> = ({
    sendMessage,
    isLoading,
    downButtonState,
    onClickDownButton,
    prompts,
    messagePromptsUsed,
    cancelStreaming,
    isTyping,
    isCancellingStreaming,
    threadId,
    threadSource,
    isDeepInvestigationButtonEnabled,
    isDeepInvestigationTurnedOn,
    onClickDeepInvestigationButton,
    forcedAgentName,
    lockAgentSelection,
    inputDisabledMessage,
    isIncidentRetroModeTurnedOn,
    toggleIncidentRetroMode,
    hasPendingUserQuestion,
}) => {
    const styles = useStyles();

    return (
        <div className={styles.root}>
            <div className={styles.chat}>
                <SreAgentBranding />
                <ChatBoxFooter
                    sendMessage={sendMessage}
                    isLoading={isLoading}
                    downButtonState={downButtonState}
                    onClickDownButton={onClickDownButton}
                    prompts={prompts}
                    messagePromptsUsed={messagePromptsUsed}
                    cancelStreaming={cancelStreaming}
                    isTyping={isTyping}
                    isCancellingStreaming={isCancellingStreaming}
                    threadId={threadId}
                    threadSource={threadSource}
                    isDeepInvestigationButtonEnabled={isDeepInvestigationButtonEnabled}
                    isDeepInvestigationTurnedOn={isDeepInvestigationTurnedOn}
                    onClickDeepInvestigationButton={onClickDeepInvestigationButton}
                    forcedAgentName={forcedAgentName}
                    lockAgentSelection={lockAgentSelection}
                    inputDisabledMessage={inputDisabledMessage}
                    isIncidentRetroModeTurnedOn={isIncidentRetroModeTurnedOn}
                    toggleIncidentRetroMode={toggleIncidentRetroMode}
                    hasPendingUserQuestion={hasPendingUserQuestion}
                    isOverview={true}
                />
            </div>
            <div className={styles.dashboard}>{'Dashboard section'}</div>
        </div>
    );
};
