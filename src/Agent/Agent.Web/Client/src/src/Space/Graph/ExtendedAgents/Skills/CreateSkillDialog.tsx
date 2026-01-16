import {
    Button,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    Divider,
    Field,
    Input,
    Link,
    mergeClasses,
    Text,
    Textarea,
    ToolbarButton,
} from '@fluentui/react-components';
import { Dismiss24Regular } from '@fluentui/react-icons';
import { FC, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { EnvironmentContext } from '../../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ExtendedAgentsGraphResources, SreAgentResources } from '../../../../Strings/SREAgentResources';
import { ExtendedTool, Skill, SkillFile, SystemTool } from '../../../Contracts/ExtendedAgentGraph';
import { PillSet } from '../../Common/PillSet';
import { ToolsPicker } from '../../Common/ToolsPicker/ToolsPicker';
import { useToolsPicker } from '../../Common/ToolsPicker/useToolsPicker';
import { McpConnection } from '../../ExtendedAgentCreationDialog/api/mcpConnectionsApi';
import { useSkillDialogStyles } from './CreateSkillDialog.styles';
import { FileSystemItem, SKILL_MD_FILENAME, SkillFileBrowser } from './SkillFileBrowser';
import { getFileLanguage, SkillFileEditor } from './SkillFileEditor';

const DEFAULT_SKILL_FILE: FileSystemItem = {
    name: SKILL_MD_FILENAME,
    type: 'file',
    path: '/' + SKILL_MD_FILENAME,
    isDefault: true,
};

interface CreateSkillDialogProps {
    isOpen: boolean;
    onDismiss: () => void;
    onSave: (skill: Skill) => Promise<{ success: boolean; error?: string }>;
    existingSkill?: Skill;
    existingTools?: ExtendedTool[];
    systemTools?: SystemTool[];
    mcpConnections?: McpConnection[];
}

export const CreateSkillDialog: FC<CreateSkillDialogProps> = ({
    isOpen,
    onDismiss,
    onSave,
    existingSkill,
    existingTools = [],
    systemTools = [],
    mcpConnections,
}) => {
    const intl = useIntl();
    const styles = useSkillDialogStyles();
    useContext(EnvironmentContext);

    const [name, setName] = useState(existingSkill?.name || '');
    const [description, setDescription] = useState(existingSkill?.description || '');
    const [selectedNonMcpTools, setSelectedNonMcpTools] = useState<string[]>([]);
    const [selectedMcpTools, setSelectedMcpTools] = useState<string[]>([]);
    const [skillContent, setSkillContent] = useState(existingSkill?.skillContent || '');
    const [additionalFiles, setAdditionalFiles] = useState<SkillFile[]>(existingSkill?.additionalFiles || []);
    const [emptyFolders, setEmptyFolders] = useState<string[]>([]);
    const [isSaving, setIsSaving] = useState(false);
    const [error, setError] = useState<string | undefined>();
    const [isToolsPanelOpen, setIsToolsPanelOpen] = useState(false);
    const [isEditingDescription, setIsEditingDescription] = useState(false);
    const [tempDescription, setTempDescription] = useState('');
    const [selectedFile, setSelectedFile] = useState<FileSystemItem | null>(DEFAULT_SKILL_FILE);

    // Compute selected file content and language
    const selectedFileContent = useMemo(() => {
        if (!selectedFile) return '';
        if (selectedFile.isDefault) return skillContent;
        if (selectedFile.fileIndex !== undefined) {
            return additionalFiles[selectedFile.fileIndex]?.content || '';
        }
        return '';
    }, [selectedFile, skillContent, additionalFiles]);

    const selectedFileLanguage = useMemo(() => {
        if (!selectedFile) return 'plaintext';
        return getFileLanguage(selectedFile.name);
    }, [selectedFile]);

    const handleEditorChange = useCallback(
        (value: string) => {
            if (!selectedFile) return;

            if (selectedFile.isDefault) {
                setSkillContent(value);
            } else if (selectedFile.fileIndex !== undefined) {
                const newFiles = [...additionalFiles];
                newFiles[selectedFile.fileIndex] = {
                    ...newFiles[selectedFile.fileIndex],
                    content: value,
                };
                setAdditionalFiles(newFiles);
            }
        },
        [selectedFile, additionalFiles]
    );

    const allMcpToolNames = useMemo(() => {
        const names = new Set<string>();
        mcpConnections?.forEach(connection => {
            connection.tools?.forEach(tool => {
                names.add(tool.name);
            });
        });
        return names;
    }, [mcpConnections]);

    // Tools picker hook
    const toolsPickerHook = useToolsPicker({
        selectedToolNames: selectedNonMcpTools,
        setSelectedToolNames: setSelectedNonMcpTools,
        selectedMcpToolNames: selectedMcpTools,
        setSelectedMcpToolNames: setSelectedMcpTools,
        existingTools,
        systemTools,
        mcpConnections,
    });

    const isEditMode = !!existingSkill;

    // Reset form when existingSkill changes (e.g., when editing a different skill)
    useEffect(() => {
        setName(existingSkill?.name || '');
        setDescription(existingSkill?.description || '');
        const { mcpTools, nonMcpTools } = splitMcpAndNonMcpTools(existingSkill?.tools || [], allMcpToolNames);
        setSelectedNonMcpTools(nonMcpTools);
        setSelectedMcpTools(mcpTools);
        setSkillContent(existingSkill?.skillContent || '');
        setAdditionalFiles(existingSkill?.additionalFiles || []);
        setEmptyFolders([]);
        setError(undefined);
        setIsToolsPanelOpen(false);
        setIsEditingDescription(false);
        setSelectedFile(DEFAULT_SKILL_FILE);
    }, [existingSkill, allMcpToolNames]);

    const handleSave = async () => {
        if (!name.trim()) {
            setError(intl.formatMessage(ExtendedAgentsGraphResources.skillNameRequired));
            return;
        }

        setIsSaving(true);
        setError(undefined);

        const skill: Skill = {
            name: name.trim(),
            description: description.trim() || undefined,
            tools: [...selectedNonMcpTools, ...selectedMcpTools],
            skillContent: skillContent.trim() || undefined,
            additionalFiles,
        };

        const result = await onSave(skill);
        setIsSaving(false);

        if (result.success) {
            handleDismiss();
        } else {
            setError(result.error || intl.formatMessage(ExtendedAgentsGraphResources.failedToSaveSkill));
        }
    };

    const handleDismiss = () => {
        setName('');
        setDescription('');
        setSelectedNonMcpTools([]);
        setSelectedMcpTools([]);
        setSkillContent('');
        setAdditionalFiles([]);
        setEmptyFolders([]);
        setError(undefined);
        setIsToolsPanelOpen(false);
        setIsEditingDescription(false);
        setSelectedFile(DEFAULT_SKILL_FILE);
        toolsPickerHook.onClearSearchAndExpandedGroups();
        onDismiss();
    };

    return (
        <Dialog open={isOpen} onOpenChange={(_, data) => !data.open && handleDismiss()}>
            <DialogSurface className={mergeClasses(styles.dialogSurface, isToolsPanelOpen && styles.dialogSurfaceWithPanel)}>
                <DialogBody>
                    <DialogTitle
                        action={
                            <Button
                                appearance="subtle"
                                aria-label={intl.formatMessage(SreAgentResources.close)}
                                icon={<Dismiss24Regular />}
                                onClick={handleDismiss}
                            />
                        }
                    >
                        {isEditMode
                            ? intl.formatMessage(ExtendedAgentsGraphResources.editSkill)
                            : intl.formatMessage(ExtendedAgentsGraphResources.createSkill)}
                    </DialogTitle>
                    <DialogContent className={styles.dialogContent}>
                        <div className={styles.dialogContentWrapper}>
                            <div className={styles.leftColumn}>
                                <div className={styles.formContent}>
                                    {error && <div className={styles.errorMessage}>{error}</div>}

                                    <Field label={intl.formatMessage(ExtendedAgentsGraphResources.skillName)} required>
                                        <Input
                                            value={name}
                                            onChange={(_, data) => setName(data.value)}
                                            disabled={isEditMode}
                                            placeholder={intl.formatMessage(ExtendedAgentsGraphResources.subagentNamePlaceholder)}
                                        />
                                    </Field>

                                    <Field
                                        label={
                                            <div className={styles.descriptionLabelRow}>
                                                <span>{intl.formatMessage(ExtendedAgentsGraphResources.skillDescription)}</span>
                                                {isEditingDescription ? (
                                                    <>
                                                        <Link
                                                            onClick={() => {
                                                                setDescription(tempDescription);
                                                                setIsEditingDescription(false);
                                                            }}
                                                        >
                                                            {intl.formatMessage(SreAgentResources.save)}
                                                        </Link>
                                                        <Link
                                                            onClick={() => {
                                                                setIsEditingDescription(false);
                                                            }}
                                                        >
                                                            {intl.formatMessage(SreAgentResources.cancel)}
                                                        </Link>
                                                    </>
                                                ) : (
                                                    <Link
                                                        onClick={() => {
                                                            setTempDescription(description);
                                                            setIsEditingDescription(true);
                                                        }}
                                                    >
                                                        {intl.formatMessage(ExtendedAgentsGraphResources.edit)}
                                                    </Link>
                                                )}
                                            </div>
                                        }
                                    >
                                        {isEditingDescription ? (
                                            <Textarea
                                                value={tempDescription}
                                                onChange={(_, data) => setTempDescription(data.value)}
                                                placeholder={intl.formatMessage(ExtendedAgentsGraphResources.descriptionPlaceholder)}
                                                rows={3}
                                                className={styles.descriptionTextarea}
                                            />
                                        ) : (
                                            <Text className={styles.descriptionText}>
                                                {description || intl.formatMessage(ExtendedAgentsGraphResources.noDescription)}
                                            </Text>
                                        )}
                                    </Field>
                                </div>
                                <div className={styles.fileBrowserWrapper}>
                                    <Field
                                        label={intl.formatMessage(ExtendedAgentsGraphResources.skillFiles)}
                                        className={styles.fileBrowserField}
                                    >
                                        <SkillFileBrowser
                                            files={additionalFiles}
                                            onFilesChange={setAdditionalFiles}
                                            skillContent={skillContent}
                                            emptyFolders={emptyFolders}
                                            onEmptyFoldersChange={setEmptyFolders}
                                            selectedFile={selectedFile}
                                            onFileSelect={setSelectedFile}
                                        />
                                    </Field>
                                </div>
                                <Field label={intl.formatMessage(ExtendedAgentsGraphResources.skillTools)}>
                                    <div className={styles.toolsFieldContent}>
                                        <PillSet
                                            items={toolsPickerHook.pillItems}
                                            onRemoveItem={key => toolsPickerHook.onSelectedToolChange(key, false)}
                                            onClearAll={toolsPickerHook.onClearSelectedTools}
                                        />

                                        <Link
                                            className={styles.toolsLink}
                                            onClick={() => setIsToolsPanelOpen(true)}
                                            disabled={isToolsPanelOpen}
                                        >
                                            {intl.formatMessage(ExtendedAgentsGraphResources.chooseTools)}
                                        </Link>
                                    </div>
                                </Field>
                            </div>
                            <div className={styles.editorColumn}>
                                <SkillFileEditor
                                    fileName={selectedFile?.name || null}
                                    content={selectedFileContent}
                                    language={selectedFileLanguage}
                                    onChange={handleEditorChange}
                                />
                            </div>
                            {isToolsPanelOpen && <Divider vertical className={styles.panelDivider} />}
                            {isToolsPanelOpen && (
                                <div className={styles.toolsPanelWrapper}>
                                    <div className={styles.toolsPanelHeader}>
                                        <Text size={400} weight="semibold">
                                            {intl.formatMessage(ExtendedAgentsGraphResources.chooseTools)}
                                        </Text>
                                        <ToolbarButton
                                            appearance="transparent"
                                            icon={<Dismiss24Regular />}
                                            onClick={() => {
                                                setIsToolsPanelOpen(false);
                                                toolsPickerHook.onClearSearchAndExpandedGroups();
                                            }}
                                        >
                                            {intl.formatMessage(ExtendedAgentsGraphResources.closePanel)}
                                        </ToolbarButton>
                                    </div>
                                    <ToolsPicker {...toolsPickerHook} />
                                </div>
                            )}
                        </div>
                    </DialogContent>
                    <DialogActions>
                        <Button appearance="secondary" onClick={handleDismiss} disabled={isSaving}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.cancel)}
                        </Button>
                        <Button appearance="primary" onClick={handleSave} disabled={isSaving || !name.trim()}>
                            {isSaving
                                ? intl.formatMessage(ExtendedAgentsGraphResources.saving)
                                : isEditMode
                                  ? intl.formatMessage(ExtendedAgentsGraphResources.save)
                                  : intl.formatMessage(ExtendedAgentsGraphResources.create)}
                        </Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};

const splitMcpAndNonMcpTools = (toolNames: string[], allMcpToolNames: Set<string>) => {
    const mcpTools: string[] = [];
    const nonMcpTools: string[] = [];

    toolNames.forEach(name => {
        if (allMcpToolNames.has(name)) {
            mcpTools.push(name);
        } else {
            nonMcpTools.push(name);
        }
    });

    return { mcpTools, nonMcpTools };
};
