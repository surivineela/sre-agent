import {
    Button,
    Drawer,
    DrawerBody,
    DrawerHeader,
    DrawerHeaderTitle,
    MessageBar,
    MessageBarBody,
    MessageBarTitle,
    Toolbar,
    ToolbarButton,
    ToolbarGroup,
    makeStyles,
} from '@fluentui/react-components';
import { Dismiss24Regular, FullScreenMaximize16Regular } from '@fluentui/react-icons';
import React from 'react';
import { useIntl } from 'react-intl';
import { Thread } from '../../../Common/Contracts/DataPlane/Thread';
import { IncidentManagementResources } from '../../../Strings/SREAgentResources';
import IncidentChat from '../IncidentChat';

export interface IncidentChatDrawerProps {
    isOpen: boolean;
    onClose: () => void;
    errorMessage?: string;
    thread?: Thread;
    onDeleteThread?: () => void;
    onEnterFullScreen?: () => void;
    size?: 'small' | 'medium' | 'large' | 'full';
    titleSuffix?: React.ReactNode;
    titleActions?: React.ReactNode;
}

const IncidentChatDrawer: React.FC<IncidentChatDrawerProps> = ({
    thread,
    isOpen,
    errorMessage,
    onClose,
    onDeleteThread,
    size = 'large',
    titleSuffix,
    titleActions,
    onEnterFullScreen,
}) => {
    const intl = useIntl();
    const styles = useIncidentChatDrawerStyles();

    return (
        <Drawer
            modalType="non-modal"
            open={isOpen}
            position="end"
            size={size}
            className={styles.drawerRoot}
            onOpenChange={(_, data) => {
                if (!data.open) onClose();
            }}
        >
            <DrawerHeader className={styles.header}>
                <DrawerHeaderTitle
                    heading={{
                        className: styles.headingContainer,
                    }}
                    action={
                        <Toolbar>
                            <ToolbarGroup className={styles.toolbarGroup}>
                                {onEnterFullScreen && (
                                    <Button
                                        icon={<FullScreenMaximize16Regular />}
                                        className={styles.fullPageButton}
                                        onClick={onEnterFullScreen}
                                    >
                                        {intl.formatMessage(IncidentManagementResources.fullPage)}
                                    </Button>
                                )}
                                {titleActions}
                                <ToolbarButton
                                    aria-label={intl.formatMessage(IncidentManagementResources.closePanel)}
                                    appearance="transparent"
                                    icon={<Dismiss24Regular />}
                                    onClick={onClose}
                                />
                            </ToolbarGroup>
                        </Toolbar>
                    }
                >
                    <div className={styles.titleText}>
                        {errorMessage || thread?.title || intl.formatMessage(IncidentManagementResources.incident)}
                    </div>
                    {titleSuffix}
                </DrawerHeaderTitle>
            </DrawerHeader>
            <DrawerBody className={styles.body}>
                {errorMessage ? (
                    <div className={styles.errorWrapper}>
                        <MessageBar intent="error" layout="multiline">
                            <MessageBarBody>
                                <MessageBarTitle>{errorMessage}</MessageBarTitle>
                            </MessageBarBody>
                        </MessageBar>
                    </div>
                ) : (
                    thread && (
                        <div className={styles.chatContainer}>
                            <div className={styles.chatContentWrapper}>
                                <IncidentChat
                                    selectedThread={thread}
                                    exitToHome={onClose}
                                    isExpandedView={false}
                                    handleThreadDelete={onDeleteThread || (() => {})}
                                    openThreadFullScreen={onEnterFullScreen}
                                />
                            </div>
                        </div>
                    )
                )}
            </DrawerBody>
        </Drawer>
    );
};

const useIncidentChatDrawerStyles = makeStyles({
    drawerRoot: {
        marginTop: '50px',
        marginBottom: '8px',
        borderRadius: '12px',
    },
    header: {
        padding: '16px 16px 7px 16px',
    },
    headingContainer: {
        display: 'flex',
        flexDirection: 'row',
        gap: '8px',
        alignItems: 'center',
        justifyContent: 'start',
        overflow: 'hidden',
    },
    toolbarGroup: {
        display: 'flex',
        flexDirection: 'row',
        gap: '8px',
    },
    fullPageButton: {
        fontWeight: 'normal',
        fontSize: '12px',
        lineHeight: '16px',
        padding: '2px 8px 2px 4px',
        margin: 'auto',
    },
    titleText: {
        whiteSpace: 'nowrap',
        textOverflow: 'ellipsis',
        overflow: 'hidden',
    },
    body: {
        padding: '0px 16px 0px 0px',
    },
    chatContainer: {
        height: '100%',
        display: 'flex',
        flexDirection: 'column',
    },
    chatContentWrapper: {
        flex: '1 1 auto',
        minHeight: '360px',
    },
    errorWrapper: {
        margin: '16px',
        marginTop: '0px',
    },
});

export default IncidentChatDrawer;
