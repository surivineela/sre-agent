import {
    Button,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTrigger,
    Field,
    Input,
    InputOnChangeData,
} from '@fluentui/react-components';
import { memo, useEffect, useState } from 'react';
import { useIntl } from 'react-intl';
import { SreAgentResources } from '../../Strings/SREAgentResources';
import { SpecialControlValue } from '../AzPortalProxy/Models/IAmplitude';
import { useAzPortalContext } from '../AzPortalProxy/Providers/AzPortalProxyContext';
import { Thread } from '../Contracts/DataPlane/Thread';
import { useDialogStyles } from './Dialog.styles';

interface RenameThreadDialogProps {
    thread: Thread;
    isOpen: boolean;
    onOpenChange: (open: boolean) => void;
    onUpdateThreadTitle: (newTitle: string) => void;
}

interface FieldState {
    title: string;
    isDirty: boolean;
    errorMessage?: string;
}

const RenameThreadDialog = ({ thread, isOpen, onOpenChange, onUpdateThreadTitle }: RenameThreadDialogProps) => {
    const { dialogSurface } = useDialogStyles();
    const intl = useIntl();
    const { logAmplitudeControlEvent } = useAzPortalContext();

    const [fieldState, setFieldState] = useState<FieldState>({
        title: thread.title,
        isDirty: false,
        errorMessage: undefined,
    });

    const onThreadTitleInputChange = (data: InputOnChangeData) => {
        setFieldState({
            title: data.value || '',
            isDirty: data.value !== thread.title,
            errorMessage: !data.value ? intl.formatMessage(SreAgentResources.threadTitleEmptyError) : undefined,
        });
    };

    useEffect(() => {
        setFieldState(prev => ({
            ...prev,
            title: thread.title,
        }));
    }, [thread.title]);

    return (
        <Dialog open={isOpen} onOpenChange={(_, data) => onOpenChange(data.open)}>
            <DialogSurface mountNode={{ className: dialogSurface }}>
                <DialogBody>
                    <DialogContent>
                        <Field
                            label={intl.formatMessage(SreAgentResources.renameFieldLabel)}
                            size="medium"
                            validationState={fieldState.errorMessage ? 'error' : undefined}
                            validationMessage={fieldState.errorMessage}
                        >
                            <Input value={fieldState.title} onChange={(_, data) => onThreadTitleInputChange(data)} />
                        </Field>
                    </DialogContent>
                    <DialogActions>
                        <Button
                            appearance={'primary'}
                            onClick={() => {
                                logAmplitudeControlEvent({
                                    targetType: 'button',
                                    targetAction: 'clicked',
                                    targetName: 'Rename thread title',
                                    targetFriendlyName: 'Rename thread title',
                                    valueObjectName: SpecialControlValue.DoAction,
                                    valueObjectFriendlyName: SpecialControlValue.DoAction,
                                });
                                onUpdateThreadTitle(fieldState.title);
                            }}
                            disabled={!!fieldState.errorMessage || !fieldState.isDirty}
                        >
                            {intl.formatMessage(SreAgentResources.save)}
                        </Button>
                        <DialogTrigger disableButtonEnhancement>
                            <Button appearance="secondary">{intl.formatMessage(SreAgentResources.cancel)}</Button>
                        </DialogTrigger>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};

export default memo(RenameThreadDialog);
