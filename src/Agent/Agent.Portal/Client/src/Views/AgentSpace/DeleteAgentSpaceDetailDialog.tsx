import { Button, Dialog, DialogActions, DialogBody, DialogContent, DialogSurface, DialogTitle } from '@fluentui/react-components';
import { useCallback } from 'react';
import { useIntl } from 'react-intl';
import { PortalResources } from '../../Strings/Resources';

interface DeleteAgentSpaceDetailDialogProps {
    open: boolean;
    onClose: () => void;
    onConfirm: () => void;
}

export const DeleteAgentSpaceDetailDialog = ({ open, onClose, onConfirm }: DeleteAgentSpaceDetailDialogProps) => {
    const intl = useIntl();

    const handleConfirm = useCallback(() => {
        onConfirm();
        onClose();
    }, [onConfirm, onClose]);

    return (
        <Dialog open={open} onOpenChange={(_, data) => !data.open && onClose()}>
            <DialogSurface>
                <DialogBody>
                    <DialogTitle>{intl.formatMessage(PortalResources.deleteAgentSpace)}</DialogTitle>
                    <DialogContent>{intl.formatMessage(PortalResources.deleteAgentSpaceConfirmation)}</DialogContent>
                    <DialogActions>
                        <Button appearance="secondary" onClick={onClose}>
                            {intl.formatMessage(PortalResources.cancel)}
                        </Button>
                        <Button appearance="primary" onClick={handleConfirm}>
                            {intl.formatMessage(PortalResources.delete)}
                        </Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};
