import { Button, makeStyles, Spinner, Text, tokens, Tooltip } from '@fluentui/react-components';
import { CheckmarkCircle20Filled, Dismiss20Regular, ErrorCircle20Filled, Info20Filled, Warning20Filled } from '@fluentui/react-icons';
import { useIntl } from 'react-intl';
import { Notification, NotificationStatus } from '../../Common/Contracts/Notification';
import { useRelativeTime } from '../../Common/Hooks/useRelativeTime';
import { PortalResources } from '../../Strings/Resources';

const useStyles = makeStyles({
    card: {
        display: 'flex',
        flexDirection: 'row',
        gap: tokens.spacingHorizontalM,
        padding: tokens.spacingVerticalM,
        borderRadius: tokens.borderRadiusMedium,
        backgroundColor: tokens.colorNeutralBackground1,
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground1Hover,
        },
    },
    iconContainer: {
        display: 'flex',
        alignItems: 'flex-start',
        paddingTop: '2px',
    },
    content: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
        flex: 1,
        minWidth: 0,
    },
    title: {
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
    },
    description: {
        color: tokens.colorNeutralForeground2,
        fontSize: tokens.fontSizeBase200,
    },
    timestamp: {
        color: tokens.colorNeutralForeground3,
        fontSize: tokens.fontSizeBase200,
        textAlign: 'right',
    },
    dismissButton: {
        minWidth: 'auto',
        alignSelf: 'flex-start',
    },
});

const getStatusIcon = (status: NotificationStatus) => {
    switch (status) {
        case 'in-progress':
            return <Spinner size="tiny" />;
        case 'success':
            return <CheckmarkCircle20Filled style={{ color: tokens.colorPaletteGreenForeground1 }} />;
        case 'error':
            return <ErrorCircle20Filled style={{ color: tokens.colorPaletteRedForeground1 }} />;
        case 'warning':
            return <Warning20Filled style={{ color: tokens.colorPaletteYellowForeground1 }} />;
        case 'info':
            return <Info20Filled style={{ color: tokens.colorBrandForeground1 }} />;
    }
};

interface NotificationCardProps {
    notification: Notification;
    onDismiss: (id: string) => void;
}

export const NotificationCard = ({ notification, onDismiss }: NotificationCardProps) => {
    const intl = useIntl();
    const styles = useStyles();
    const relativeTime = useRelativeTime(notification.timestamp);

    return (
        <div className={styles.card}>
            <div className={styles.iconContainer}>{getStatusIcon(notification.status)}</div>
            <div className={styles.content}>
                <Text className={styles.title}>{notification.title}</Text>
                {notification.description && <Text className={styles.description}>{notification.description}</Text>}
                <Text className={styles.timestamp}>{relativeTime}</Text>
            </div>
            <Tooltip content={intl.formatMessage(PortalResources.dismiss)} relationship="label">
                <Button
                    icon={<Dismiss20Regular />}
                    appearance="subtle"
                    size="small"
                    className={styles.dismissButton}
                    onClick={() => onDismiss(notification.id)}
                    aria-label={intl.formatMessage(PortalResources.dismiss)}
                />
            </Tooltip>
        </div>
    );
};
