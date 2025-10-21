import {
    Button,
    Divider,
    Drawer,
    DrawerBody,
    DrawerHeader,
    DrawerHeaderTitle,
    makeStyles,
    Menu,
    MenuButton,
    MenuItem,
    MenuList,
    MenuPopover,
    MenuTrigger,
    Text,
    tokens,
} from '@fluentui/react-components';
import { ChevronDown20Regular, Dismiss24Regular } from '@fluentui/react-icons';
import { useEffect } from 'react';
import { useIntl } from 'react-intl';
import { useNotifications } from '../../Common/Contexts/NotificationContext';
import { PortalResources } from '../../Strings/Resources';
import { NotificationCard } from './NotificationCard';

const useStyles = makeStyles({
    drawer: {
        width: '400px',
        top: '44px', // Start below navbar
        height: 'calc(100vh - 44px)', // Full height minus navbar
    },
    header: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        width: '100%',
    },
    headerContent: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        width: '100%',
    },
    body: {
        display: 'flex',
        flexDirection: 'column',
        padding: 0,
    },
    actions: {
        display: 'flex',
        gap: tokens.spacingHorizontalS,
        paddingTop: tokens.spacingVerticalM,
        paddingBottom: tokens.spacingVerticalS,
        paddingLeft: tokens.spacingHorizontalM,
        paddingRight: tokens.spacingHorizontalM,
        justifyContent: 'flex-end',
    },
    divider: {
        flexGrow: 0,
    },
    notificationList: {
        display: 'flex',
        flexDirection: 'column',
    },
    emptyState: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'flex-start',
        padding: tokens.spacingVerticalXXXL,
        gap: tokens.spacingVerticalM,
        textAlign: 'center',
    },
    emptyStateIcon: {
        fontSize: '48px',
        color: tokens.colorNeutralForeground3,
    },
});

interface NotificationDrawerProps {
    open: boolean;
    onOpenChange: (open: boolean) => void;
}

export const NotificationDrawer = ({ open, onOpenChange }: NotificationDrawerProps) => {
    const intl = useIntl();
    const styles = useStyles();
    const { notifications, dismiss, dismissAll, dismissCompleted, markAllAsRead } = useNotifications();

    useEffect(() => {
        if (open) {
            markAllAsRead();
        }
    }, [open, markAllAsRead]);

    const hasNotifications = notifications.length > 0;
    const hasCompletedNotifications = notifications.some(
        n => n.status === 'success' || n.status === 'error' || n.status === 'warning' || n.status === 'info'
    );

    return (
        <Drawer
            type="overlay"
            separator
            open={open}
            onOpenChange={(_, { open }) => onOpenChange(open)}
            position="end"
            className={styles.drawer}
        >
            <DrawerHeader>
                <div className={styles.headerContent}>
                    <DrawerHeaderTitle>{intl.formatMessage(PortalResources.notifications)}</DrawerHeaderTitle>
                    <Button
                        appearance="subtle"
                        icon={<Dismiss24Regular />}
                        onClick={() => onOpenChange(false)}
                        aria-label={intl.formatMessage(PortalResources.close)}
                    />
                </div>
            </DrawerHeader>

            <DrawerBody className={styles.body}>
                {hasNotifications ? (
                    <>
                        <div className={styles.actions}>
                            <Button
                                appearance="transparent"
                                size="small"
                                onClick={() => {
                                    dismissAll();
                                }}
                            >
                                {intl.formatMessage(PortalResources.dismissAll)}
                            </Button>
                            <Menu positioning="below-end">
                                <MenuTrigger disableButtonEnhancement>
                                    <MenuButton appearance="transparent" size="small" icon={<ChevronDown20Regular />} />
                                </MenuTrigger>
                                <MenuPopover>
                                    <MenuList>
                                        <MenuItem
                                            onClick={() => {
                                                dismissCompleted();
                                            }}
                                            disabled={!hasCompletedNotifications}
                                        >
                                            {intl.formatMessage(PortalResources.dismissCompleted)}
                                        </MenuItem>
                                    </MenuList>
                                </MenuPopover>
                            </Menu>
                        </div>

                        <Divider className={styles.divider} />

                        <div className={styles.notificationList}>
                            {notifications.map(notification => (
                                <NotificationCard key={notification.id} notification={notification} onDismiss={dismiss} />
                            ))}
                        </div>
                    </>
                ) : (
                    <div className={styles.emptyState}>
                        <Text size={500} weight="semibold">
                            {intl.formatMessage(PortalResources.noNotifications)}
                        </Text>
                        <Text size={300} align="center">
                            {intl.formatMessage(PortalResources.noNotificationsDescription)}
                        </Text>
                    </div>
                )}
            </DrawerBody>
        </Drawer>
    );
};
