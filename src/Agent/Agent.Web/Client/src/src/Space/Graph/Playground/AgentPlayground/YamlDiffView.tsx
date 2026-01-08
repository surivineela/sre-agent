import { useTheme } from '@fluentui/react';
import { DiffEditor } from '@monaco-editor/react';
import { FC } from 'react';
import { useAgentPlaygroundStyles } from './AgentPlayground.Styles';
import { YamlDiffViewProps } from './Contracts';

export const YamlDiffView: FC<YamlDiffViewProps> = ({ yamlContent, originalYamlContent }) => {
    const styles = useAgentPlaygroundStyles();
    const theme = useTheme();

    return (
        <div className={styles.dialogContentOuterWrapper}>
            <div className={styles.dialogContentInnerWrapper}>
                <div className={styles.yamlContainer}>
                    <div className={styles.yamlEditor}>
                        <DiffEditor
                            modified={yamlContent}
                            original={originalYamlContent}
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
                                readOnly: true,
                                lineNumbers: 'off',
                            }}
                            height="100%"
                            width="100%"
                        />
                    </div>
                </div>
            </div>
        </div>
    );
};
