import { Image } from '@fluentui/react-components';
import { Document20Regular, Globe20Regular } from '@fluentui/react-icons';
import { FC } from 'react';
import { useIntl } from 'react-intl';
import { AddRepositoryDialog } from '../../../Space/Settings/KnowledgeBaseComponents/AddRepositoryDialog/AddRepositoryDialog';
import { AddWebPageDialog } from '../../../Space/Settings/KnowledgeBaseComponents/AddWebPageDialog';
import { FileUploadDialog } from '../../../Space/Settings/KnowledgeBaseComponents/FileUploadDialog';
import { OnboardingWizardResources } from '../../../Strings/SREAgentResources';
import { resolveResourceIcon } from '../../Helpers/Resources';
import { SelectableCard } from '../SelectableCard/SelectableCard';
import { useKnowledgeBasePicker, UseKnowledgeBasePickerResult } from './useKnowledgeBasePicker';

interface KnowledgeBasePickerProps {
    picker: UseKnowledgeBasePickerResult;
}

export const AddRepositoryCard: FC<KnowledgeBasePickerProps> = ({ picker }) => {
    const intl = useIntl();

    return (
        <SelectableCard
            onSelect={() => picker.setIsRepositoryDialogOpen(true)}
            icon={<Image src={resolveResourceIcon('GitHub')} alt="" aria-hidden="true" width={20} height={20} />}
            title={intl.formatMessage(OnboardingWizardResources.addRepository)}
        />
    );
};

export const AddFileCard: FC<KnowledgeBasePickerProps> = ({ picker }) => {
    const intl = useIntl();

    return (
        <SelectableCard
            onSelect={() => picker.setIsFileDialogOpen(true)}
            icon={<Document20Regular />}
            title={intl.formatMessage(OnboardingWizardResources.addFile)}
        />
    );
};

export const AddWebPageCard: FC<KnowledgeBasePickerProps> = ({ picker }) => {
    const intl = useIntl();

    return (
        <SelectableCard
            onSelect={() => picker.setIsWebPageDialogOpen(true)}
            icon={<Globe20Regular />}
            title={intl.formatMessage(OnboardingWizardResources.addWebPage)}
        />
    );
};

export const KnowledgeBaseDialogs: FC<KnowledgeBasePickerProps> = ({ picker }) => {
    return (
        <>
            <FileUploadDialog
                isOpen={picker.isFileDialogOpen}
                onOpenChange={picker.setIsFileDialogOpen}
                selectedFiles={picker.selectedFiles}
                isDragOver={picker.isDragOver}
                isUploading={picker.isUploading}
                fileInputRef={picker.fileInputRef}
                onDragOver={picker.handleFileDragOver}
                onDragLeave={picker.handleFileDragLeave}
                onDrop={picker.handleFileDrop}
                onBrowseClick={picker.handleBrowseClick}
                onFileInputChange={picker.handleFileInputChange}
                onRemoveFile={picker.handleRemoveFile}
                onUpload={picker.handleFileUpload}
                onCancel={picker.handleFileDialogCancel}
                onCreateFile={picker.handleCreateFile}
            />

            <AddWebPageDialog
                isOpen={picker.isWebPageDialogOpen}
                onOpenChange={picker.setIsWebPageDialogOpen}
                onAddWebPage={picker.handleAddWebPage}
                onCancel={picker.handleWebPageDialogCancel}
            />

            <AddRepositoryDialog
                isOpen={picker.isRepositoryDialogOpen}
                onOpenChange={picker.setIsRepositoryDialogOpen}
                onSuccess={picker.handleRepositorySuccess}
                agentName={picker.agentName}
                agentLocation={picker.agentLocation}
            />
        </>
    );
};

export { useKnowledgeBasePicker };
export type { UseKnowledgeBasePickerResult };
