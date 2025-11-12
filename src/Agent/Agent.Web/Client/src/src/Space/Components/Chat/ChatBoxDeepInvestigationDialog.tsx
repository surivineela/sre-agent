import { tokens } from '@fluentui-copilot/react-copilot';
import {
    Button,
    Checkbox,
    CheckboxProps,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    DialogTrigger,
    makeStyles,
} from '@fluentui/react-components';
import { memo, useEffect, useState } from 'react';
import { useIntl } from 'react-intl';
import { AgentTaskResources, SreAgentResources } from '../../../Strings/SREAgentResources';

interface IChatBoxDeepInvestigationDialogProps {
    onClickDeepInvestigationDialogActionButton: (dismissDialog: boolean, yes: boolean) => void;
    isOpen: boolean;
    setIsOpen: (visible: boolean) => void;
}

const useStyles = makeStyles({
    surface: {
        padding: `${tokens.spacingVerticalL} ${tokens.spacingHorizontalL}`,
        borderRadius: tokens.borderRadius2XL,
        border: `1px solid ${tokens.colorTransparentStroke}`,
        maxHeight: '100dvh',
        boxShadow: tokens.shadow64,
    },
    checkbox: {
        marginTop: tokens.spacingVerticalM,
        marginLeft: `calc(-1 * ${tokens.spacingHorizontalS})`,
    },
});

const ChatBoxDeepInvestigationDialog = (props: IChatBoxDeepInvestigationDialogProps) => {
    const intl = useIntl();

    const styles = useStyles();

    const [isDialogDismissedChecked, setIsDialogDismissedChecked] = useState<CheckboxProps['checked']>(false);

    useEffect(() => {
        setIsDialogDismissedChecked(false);
    }, [props.isOpen]);

    return (
        <Dialog open={props.isOpen} onOpenChange={(_, data) => props.setIsOpen(data.open)} modalType={'alert'}>
            <DialogSurface className={styles.surface}>
                <DialogBody>
                    <DialogTitle>{intl.formatMessage(AgentTaskResources.deepInvestigation)}</DialogTitle>
                    <DialogContent>
                        <div>
                            {intl.formatMessage(AgentTaskResources.deepInvestigationWarning)}{' '}
                            {intl.formatMessage(SreAgentResources.doYouWantToProceed)}
                        </div>
                        <div className={styles.checkbox}>
                            <Checkbox
                                checked={isDialogDismissedChecked}
                                onChange={(_, data) => setIsDialogDismissedChecked(data.checked)}
                                label={{
                                    children: intl.formatMessage(AgentTaskResources.deepInvestigationDismissCheckboxLabel),
                                    style: { lineHeight: tokens.lineHeightBase300 },
                                }}
                            />
                        </div>
                    </DialogContent>

                    <DialogActions fluid>
                        <DialogTrigger>
                            <Button
                                appearance={'primary'}
                                onClick={() => props.onClickDeepInvestigationDialogActionButton(!!isDialogDismissedChecked, true)}
                            >
                                {intl.formatMessage(SreAgentResources.yes)}
                            </Button>
                        </DialogTrigger>
                        <DialogTrigger>
                            <Button onClick={() => props.onClickDeepInvestigationDialogActionButton(!!isDialogDismissedChecked, false)}>
                                {intl.formatMessage(SreAgentResources.no)}
                            </Button>
                        </DialogTrigger>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};

export default memo(ChatBoxDeepInvestigationDialog);
