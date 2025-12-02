import { Button, Dialog, DialogActions, DialogBody, DialogContent, DialogSurface, DialogTitle } from '@fluentui/react-components';
import { useCallback } from 'react';
import { useIntl } from 'react-intl';
import { SreAgentArgItem } from '../../Common/Contracts/SreAgent';
import { PortalResources } from '../../Strings/Resources';

interface DeleteAgentDialogProps {
    open: boolean;
    selectedAgents: SreAgentArgItem[];
    onClose: () => void;
    onConfirm: () => void;
}

export const DeleteAgentDialog = ({ open, selectedAgents, onClose, onConfirm }: DeleteAgentDialogProps) => {
    const intl = useIntl();

    const handleConfirm = useCallback(() => {
        onConfirm();
        onClose();
    }, [onConfirm, onClose]);

    const isSingleAgent = selectedAgents.length === 1;
    const title = isSingleAgent
        ? intl.formatMessage(PortalResources.deleteAgentTitle)
        : intl.formatMessage(PortalResources.deleteAgentsTitle);

    const content = isSingleAgent
        ? intl.formatMessage(PortalResources.deleteAgentContent, { name: selectedAgents[0]?.name || '' })
        : intl.formatMessage(PortalResources.deleteAgentsContent, { count: selectedAgents.length });

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
