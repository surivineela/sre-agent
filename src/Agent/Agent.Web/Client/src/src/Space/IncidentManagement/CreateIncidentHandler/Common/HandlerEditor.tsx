import MonacoEditor, { Monaco } from '@monaco-editor/react';
import { FC } from 'react';

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

export interface HandlerEditorProps {
    editorDisplayValue: string | undefined;
    onEditorValueChange: (value: string | undefined) => void;
    setIsValid: (isValid: boolean) => void;
}

export const HandlerEditor: FC<HandlerEditorProps> = ({ editorDisplayValue, onEditorValueChange, setIsValid }) => {
    return (
        <MonacoEditor
            language="json"
            theme="vs"
            options={{
                automaticLayout: true,
                formatOnType: true,
                formatOnPaste: true,
                fontSize: 15,
                wordWrap: 'on',
                lineNumbers: 'off',
            }}
            onMount={handleEditorDidMount}
            value={editorDisplayValue}
            onChange={(value, _ev) => onEditorValueChange(value)}
            onValidate={markers => setIsValid(!markers.some(marker => marker.severity === 8))}
        />
    );
};
