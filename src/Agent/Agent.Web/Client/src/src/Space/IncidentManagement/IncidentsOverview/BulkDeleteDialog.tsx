import {
    Button,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    DialogTrigger,
    makeStyles,
    tokens,
} from '@fluentui/react-components';
import { Delete16Regular } from '@fluentui/react-icons';
import { FC } from 'react';
import { useIntl } from 'react-intl';
import { Thread } from '../../../Common/Contracts/DataPlane/Thread';
import { ActivitiesThreadHeaderResources, IncidentManagementResources, SreAgentResources } from '../../../Strings/SREAgentResources';

const useStyles = makeStyles({
    dangerButton: {
        backgroundColor: tokens.colorStatusDangerBackground3,
        color: `${tokens.colorNeutralForegroundInverted} !important`,
        ':hover': {
            backgroundColor: tokens.colorStatusDangerBackground3Hover,
        },
        ':active': {
            backgroundColor: tokens.colorStatusDangerBackground3Pressed,
        },
    },
});

interface BulkDeleteDialogProps {
    isOpen: boolean;
    onOpenChange: (open: boolean) => void;
    selectedThreads: Set<string>;
    incidentThreads: Thread[];
    onConfirmDelete: () => void;
    disabled?: boolean;
    className?: string;
}

export const BulkDeleteDialog: FC<BulkDeleteDialogProps> = ({
    isOpen,
    onOpenChange,
    selectedThreads,
    incidentThreads,
    onConfirmDelete,
    disabled,
    className,
}) => {
    const intl = useIntl();
    const { dangerButton } = useStyles();

    const getDialogTitle = () => {
        if (selectedThreads.size === 1) {
            const thread = incidentThreads.find(t => selectedThreads.has(t.id));
            return intl.formatMessage(ActivitiesThreadHeaderResources.deleteIncidentTitle, {
                title: thread?.title || 'incident',
            });
        }
        return intl.formatMessage(ActivitiesThreadHeaderResources.deleteMultipleIncidentsTitle, {
            count: selectedThreads.size,
        });
    };

    const getDialogContent = () => {
        if (selectedThreads.size === 1) {
            return intl.formatMessage(IncidentManagementResources.deleteIncidentThreadConfirmation);
        }
        return intl.formatMessage(IncidentManagementResources.deleteIncidentThreadsConfirmation);
    };

    return (
        <Dialog open={isOpen} onOpenChange={(_, data) => onOpenChange(data.open)}>
            <DialogTrigger disableButtonEnhancement>
                <Button
                    icon={<Delete16Regular />}
                    appearance="transparent"
                    className={className}
                    disabled={disabled || selectedThreads.size === 0}
                >
                    {intl.formatMessage(SreAgentResources.delete)}
                </Button>
            </DialogTrigger>
            <DialogSurface>
                <DialogBody>
                    <DialogTitle>{getDialogTitle()}</DialogTitle>
                    <DialogContent>{getDialogContent()}</DialogContent>
                    <DialogActions>
                        <DialogTrigger disableButtonEnhancement>
                            <Button className={dangerButton} onClick={onConfirmDelete}>
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
    );
};
