import { Text } from '@fluentui/react-components';
import Editor from '@monaco-editor/react';
import { FC, useContext } from 'react';
import { useIntl } from 'react-intl';
import { ThemeMode } from '../../../../Common/AzPortalProxy/Models/ITheme';
import { EnvironmentContext } from '../../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ExtendedAgentsGraphResources } from '../../../../Strings/SREAgentResources';
import { useSkillFileEditorStyles } from './SkillFileEditor.styles';

export const getFileLanguage = (fileName: string): string => {
    const ext = fileName.split('.').pop()?.toLowerCase();
    switch (ext) {
        case 'md':
            return 'markdown';
        case 'json':
            return 'json';
        case 'yaml':
        case 'yml':
            return 'yaml';
        case 'js':
            return 'javascript';
        case 'ts':
            return 'typescript';
        case 'py':
            return 'python';
        case 'sh':
        case 'bash':
            return 'shell';
        case 'ps1':
            return 'powershell';
        case 'xml':
            return 'xml';
        case 'html':
            return 'html';
        case 'css':
            return 'css';
        default:
            return 'plaintext';
    }
};

interface SkillFileEditorProps {
    fileName: string | null;
    content: string;
    language: string;
    readOnly?: boolean;
    onChange: (value: string) => void;
}

export const SkillFileEditor: FC<SkillFileEditorProps> = ({ fileName, content, language, readOnly = false, onChange }) => {
    const intl = useIntl();
    const styles = useSkillFileEditorStyles();
    const { theme } = useContext(EnvironmentContext);
    const isDarkTheme = theme?.mode === ThemeMode.Dark || theme?.name === 'dark';
    const editorTheme = isDarkTheme ? 'vs-dark' : 'vs-light';

    const handleEditorChange = (value: string | undefined) => {
        if (!readOnly) {
            onChange(value ?? '');
        }
    };

    if (!fileName) {
        return (
            <div className={styles.container}>
                <div className={styles.noFileSelected}>
                    <Text>{intl.formatMessage(ExtendedAgentsGraphResources.noFileSelected)}</Text>
                </div>
            </div>
        );
    }

    return (
        <div className={styles.container}>
            <div className={styles.header}>
                <Text>{fileName}</Text>
            </div>
            <div className={styles.editorContainer}>
                <Editor
                    value={content}
                    language={language}
                    theme={editorTheme}
                    onChange={handleEditorChange}
                    options={{
                        minimap: { enabled: false },
                        wordWrap: 'on',
                        lineNumbers: 'on',
                        glyphMargin: false,
                        folding: true,
                        scrollBeyondLastLine: false,
                        automaticLayout: true,
                        fontSize: 13,
                        readOnly,
                    }}
                    height="100%"
                />
            </div>
        </div>
    );
};
