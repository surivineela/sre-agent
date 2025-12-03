import { useTheme } from '@fluentui/react';
import MonacoEditor from '@monaco-editor/react';
import { FC } from 'react';
import { useAgentCreateDialogStyles } from './AgentCreateDialog.Styles';
import { YamlViewProps } from './Contracts';
import { TestPanel } from './TestPanel';

export const YamlView: FC<YamlViewProps> = ({
    yamlContent,
    handleYamlChange,
    disabled,

    openedPanel,
    testPanelProps,
}) => {
    const styles = useAgentCreateDialogStyles();
    const theme = useTheme();

    return (
        <div className={styles.dialogContentOuterWrapper}>
            <div className={styles.dialogContentInnerWrapper}>
                <div className={styles.yamlContainer}>
                    <div className={styles.yamlEditor}>
                        <MonacoEditor
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
                </div>
                {openedPanel === 'test' && <TestPanel {...testPanelProps} />}
            </div>
        </div>
    );
};
