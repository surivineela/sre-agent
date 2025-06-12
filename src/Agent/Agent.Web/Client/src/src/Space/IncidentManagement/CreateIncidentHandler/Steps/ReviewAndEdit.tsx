import { Button } from '@fluentui/react-components';
import MonacoEditor, { Monaco } from '@monaco-editor/react';
import { FC, useContext, useEffect } from 'react';
import { useIntl } from 'react-intl';
import { IncidentHandlerCreateResources } from '../../../../Strings/SREAgentResources';
import { IncidentHandlerCreateContext, IncidentHandlerCreateSteps } from '../IncidentHandlerCreateContext';

const monacoJsonSchema = {
    $schema: 'http://json-schema.org/draft-07/schema#',
    title: 'IncidentHandler',
    type: 'object',
    properties: {
        id: { type: 'string' },
        name: { type: 'string' },
        description: { type: 'string' },
        incidentFilterId: { type: 'string' },
        incidentProcessingGuide: { type: 'array', items: { type: 'string' } },
        tools: { type: 'array', items: { type: 'string' } },
        incidents: { type: 'array', items: { type: 'string' } },
        customInstructions: { type: 'string' },
    },
};
const handleEditorDidMount = (_editor: any, monaco: Monaco) => {
    // Configure JSON validation
    monaco.languages.json.jsonDefaults.setDiagnosticsOptions({
        validate: true,
        schemas: [
            {
                uri: '', // This is just an identifier
                fileMatch: ['*'],
                schema: monacoJsonSchema,
            },
        ],
    });
};

export const ReviewAndEdit: FC = () => {
    const { editorDisplayValue, onEditorValueChange, exitToHome, save, setCurrentStep, initializeEditorDisplayValue } =
        useContext(IncidentHandlerCreateContext);
    const intl = useIntl();

    useEffect(() => {
        initializeEditorDisplayValue();
    }, []);

    return (
        <div
            style={{
                height: 'calc(100% - 92px)',
                width: 'calc(100% - 42px)',
                margin: 20,
                border: '1px solid #ccc',
            }}
        >
            <MonacoEditor
                language="json"
                theme="vs"
                options={{
                    automaticLayout: true,
                    formatOnType: true,
                    formatOnPaste: true,
                    fontSize: 15,
                    wordWrap: 'on',
                }}
                onMount={handleEditorDidMount}
                value={editorDisplayValue}
                onChange={(value, _ev) => onEditorValueChange(value)}
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
                <Button appearance="primary" onClick={save}>
                    {intl.formatMessage(IncidentHandlerCreateResources.save)}
                </Button>
                <Button onClick={exitToHome}>{intl.formatMessage(IncidentHandlerCreateResources.cancel)}</Button>
            </div>
        </div>
    );
};
