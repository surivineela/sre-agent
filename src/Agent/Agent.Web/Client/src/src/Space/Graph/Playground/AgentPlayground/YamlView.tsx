import { useTheme } from '@fluentui/react';
import MonacoEditor from '@monaco-editor/react';
import { FC } from 'react';
import { useAgentPlaygroundStyles } from './AgentPlayground.Styles';
import { YamlViewProps } from './Contracts';

export const YamlView: FC<YamlViewProps> = ({ yamlContent, handleYamlChange, disabled }) => {
    const styles = useAgentPlaygroundStyles();
    const theme = useTheme();

    return (
        <div className={styles.yamlEditor}>
            <MonacoEditor
                saveViewState={false}
                value={yamlContent}
                onChange={handleYamlChange}
                language="yaml"
                theme={theme.isInverted ? 'vs-dark' : 'vs'}
                options={{
                    automaticLayout: true,
                    minimap: { enabled: false },
                    scrollBeyondLastLine: false,
                    fontSize: 14,
                    wordWrap: 'on',
                    formatOnType: true,
                    formatOnPaste: true,
                    tabSize: 2,
                    readOnly: disabled,
                    lineNumbers: 'off',
                }}
                height="100%"
                width="100%"
            />
        </div>
    );
};
