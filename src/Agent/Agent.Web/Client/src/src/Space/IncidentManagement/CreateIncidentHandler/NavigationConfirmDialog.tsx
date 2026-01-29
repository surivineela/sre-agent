import { FC, useMemo } from 'react';
import { useBlocker } from 'react-router';
import { DirtyStateConfirmationDialog } from './DirtyStateConfirmationDialog';

export const DirtyStateNavigationConfirmDialog: FC<{ isDirty: boolean }> = ({ isDirty }) => {
    const blocker = useBlocker(isDirty);
    const onConfirm = useMemo(() => blocker?.proceed || (() => {}), [blocker?.proceed]);
    const onCancel = useMemo(() => blocker?.reset || (() => {}), [blocker?.reset]);

    return (
        <DirtyStateConfirmationDialog isDirty={isDirty} onConfirm={onConfirm} onCancel={onCancel} condition={blocker.state === 'blocked'} />
    );
};
