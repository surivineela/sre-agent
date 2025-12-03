import { Button, Field, Input, tokens } from '@fluentui/react-components';
import { Code20Regular, Sparkle20Filled, Timer16Regular } from '@fluentui/react-icons';
import MonacoEditor from '@monaco-editor/react';
import { FC } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources } from '../../../../../Strings/SREAgentResources';
import { sanitizeEntityName } from '../../utils/nameValidation';
import { useCodeViewStyles } from './styles';
import { CodeViewProps } from './types';

export const CodeView: FC<CodeViewProps> = ({ tool, onChange, onSwitchToPrompt }) => {
    const styles = useCodeViewStyles();
    const intl = useIntl();

    return (
        <div className={styles.container}>
            {/* Compact Toolbar */}
            <div className={styles.toolbar}>
                <Field size="small" className={styles.toolbarField}>
                    <Input
                        size="small"
                        value={tool.name || ''}
                        onChange={(_, data) => onChange({ ...tool, name: sanitizeEntityName(data.value) })}
                        placeholder="function_name"
                        contentBefore={<Code20Regular style={{ fontSize: 16, color: tokens.colorNeutralForeground3 }} />}
                        style={{ fontFamily: 'Consolas, Monaco, monospace' }}
                    />
                </Field>
                <Field size="small" className={styles.toolbarFieldSmall}>
                    <Input
                        size="small"
                        type="number"
                        value={tool.timeoutSeconds?.toString() || '120'}
                        onChange={(_, data) => onChange({ ...tool, timeoutSeconds: parseInt(data.value) || 120 })}
                        contentBefore={<Timer16Regular style={{ color: tokens.colorNeutralForeground3 }} />}
                        min={5}
                        max={900}
                        title={intl.formatMessage(ExtendedAgentsGraphResources.pythonToolTimeoutTitle)}
                    />
                </Field>
                <Button appearance="subtle" size="small" icon={<Sparkle20Filled />} onClick={onSwitchToPrompt}>
                    AI Assist
                </Button>
            </div>

            {/* Description Bar */}
            <div className={styles.descriptionBar}>
                <Input
                    size="small"
                    value={tool.description || ''}
                    onChange={(_, data) => onChange({ ...tool, description: data.value })}
                    placeholder="Brief description of what this function does..."
                    style={{ width: '100%', backgroundColor: 'transparent' }}
                    appearance="underline"
                />
            </div>

            {/* Monaco Editor */}
            <div className={styles.editorContainer}>
                <MonacoEditor
                    height="100%"
                    language="python"
                    theme="vs-light"
                    value={tool.functionCode || getDefaultCode()}
                    onChange={value => onChange({ ...tool, functionCode: value || '' })}
                    options={{
                        minimap: { enabled: false },
                        lineNumbers: 'on',
                        scrollBeyondLastLine: false,
                        fontSize: 13,
                        fontFamily: 'Consolas, Monaco, "Courier New", monospace',
                        tabSize: 4,
                        wordWrap: 'on',
                        automaticLayout: true,
                        padding: { top: 12, bottom: 12 },
                        lineHeight: 20,
                        renderLineHighlight: 'line',
                        cursorBlinking: 'smooth',
                        smoothScrolling: true,
                    }}
                />
            </div>
        </div>
    );
};

function getDefaultCode(): string {
    return `def main() -> dict:
    """
    Your function description here.
    """
    # Your code here
    return {"result": "Hello, World!"}
`;
}
