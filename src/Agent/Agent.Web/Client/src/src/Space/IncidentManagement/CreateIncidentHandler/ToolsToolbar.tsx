import {
    Button,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    DialogTrigger,
} from '@fluentui/react-components';
import { Add16Regular, ArrowClockwise16Regular } from '@fluentui/react-icons';
import { FC } from 'react';
import { useIntl } from 'react-intl';
import { IncidentHandlerCreateResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import { useIncidentManagementStyles } from '../../Styles/IncidentManagement.styles';

export interface ToolsToolbarProps {
    onUpdateToolsClick: () => void;
    onAddClick: () => void;
    disabled: boolean;
}

export const ToolsToolbar: FC<ToolsToolbarProps> = ({ onUpdateToolsClick, onAddClick, disabled }) => {
    const intl = useIntl();
    const styles = useIncidentManagementStyles();

    return (
        <div className={styles.toolsToolbar}>
            <Dialog modalType="alert">
                <DialogTrigger disableButtonEnhancement>
                    <Button icon={<ArrowClockwise16Regular />} appearance="transparent" className={styles.button} disabled={disabled}>
                        {intl.formatMessage(IncidentHandlerCreateResources.regenerateTools)}
                    </Button>
                </DialogTrigger>
                <DialogSurface>
                    <DialogBody>
                        <DialogTitle>{intl.formatMessage(IncidentHandlerCreateResources.regenerateToolsConfirmationTitle)}</DialogTitle>
                        <DialogContent>
                            {intl.formatMessage(IncidentHandlerCreateResources.regenerateToolsConfirmationMessage)}
                        </DialogContent>
                        <DialogActions>
                            <DialogTrigger>
                                <Button className={styles.dangerButton} onClick={() => onUpdateToolsClick()}>
                                    {intl.formatMessage(SreAgentResources.yes)}
                                </Button>
                            </DialogTrigger>
                            <DialogTrigger disableButtonEnhancement>
                                <Button appearance="secondary">{intl.formatMessage(SreAgentResources.no)}</Button>
                            </DialogTrigger>
                        </DialogActions>
                    </DialogBody>
                </DialogSurface>
            </Dialog>
            <Button
                icon={<Add16Regular />}
                appearance="transparent"
                className={styles.button}
                onClick={() => onAddClick()}
                disabled={disabled}
            >
                {intl.formatMessage(IncidentHandlerCreateResources.manageTools)}
            </Button>
        </div>
    );
};
