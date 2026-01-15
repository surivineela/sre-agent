import { Button, Dialog, DialogActions, DialogBody, DialogContent, DialogSurface, DialogTitle } from '@fluentui/react-components';
import { useCallback } from 'react';
import { useIntl } from 'react-intl';
import { PortalResources } from '../../../Strings/Resources';

interface RemoveAgentDialogProps {
    open: boolean;
    agentNames: string[];
    onClose: () => void;
    onConfirm: () => void;
}

export const RemoveAgentDialog = ({ open, agentNames, onClose, onConfirm }: RemoveAgentDialogProps) => {
    const intl = useIntl();

    const handleConfirm = useCallback(() => {
        onConfirm();
        onClose();
    }, [onConfirm, onClose]);

    const maxDisplayedAgents = 5;
    const displayedAgents = agentNames.slice(0, maxDisplayedAgents);
    const remainingCount = agentNames.length - maxDisplayedAgents;

    return (
        <Dialog open={open} onOpenChange={(_, data) => !data.open && onClose()}>
            <DialogSurface>
                <DialogBody>
                    <DialogTitle>{intl.formatMessage(PortalResources.removeAgentFromSpaceTitle)}</DialogTitle>
                    <DialogContent>
                        <div>{intl.formatMessage(PortalResources.removeAgentFromSpaceConfirmation, { count: agentNames.length })}</div>
                        {agentNames.length <= maxDisplayedAgents && (
                            <ul>
                                {displayedAgents.map(name => (
                                    <li key={name}>{name}</li>
                                ))}
                            </ul>
                        )}
                        {agentNames.length > maxDisplayedAgents && (
                            <ul>
                                {displayedAgents.map(name => (
                                    <li key={name}>{name}</li>
                                ))}
                                <li>{intl.formatMessage(PortalResources.andMore, { count: remainingCount })}</li>
                            </ul>
                        )}
                    </DialogContent>
                    <DialogActions>
                        <Button appearance="secondary" onClick={onClose}>
                            {intl.formatMessage(PortalResources.cancel)}
                        </Button>
                        <Button appearance="primary" onClick={handleConfirm}>
                            {intl.formatMessage(PortalResources.remove)}
                        </Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};
