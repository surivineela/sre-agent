import { Caption1, Image, Subtitle2, Text } from '@fluentui/react-components';
import { Document20Regular, Globe20Regular } from '@fluentui/react-icons';
import { useFormikContext } from 'formik';
import { FC, useCallback, useContext, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { SelectableCard } from '../../../Common/Components/SelectableCard/SelectableCard';
import { resolveResourceIcon } from '../../../Common/Helpers/Resources';
import { OnboardingWizardResources } from '../../../Strings/SREAgentResources';
import { SreAgentContext } from '../../Contracts/Context';
import { AddRepositoryDialog } from '../../Settings/KnowledgeBaseComponents/AddRepositoryDialog/AddRepositoryDialog';
import { AddWebPageDialog } from '../../Settings/KnowledgeBaseComponents/AddWebPageDialog';
import { FileUploadDialog } from '../../Settings/KnowledgeBaseComponents/FileUploadDialog';
import { KnowledgeSource, KnowledgeSourceType, WizardFormValues } from '../OnboardingWizard';
import { useKnowledgeBaseStepStyles } from '../OnboardingWizard.styles';
import { ScopeDataGrid, ScopeDataGridColumn } from './ScopeDataGrid';

export const KnowledgeBaseStep: FC = () => {
    const intl = useIntl();
    const styles = useKnowledgeBaseStepStyles();
    const { agentObj } = useContext(SreAgentContext);

    const { values, setFieldValue } = useFormikContext<WizardFormValues>();

    const [isFileDialogOpen, setIsFileDialogOpen] = useState(false);
    const [isWebPageDialogOpen, setIsWebPageDialogOpen] = useState(false);
    const [isRepositoryDialogOpen, setIsRepositoryDialogOpen] = useState(false);

    const [selectedFiles, setSelectedFiles] = useState<File[]>([]);
    const [isDragOver, setIsDragOver] = useState(false);
    const [isUploading] = useState(false);
    const fileInputRef = useRef<HTMLInputElement>(null);

    const agentName = useMemo(() => agentObj?.name, [agentObj]);
    const agentLocation = useMemo(() => agentObj?.location, [agentObj]);

    const getIconForType = useCallback((type: KnowledgeSourceType) => {
        switch (type) {
            case 'file':
                return <Document20Regular />;
            case 'webpage':
                return <Globe20Regular />;
            case 'repository':
                return <Image src={resolveResourceIcon('GitHub')} alt="" aria-hidden="true" width={20} height={20} />;
        }
    }, []);

    const handleDeleteSelected = useCallback(
        (selectedIds: string[]) => {
            const selectedSet = new Set(selectedIds);
            const remainingSources = values.knowledgeSources.filter(source => !selectedSet.has(source.id));
            setFieldValue('knowledgeSources', remainingSources);
        },
        [values.knowledgeSources, setFieldValue]
    );

    const handleFileDragOver = useCallback((event: React.DragEvent<HTMLDivElement>) => {
        event.preventDefault();
        setIsDragOver(true);
    }, []);

    const handleFileDragLeave = useCallback((event: React.DragEvent<HTMLDivElement>) => {
        event.preventDefault();
        setIsDragOver(false);
    }, []);

    const handleFileDrop = useCallback((event: React.DragEvent<HTMLDivElement>) => {
        event.preventDefault();
        setIsDragOver(false);
        const files = Array.from(event.dataTransfer.files);
        setSelectedFiles(prev => [...prev, ...files]);
    }, []);

    const handleBrowseClick = useCallback(() => {
        fileInputRef.current?.click();
    }, []);

    const handleFileInputChange = useCallback((event: React.ChangeEvent<HTMLInputElement>) => {
        const files = event.target.files ? Array.from(event.target.files) : [];
        setSelectedFiles(prev => [...prev, ...files]);
    }, []);

    const handleRemoveFile = useCallback((index: number) => {
        setSelectedFiles(prev => prev.filter((_, i) => i !== index));
    }, []);

    const handleFileUpload = useCallback(() => {
        const newSources: KnowledgeSource[] = selectedFiles.map(file => ({
            id: `file-${Date.now()}-${file.name}`,
            type: 'file',
            name: file.name,
            lastModified: new Date().toISOString(),
        }));

        setFieldValue('knowledgeSources', [...values.knowledgeSources, ...newSources]);
        setSelectedFiles([]);
        setIsFileDialogOpen(false);
    }, [selectedFiles, values.knowledgeSources, setFieldValue]);

    const handleFileDialogCancel = useCallback(() => {
        setSelectedFiles([]);
        setIsFileDialogOpen(false);
    }, []);

    const handleCreateFile = useCallback(() => {
        setIsFileDialogOpen(false);
    }, []);

    const handleAddWebPage = useCallback(
        (url: string, name: string) => {
            const newSource: KnowledgeSource = {
                id: `webpage-${Date.now()}-${name}`,
                type: 'webpage',
                name,
                url,
                lastModified: new Date().toISOString(),
            };

            setFieldValue('knowledgeSources', [...values.knowledgeSources, newSource]);
            setIsWebPageDialogOpen(false);
        },
        [values.knowledgeSources, setFieldValue]
    );

    const handleWebPageDialogCancel = useCallback(() => {
        setIsWebPageDialogOpen(false);
    }, []);

    const handleRepositorySuccess = useCallback(() => {
        setIsRepositoryDialogOpen(false);
    }, []);

    const fileSources = useMemo(
        () => values.knowledgeSources.filter(source => source.type === 'file'),
        [values.knowledgeSources]
    );

    const webPageSources = useMemo(
        () => values.knowledgeSources.filter(source => source.type === 'webpage'),
        [values.knowledgeSources]
    );

    const repositorySources = useMemo(
        () => values.knowledgeSources.filter(source => source.type === 'repository'),
        [values.knowledgeSources]
    );

    const columns: ScopeDataGridColumn<KnowledgeSource>[] = useMemo(
        () => [
            {
                columnId: 'name',
                headerLabel: intl.formatMessage(OnboardingWizardResources.nameColumn),
                minWidth: 200,
                defaultWidth: 280,
                renderCell: (item: KnowledgeSource) => (
                    <div className={styles.nameCell}>
                        {getIconForType(item.type)}
                        <Text>{item.name}</Text>
                    </div>
                ),
            },
            {
                columnId: 'lastModified',
                headerLabel: intl.formatMessage(OnboardingWizardResources.lastModifiedColumn),
                minWidth: 150,
                defaultWidth: 180,
                renderCell: (item: KnowledgeSource) =>
                    item.lastModified ? new Date(item.lastModified).toLocaleDateString() : '—',
            },
        ],
        [intl, getIconForType, styles.nameCell]
    );

    return (
        <div className={styles.container}>
            <div className={styles.headerSection}>
                <Subtitle2>{intl.formatMessage(OnboardingWizardResources.knowledgeBase)}</Subtitle2>
                <Caption1 className={styles.description}>
                    {intl.formatMessage(OnboardingWizardResources.knowledgeBaseDescription)}
                </Caption1>
            </div>

            <div className={styles.addButtonsContainer}>
                <SelectableCard
                    onSelect={() => setIsRepositoryDialogOpen(true)}
                    icon={<Image src={resolveResourceIcon('GitHub')} alt="" aria-hidden="true" width={20} height={20} />}
                    title={intl.formatMessage(OnboardingWizardResources.addRepository)}
                />
                <SelectableCard
                    onSelect={() => setIsFileDialogOpen(true)}
                    icon={<Document20Regular />}
                    title={intl.formatMessage(OnboardingWizardResources.addFile)}
                />
                <SelectableCard
                    onSelect={() => setIsWebPageDialogOpen(true)}
                    icon={<Globe20Regular />}
                    title={intl.formatMessage(OnboardingWizardResources.addWebPage)}
                />
            </div>

            {fileSources.length > 0 && (
                <ScopeDataGrid
                    title={intl.formatMessage(OnboardingWizardResources.filesTitle)}
                    items={fileSources}
                    columns={columns}
                    getRowId={item => item.id}
                    emptyMessage={intl.formatMessage(OnboardingWizardResources.noFilesSelected)}
                    ariaLabel={intl.formatMessage(OnboardingWizardResources.filesTitle)}
                    onDeleteSelected={handleDeleteSelected}
                />
            )}

            {webPageSources.length > 0 && (
                <ScopeDataGrid
                    title={intl.formatMessage(OnboardingWizardResources.webPagesTitle)}
                    items={webPageSources}
                    columns={columns}
                    getRowId={item => item.id}
                    emptyMessage={intl.formatMessage(OnboardingWizardResources.noWebPagesSelected)}
                    ariaLabel={intl.formatMessage(OnboardingWizardResources.webPagesTitle)}
                    onDeleteSelected={handleDeleteSelected}
                />
            )}

            {repositorySources.length > 0 && (
                <ScopeDataGrid
                    title={intl.formatMessage(OnboardingWizardResources.repositoriesTitle)}
                    items={repositorySources}
                    columns={columns}
                    getRowId={item => item.id}
                    emptyMessage={intl.formatMessage(OnboardingWizardResources.noRepositoriesSelected)}
                    ariaLabel={intl.formatMessage(OnboardingWizardResources.repositoriesTitle)}
                    onDeleteSelected={handleDeleteSelected}
                />
            )}

            <FileUploadDialog
                isOpen={isFileDialogOpen}
                onOpenChange={setIsFileDialogOpen}
                selectedFiles={selectedFiles}
                isDragOver={isDragOver}
                isUploading={isUploading}
                fileInputRef={fileInputRef}
                onDragOver={handleFileDragOver}
                onDragLeave={handleFileDragLeave}
                onDrop={handleFileDrop}
                onBrowseClick={handleBrowseClick}
                onFileInputChange={handleFileInputChange}
                onRemoveFile={handleRemoveFile}
                onUpload={handleFileUpload}
                onCancel={handleFileDialogCancel}
                onCreateFile={handleCreateFile}
            />

            <AddWebPageDialog
                isOpen={isWebPageDialogOpen}
                onOpenChange={setIsWebPageDialogOpen}
                onAddWebPage={handleAddWebPage}
                onCancel={handleWebPageDialogCancel}
            />

            <AddRepositoryDialog
                isOpen={isRepositoryDialogOpen}
                onOpenChange={setIsRepositoryDialogOpen}
                onSuccess={handleRepositorySuccess}
                agentName={agentName}
                agentLocation={agentLocation}
            />
        </div>
    );
};
