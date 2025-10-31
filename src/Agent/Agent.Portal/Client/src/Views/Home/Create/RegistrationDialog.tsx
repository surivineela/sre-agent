import { Button, Dialog, DialogActions, DialogBody, DialogSurface, DialogTitle } from '@fluentui/react-components';
import { useIntl } from 'react-intl';
import { PortalResources } from '../../../Strings/Resources';

// TODO: Better styling & accessibility

interface RegistrationDialogProps {
    open: boolean;
    setOpen: (open: boolean) => void;
    onResolve: (result: boolean) => void;
}

const RegistrationDialog = ({ open, setOpen, onResolve }: RegistrationDialogProps) => {
    const intl = useIntl();

    const handleClose = (result: boolean) => {
        onResolve(result);
        setOpen(false);
    };

    return (
        <Dialog open={open} aria-labelledby="registration-dialog-title">
            <DialogSurface>
                <DialogBody style={{ display: 'flex', flexDirection: 'column', gap: '20px', fontFamily: 'Segoe UI, sans-serif' }}>
                    <DialogTitle id="registration-dialog-title">{PortalResources.registrationFailed}</DialogTitle>
                    <div style={{ fontSize: '14px' }} role="alert" aria-live="assertive">
                        {intl.formatMessage(PortalResources.registrationFailedMessage)}
                    </div>
                    <div style={{ fontSize: '14px' }}>{intl.formatMessage(PortalResources.doYouWantToContinue)}</div>

                    <DialogActions style={{ display: 'flex', justifyContent: 'flex-end' }}>
                        <Button appearance="primary" onClick={() => handleClose(false)}>
                            {intl.formatMessage(PortalResources.no)}
                        </Button>
                        <Button onClick={() => handleClose(true)}>{intl.formatMessage(PortalResources.yes)}</Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};

export default RegistrationDialog;
