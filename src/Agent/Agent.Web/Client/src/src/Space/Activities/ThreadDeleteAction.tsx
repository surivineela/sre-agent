import { memo } from "react";
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
    tokens
} from "@fluentui/react-components";
import { DeleteRegular, MoreHorizontal20Regular } from "@fluentui/react-icons";
import {
    SreAgentResources,
    Activities_ThreadHeader as ThreadHeaderResources
} from "../../Strings/SREResources.resjson";

const useStyles = makeStyles({
    dangerButton: {
        backgroundColor: tokens.colorStatusDangerBackground3,
        color: `${tokens.colorNeutralForegroundInverted} !important`,
        ':hover': {
            backgroundColor: tokens.colorStatusDangerBackground3Hover,
        },
        ':active': {
            backgroundColor: tokens.colorStatusDangerBackground3Pressed
        },
    },
});

const ThreadDeleteAction = ({ handleThreadDelete }: { handleThreadDelete: () => void }) => {

    const { dangerButton } = useStyles();

    return (
        <Dialog modalType="alert">
            <Menu>
                <MenuTrigger >
                    <Button
                        style={{ display: 'inline-block' }}
                        appearance="transparent"
                        icon={<MoreHorizontal20Regular />}
                        aria-label="More options"
                    />
                </MenuTrigger>
                <MenuPopover>
                    <MenuList>
                        < DialogTrigger disableButtonEnhancement >
                            <MenuItem icon={<DeleteRegular />}>{'Delete'}</MenuItem>
                        </DialogTrigger >
                    </MenuList>

                </MenuPopover>
            </Menu>

            <DialogSurface>
                <DialogBody>
                    <DialogTitle>{ThreadHeaderResources.deleteThreadDialogTitle}</DialogTitle>
                    <DialogContent>{ThreadHeaderResources.deleteThreadDialogDescription}</DialogContent>
                    <DialogActions>
                        <DialogTrigger>
                            <Button className={dangerButton} onClick={() => handleThreadDelete()}>{SreAgentResources.yes}</Button>
                        </DialogTrigger>
                        <DialogTrigger disableButtonEnhancement>
                            <Button appearance="secondary">{SreAgentResources.no}</Button>
                        </DialogTrigger>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog >
    )
};

export default memo(ThreadDeleteAction);