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
import { Add16Regular, ArrowClockwise16Regular, Delete16Regular } from '@fluentui/react-icons';
import { FC } from 'react';
import { useIntl } from 'react-intl';
import { IncidentHandlerCreateResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import { useIncidentManagementStyles } from '../../Styles/IncidentManagement.styles';

export interface ToolsToolbarProps {
    onUpdateToolsClick: () => void;
    onAddClick: () => void;
    onDeleteClick: () => void;
    disabled: boolean;
    addDisabled?: boolean;
    hasToolsSelected: boolean;
}

export const ToolsToolbar: FC<ToolsToolbarProps> = ({
    onUpdateToolsClick,
    onAddClick,
    onDeleteClick,
    disabled,
    hasToolsSelected,
    addDisabled,
}) => {
    const intl = useIntl();
    const styles = useIncidentManagementStyles();

    return (
        <div className={styles.toolsToolbar}>
            <Dialog modalType="alert">
                <DialogTrigger disableButtonEnhancement>
                    <Button icon={<ArrowClockwise16Regular />} appearance="transparent" className={styles.button} disabled={disabled}>
                        {intl.formatMessage(IncidentHandlerCreateResources.updateTools)}
                    </Button>
                </DialogTrigger>
                <DialogSurface>
                    <DialogBody>
                        <DialogTitle>{intl.formatMessage(IncidentHandlerCreateResources.updateToolsConfirmationTitle)}</DialogTitle>
                        <DialogContent>{intl.formatMessage(IncidentHandlerCreateResources.updateToolsConfirmationMessage)}</DialogContent>
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
                disabled={disabled || addDisabled}
            >
                {intl.formatMessage(SreAgentResources.add)}
            </Button>
            <div className={styles.divider} />
            <Button
                icon={<Delete16Regular />}
                appearance="transparent"
                className={styles.button}
                onClick={() => onDeleteClick()}
                disabled={disabled || !hasToolsSelected}
            >
                {intl.formatMessage(SreAgentResources.delete)}
            </Button>
        </div>
    );
};
