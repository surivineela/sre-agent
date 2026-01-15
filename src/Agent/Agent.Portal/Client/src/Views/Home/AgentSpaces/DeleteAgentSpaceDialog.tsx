import { Button, Dialog, DialogActions, DialogBody, DialogContent, DialogSurface, DialogTitle } from '@fluentui/react-components';
import { useCallback } from 'react';
import { useIntl } from 'react-intl';
import { AgentSpaceArgItem } from '../../../Common/Contracts/AgentSpace';
import { PortalResources } from '../../../Strings/Resources';

interface DeleteAgentSpaceDialogProps {
    open: boolean;
    selectedSpaces: AgentSpaceArgItem[];
    onClose: () => void;
    onConfirm: () => void;
}

export const DeleteAgentSpaceDialog = ({ open, selectedSpaces, onClose, onConfirm }: DeleteAgentSpaceDialogProps) => {
    const intl = useIntl();

    const handleConfirm = useCallback(() => {
        onConfirm();
        onClose();
    }, [onConfirm, onClose]);

    const isSingleSpace = selectedSpaces.length === 1;
    const title = intl.formatMessage(PortalResources.deleteAgentSpace);

    const content = isSingleSpace
        ? intl.formatMessage(PortalResources.deleteAgentSpaceConfirmation)
        : intl.formatMessage(PortalResources.deleteAgentSpaceConfirmation);

    return (
        <Dialog open={open} onOpenChange={(_, data) => !data.open && onClose()}>
            <DialogSurface>
                <DialogBody>
                    <DialogTitle>{title}</DialogTitle>
                    <DialogContent>{content}</DialogContent>
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
