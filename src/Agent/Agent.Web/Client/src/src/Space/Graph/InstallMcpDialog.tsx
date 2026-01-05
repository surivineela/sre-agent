import { Button, Dialog, DialogActions, DialogBody, DialogContent, DialogSurface, DialogTitle } from '@fluentui/react-components';
import { useIntl } from 'react-intl';
import ReactMarkdownComponent from '../../Common/Components/ReactMarkdownComponent';
import { ExtendedAgentsGraphResources } from '../../Strings/SREAgentResources';

interface InstallMcpDialogProps {
    isOpen: boolean;
    onOpenChange: (open: boolean) => void;
}

export const InstallMcpDialog = ({ isOpen, onOpenChange }: InstallMcpDialogProps) => {
    const intl = useIntl();
    const vscodeMcp = { name: 'sre-agent-mcp', command: 'srectl', args: ['mcp', 'start'] };
    const mcpInstallUrl = `vscode:mcp/install?${encodeURIComponent(JSON.stringify(vscodeMcp))}`;

    return (
        <Dialog open={isOpen} onOpenChange={(_, data) => onOpenChange(data.open)}>
            <DialogSurface>
                <DialogBody>
                    <DialogTitle>{intl.formatMessage(ExtendedAgentsGraphResources.installMcpDialogTitle)}</DialogTitle>
                    <DialogContent>
                        <ReactMarkdownComponent content={intl.formatMessage(ExtendedAgentsGraphResources.installMcpDialogDescription)} />
                    </DialogContent>
                    <DialogActions>
                        <Button as="a" href={mcpInstallUrl} appearance="primary" onClick={() => onOpenChange(false)}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.installMcpDialogInstallButton)}
                        </Button>
                        <Button appearance="secondary" onClick={() => onOpenChange(false)}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.installMcpDialogCloseButton)}
                        </Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};
