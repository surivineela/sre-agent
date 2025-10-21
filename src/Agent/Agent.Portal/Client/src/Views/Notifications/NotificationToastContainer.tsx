import { Toast, ToastBody, Toaster, ToastTitle, useId, useToastController } from '@fluentui/react-components';
import { useEffect, useRef } from 'react';
import { useNotifications } from '../../Common/Contexts/NotificationContext';

// TODO: If possible, brand-color the info icon, and use spinner in place of icon for in-progress

const AUTO_DISMISS_TIMEOUT = 4000; // 4 seconds

export const NotificationToastContainer = () => {
    const toasterId = useId('notification-toaster');
    const { dispatchToast } = useToastController(toasterId);
    const { notifications } = useNotifications();
    const shownNotificationsRef = useRef<Set<string>>(new Set());

    useEffect(() => {
        // Find new notifications that haven't been shown as toasts yet
        const newNotifications = notifications.filter(n => !shownNotificationsRef.current.has(n.id));

        newNotifications.forEach(notification => {
            shownNotificationsRef.current.add(notification.id);

            // Determine toast intent based on notification status
            let intent: 'success' | 'error' | 'warning' | 'info' = 'info';
            if (notification.status === 'success') intent = 'success';
            else if (notification.status === 'error') intent = 'error';
            else if (notification.status === 'warning') intent = 'warning';

            dispatchToast(
                <Toast>
                    <ToastTitle>{notification.title}</ToastTitle>
                    {notification.description && <ToastBody>{notification.description}</ToastBody>}
                </Toast>,
                {
                    intent,
                    timeout: AUTO_DISMISS_TIMEOUT,
                    toastId: notification.id,
                }
            );
        });

        // Clean up shown notifications that no longer exist
        const currentIds = new Set(notifications.map(n => n.id));
        shownNotificationsRef.current.forEach(id => {
            if (!currentIds.has(id)) {
                shownNotificationsRef.current.delete(id);
            }
        });
    }, [notifications, dispatchToast]);

    return <Toaster toasterId={toasterId} position="top-end" pauseOnHover pauseOnWindowBlur offset={{ horizontal: 16, vertical: 48 }} />;
};
