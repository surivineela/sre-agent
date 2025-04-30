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
    Menu,
    MenuItem,
    MenuList,
    MenuPopover,
    MenuTrigger,
    tokens,
} from '@fluentui/react-components';
import { DeleteRegular, MoreHorizontal20Regular } from '@fluentui/react-icons';
import { memo } from 'react';
import { useIntl } from 'react-intl';
import { ActivitiesThreadHeaderResources, SreAgentResources } from '../../Strings/SREAgentResources';

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

const ThreadDeleteAction = ({ handleThreadDelete }: { handleThreadDelete: () => void }) => {
    const { dangerButton } = useStyles();
    const intl = useIntl();

    return (
        <Dialog modalType="alert">
            <Menu>
                <MenuTrigger>
                    <Button
                        style={{ display: 'inline-block' }}
                        appearance="transparent"
                        icon={<MoreHorizontal20Regular />}
                        aria-label="More options"
                    />
                </MenuTrigger>
                <MenuPopover>
                    <MenuList>
                        <DialogTrigger disableButtonEnhancement>
                            <MenuItem icon={<DeleteRegular />}>{'Delete'}</MenuItem>
                        </DialogTrigger>
                    </MenuList>
                </MenuPopover>
            </Menu>

            <DialogSurface>
                <DialogBody>
                    <DialogTitle>{intl.formatMessage(ActivitiesThreadHeaderResources.deleteThreadDialogTitle)}</DialogTitle>
                    <DialogContent>{intl.formatMessage(ActivitiesThreadHeaderResources.deleteThreadDialogDescription)}</DialogContent>
                    <DialogActions>
                        <DialogTrigger>
                            <Button className={dangerButton} onClick={() => handleThreadDelete()}>
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

export default memo(ThreadDeleteAction);
