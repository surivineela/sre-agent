import { Button } from '@fluentui/react-components';
import { FC, useContext, useEffect } from 'react';
import { useIntl } from 'react-intl';
import { IncidentHandlerCreateResources } from '../../../Strings/SREAgentResources';
import { HandlerEditor } from '../CreateIncidentHandler/Common/HandlerEditor';
import { IncidentHandlerCreateContext } from '../CreateIncidentHandler/IncidentHandlerCreateContext';
import QuickEditIncidentHandlerToolbar from './QuickEditIncidentHandlerToolbar';

export const QuickEditIncidentHandler: FC = () => {
    const {
        editorDisplayValue,
        onEditorValueChange,
        setIsEditorValueValid,
        isEditorValueValid,
        exitToHome,
        deleteHandler,
        saveHandler,
        exportHandler,
        initializeEditorDisplayValue,
        handlerLoaded,
        setMode,
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
                onRegenerateClick={() => setMode('edit')}
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
                    <Button onClick={exitToHome}>{intl.formatMessage(IncidentHandlerCreateResources.cancel)}</Button>
                </div>
            </div>
        </div>
    );
};
