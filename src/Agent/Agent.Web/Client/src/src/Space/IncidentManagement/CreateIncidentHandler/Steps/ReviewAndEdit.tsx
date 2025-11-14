import { Button } from '@fluentui/react-components';
import { FC, useContext, useEffect } from 'react';
import { useIntl } from 'react-intl';
import { IncidentHandlerCreateResources } from '../../../../Strings/SREAgentResources';
import { HandlerEditor } from '../Common/HandlerEditor';
import { DirtyStateConfirmationWrapper } from '../DirtyStateConfirmationDialog';
import { IncidentHandlerCreateContext, IncidentHandlerCreateSteps } from '../IncidentHandlerCreateContext';

export const ReviewAndEdit: FC = () => {
    const {
        editorDisplayValue,
        onEditorValueChange,
        setIsEditorValueValid,
        isEditorValueValid,
        exitToHome,
        saveHandler,
        setCurrentStep,
        initializeEditorDisplayValue,
    } = useContext(IncidentHandlerCreateContext);
    const intl = useIntl();

    useEffect(() => {
        initializeEditorDisplayValue();
    }, [initializeEditorDisplayValue]);

    return (
        <div
            style={{
                height: 'calc(100% - 92px)',
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
                <Button onClick={() => setCurrentStep(IncidentHandlerCreateSteps.GenerateHandler)}>
                    {intl.formatMessage(IncidentHandlerCreateResources.previous)}
                </Button>
                <Button appearance="primary" onClick={saveHandler} disabled={!isEditorValueValid}>
                    {intl.formatMessage(IncidentHandlerCreateResources.save)}
                </Button>
                <DirtyStateConfirmationWrapper isDirty={true} onConfirm={() => exitToHome()}>
                    <Button>{intl.formatMessage(IncidentHandlerCreateResources.cancel)}</Button>
                </DirtyStateConfirmationWrapper>
            </div>
        </div>
    );
};
