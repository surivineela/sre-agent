import {
    Button,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    Field,
    Input,
    Text,
} from '@fluentui/react-components';
import { Globe20Regular } from '@fluentui/react-icons';
import { FC, useCallback, useState } from 'react';
import { useIntl } from 'react-intl';
import { KnowledgeSettingsResources, PermissionsResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import { useKnowledgeBaseStyles } from '../Styles/KnowledgeBase.styles';

interface AddWebPageDialogProps {
    isOpen: boolean;
    onOpenChange: (open: boolean) => void;
    onAddWebPage: (url: string, name: string, description?: string) => void;
    onCancel: () => void;
    isAdding?: boolean;
}

export const AddWebPageDialog: FC<AddWebPageDialogProps> = ({ isOpen, onOpenChange, onAddWebPage, onCancel, isAdding = false }) => {
    const intl = useIntl();
    const styles = useKnowledgeBaseStyles();

    const [url, setUrl] = useState('');
    const [name, setName] = useState('');
    const [description, setDescription] = useState('');

    const isFormValid = url.trim() !== '' && name.trim() !== '';

    const handleCancel = useCallback(() => {
        setUrl('');
        setName('');
        setDescription('');
        onCancel();
    }, [onCancel]);

    const handleAddWebPage = useCallback(() => {
        if (isFormValid) {
            onAddWebPage(url.trim(), name.trim(), description.trim() || undefined);
            setUrl('');
            setName('');
            setDescription('');
        }
    }, [isFormValid, onAddWebPage, url, name, description]);

    return (
        <Dialog open={isOpen} onOpenChange={(_, data) => onOpenChange(data.open)}>
            <DialogSurface className={styles.dialogSurface}>
                <DialogBody className={styles.dialogBody}>
                    <DialogTitle>
                        <div className={styles.dialogTitleContainer}>
                            <Globe20Regular />
                            {intl.formatMessage(KnowledgeSettingsResources.addWebPage)}
                        </div>
                    </DialogTitle>
                    <DialogContent className={styles.dialogContent}>
                        <Text>{intl.formatMessage(KnowledgeSettingsResources.addWebPageDescription)}</Text>

                        <Field label={intl.formatMessage(KnowledgeSettingsResources.webPageUrlLabel)} required className={styles.formField}>
                            <Input
                                value={url}
                                onChange={(_, data) => setUrl(data.value)}
                                placeholder={intl.formatMessage(KnowledgeSettingsResources.webPageUrlPlaceholder)}
                            />
                        </Field>

                        <Field label={intl.formatMessage(SreAgentResources.name)} required className={styles.formField}>
                            <Input
                                value={name}
                                onChange={(_, data) => setName(data.value)}
                                placeholder={intl.formatMessage(KnowledgeSettingsResources.webPageNamePlaceholder)}
                            />
                        </Field>

                        <Field label={intl.formatMessage(PermissionsResources.description)} className={styles.formField}>
                            <Input
                                value={description}
                                onChange={(_, data) => setDescription(data.value)}
                                placeholder={intl.formatMessage(KnowledgeSettingsResources.webPageDescriptionPlaceholder)}
                            />
                        </Field>
                    </DialogContent>
                </DialogBody>
                <DialogActions className={styles.dialogFooter}>
                    <Button appearance="primary" onClick={handleAddWebPage} disabled={!isFormValid || isAdding}>
                        {intl.formatMessage(KnowledgeSettingsResources.addWebPage)}
                    </Button>
                    <Button appearance="secondary" onClick={handleCancel}>
                        {intl.formatMessage(SreAgentResources.cancel)}
                    </Button>
                </DialogActions>
            </DialogSurface>
        </Dialog>
    );
};
