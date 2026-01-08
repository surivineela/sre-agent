import { Button, Dialog, DialogActions, DialogSurface } from '@fluentui/react-components';
import { FC } from 'react';
import { ToolsPicker } from '../../Common/ToolsPicker/ToolsPicker';
import { UseToolsPickerReturn } from '../../Common/ToolsPicker/useToolsPicker';

export const ToolsPickerDialog: FC<UseToolsPickerReturn & { open: boolean; onClose: () => void }> = ({
    open,
    onClose,
    ...toolsPickerProps
}) => {
    return (
        <Dialog open={open} modalType="modal">
            <DialogSurface
                style={{ display: 'flex', flexDirection: 'column', height: '80vh', maxHeight: 'unset', maxWidth: '80vw', gap: '16px' }}
            >
                <ToolsPicker {...toolsPickerProps} />
                <DialogActions>
                    <Button appearance="primary" style={{ marginLeft: 'auto' }} onClick={() => onClose()}>
                        {'Done'}
                    </Button>
                </DialogActions>
            </DialogSurface>
        </Dialog>
    );
};
