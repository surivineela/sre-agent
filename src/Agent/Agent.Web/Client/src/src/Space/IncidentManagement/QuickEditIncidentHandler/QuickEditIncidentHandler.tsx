import { Button } from '@fluentui/react-components';
import { FC, useContext, useEffect } from 'react';
import { useIntl } from 'react-intl';
import { IncidentHandlerCreateResources } from '../../../Strings/SREAgentResources';
import { HandlerEditor } from '../CreateIncidentHandler/Common/HandlerEditor';
import { DirtyStateConfirmationWrapper } from '../CreateIncidentHandler/DirtyStateConfirmationDialog';
import { IncidentHandlerCreateContext } from '../CreateIncidentHandler/IncidentHandlerCreateContext';
import QuickEditIncidentHandlerToolbar from './QuickEditIncidentHandlerToolbar';

export const QuickEditIncidentHandler: FC = () => {
    const {
        isDirty,
        editorDisplayValue,
        onEditorValueChange,
        setIsEditorValueValid,
        isEditorValueValid,
        exitToHome,
        goToFullEditMode,
        deleteHandler,
        saveHandler,
        exportHandler,
        initializeEditorDisplayValue,
        handlerLoaded,
    } = useContext(IncidentHandlerCreateContext);
    const intl = useIntl();

    useEffect(() => {
        if (handlerLoaded) {
            initializeEditorDisplayValue();
        }
    }, [handlerLoaded, initializeEditorDisplayValue]);

    return (
        <div style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
            <QuickEditIncidentHandlerToolbar
                isDirty={isDirty}
                onRegenerateClick={goToFullEditMode}
                onExportClick={() => exportHandler()}
                onDeleteClick={() => deleteHandler()}
            />
            <div
                style={{
                    height: 'calc(100% - 136px)',
                    width: 'calc(100% - 42px)',
                    margin: 20,
                    border: '1px solid #ccc',
                }}
            >
                <HandlerEditor
                    editorDisplayValue={editorDisplayValue}
                    onEditorValueChange={onEditorValueChange}
                    setIsValid={setIsEditorValueValid}
                />
                <div
                    style={{
                        display: 'flex',
                        marginTop: 20,
                        marginBottom: 20,
                        gap: 10,
                    }}
                >
                    <Button appearance="primary" onClick={saveHandler} disabled={!isEditorValueValid}>
                        {intl.formatMessage(IncidentHandlerCreateResources.save)}
                    </Button>
                    <DirtyStateConfirmationWrapper isDirty={isDirty} onConfirm={exitToHome}>
                        <Button>{intl.formatMessage(IncidentHandlerCreateResources.cancel)}</Button>
                    </DirtyStateConfirmationWrapper>
                </div>
            </div>
        </div>
    );
};
