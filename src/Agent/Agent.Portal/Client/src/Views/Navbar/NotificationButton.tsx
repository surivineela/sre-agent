import { Badge, Button, makeStyles, Spinner, Tooltip } from '@fluentui/react-components';
import { Alert32Regular } from '@fluentui/react-icons';
import { useState } from 'react';
import { useIntl } from 'react-intl';
import { useNotifications } from '../../Common/Contexts/NotificationContext';
import { PortalResources } from '../../Strings/Resources';
import { NotificationDrawer } from '../Notifications/NotificationDrawer';

const useStyles = makeStyles({
    buttonWrapper: {
        position: 'relative',
        display: 'inline-block',
    },
    badge: {
        position: 'absolute',
        top: '4px',
        right: '4px',
        pointerEvents: 'none',
    },
    spinner: {
        position: 'absolute',
        top: '1px',
        right: '1px',
        pointerEvents: 'none',
    },
});

export const NotificationButton = () => {
    const intl = useIntl();
    const styles = useStyles();
    const { unreadCount, notifications } = useNotifications();
    const [drawerOpen, setDrawerOpen] = useState(false);

    const hasInProgress = notifications.some(n => n.status === 'in-progress');
    const showBadge = unreadCount > 0;

    return (
        <>
            <Tooltip content={intl.formatMessage(PortalResources.notifications)} relationship="label">
                <div className={styles.buttonWrapper}>
                    <Button
                        icon={<Alert32Regular />}
                        appearance="subtle"
                        onClick={() => setDrawerOpen(true)}
                        aria-label={intl.formatMessage(PortalResources.notifications)}
                    />
                    {showBadge && (
                        <Badge className={styles.badge} size="extra-small" appearance="filled" color="danger">
                            {unreadCount > 99 ? '99+' : unreadCount}
                        </Badge>
                    )}
                    {hasInProgress && (
                        <div className={styles.spinner}>
                            <Spinner size="extra-tiny" />
                        </div>
                    )}
                </div>
            </Tooltip>

            <NotificationDrawer open={drawerOpen} onOpenChange={setDrawerOpen} />
        </>
    );
};
