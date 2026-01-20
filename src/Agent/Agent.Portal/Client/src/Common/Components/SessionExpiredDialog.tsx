import {
    Button,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    makeStyles,
    tokens,
} from '@fluentui/react-components';
import { LockClosedRegular } from '@fluentui/react-icons';
import { useIntl } from 'react-intl';
import { PortalResources } from '../../Strings/Resources';
import { useAuth } from '../Contexts/AuthContext';

const useStyles = makeStyles({
    dialogSurface: {
        maxWidth: '420px',
        padding: tokens.spacingVerticalL,
    },
    content: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        textAlign: 'center',
        gap: tokens.spacingVerticalL,
        paddingTop: tokens.spacingVerticalL,
        paddingBottom: tokens.spacingVerticalL,
    },
    icon: {
        fontSize: '56px',
        color: tokens.colorBrandForeground1,
    },
    message: {
        color: tokens.colorNeutralForeground2,
        lineHeight: tokens.lineHeightBase400,
        margin: 0,
    },
    actions: {
        justifyContent: 'center',
        gap: tokens.spacingHorizontalM,
        paddingTop: tokens.spacingVerticalS,
    },
    button: {
        minWidth: '120px',
    },
});

interface SessionExpiredDialogProps {
    open: boolean;
}

export const SessionExpiredDialog = ({ open }: SessionExpiredDialogProps) => {
    const styles = useStyles();
    const intl = useIntl();
    const { signIn, signOut } = useAuth();

    return (
        <Dialog
            open={open}
            modalType="alert"
            // Prevent closing via escape or backdrop click - user must take action
            onOpenChange={() => {}}
        >
            <DialogSurface className={styles.dialogSurface} aria-describedby="session-expired-message">
                <DialogBody>
                    <DialogTitle>{intl.formatMessage(PortalResources.sessionExpired)}</DialogTitle>
                    <DialogContent className={styles.content}>
                        <LockClosedRegular className={styles.icon} aria-hidden="true" />
                        <p id="session-expired-message" className={styles.message} role="alert" aria-live="assertive">
                            {intl.formatMessage(PortalResources.sessionExpiredMessage)}
                        </p>
                    </DialogContent>
                    <DialogActions className={styles.actions}>
                        <Button className={styles.button} onClick={() => signOut()}>
                            {intl.formatMessage(PortalResources.signOut)}
                        </Button>
                        <Button appearance="primary" className={styles.button} onClick={() => signIn()}>
                            {intl.formatMessage(PortalResources.signInAgain)}
                        </Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};
