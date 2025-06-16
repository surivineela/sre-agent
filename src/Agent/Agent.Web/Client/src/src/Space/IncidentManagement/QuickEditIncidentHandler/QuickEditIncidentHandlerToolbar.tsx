import {
    Button,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    DialogTrigger,
    InfoLabel,
} from '@fluentui/react-components';
import { ArrowDown20Regular, Delete20Regular, Info12Regular, PenSparkle20Regular } from '@fluentui/react-icons';
import { FC } from 'react';
import { useIntl } from 'react-intl';
import { IncidentHandlerCreateResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import { useIncidentManagementStyles } from '../../Styles/IncidentManagement.styles';
export type QuickEditIncidentHandlerToolbarProps = {
    onRegenerateClick: () => void;
    onExportClick: () => void;
    onDeleteClick: () => void;
};

const QuickEditIncidentHandlerToolbar: FC<QuickEditIncidentHandlerToolbarProps> = ({ onRegenerateClick, onExportClick, onDeleteClick }) => {
    const intl = useIntl();
    const styles = useIncidentManagementStyles();

    return (
        <div
            style={{
                display: 'flex',
                justifyContent: 'start',
                alignItems: 'center',
                gap: 8,
                marginTop: 20,
                marginLeft: 30,
            }}
        >
            <InfoLabel
                info={intl.formatMessage(IncidentHandlerCreateResources.regenerateTooltip)}
                infoButton={<Info12Regular />}
                className={styles.infoButton}
            >
                <Button
                    icon={<PenSparkle20Regular />}
                    appearance="transparent"
                    className={styles.button}
                    onClick={() => onRegenerateClick()}
                >
                    {intl.formatMessage(IncidentHandlerCreateResources.regenerate)}
                </Button>
            </InfoLabel>

            <Button icon={<ArrowDown20Regular />} appearance="transparent" className={styles.button} onClick={() => onExportClick()}>
                {intl.formatMessage(IncidentHandlerCreateResources.export)}
            </Button>
            <div className={styles.divider} />
            <Dialog modalType="alert">
                <DialogTrigger disableButtonEnhancement>
                    <Button icon={<Delete20Regular />} appearance="transparent" className={styles.button}>
                        {intl.formatMessage(SreAgentResources.delete)}
                    </Button>
                </DialogTrigger>
                <DialogSurface>
                    <DialogBody>
                        <DialogTitle>{intl.formatMessage(IncidentHandlerCreateResources.customHandlerDeleteConfirmationTitle)}</DialogTitle>
                        <DialogContent>
                            {intl.formatMessage(IncidentHandlerCreateResources.customHandlerDeleteConfirmationMessage)}
                        </DialogContent>
                        <DialogActions>
                            <DialogTrigger>
                                <Button className={styles.dangerButton} onClick={() => onDeleteClick()}>
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
        </div>
    );
};

export default QuickEditIncidentHandlerToolbar;
