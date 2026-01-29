import {
    Button,
    Card,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    Field,
    Input,
    Link,
    Spinner,
    Text,
    Textarea,
} from '@fluentui/react-components';
import { CheckmarkCircle20Filled, Dismiss24Regular } from '@fluentui/react-icons';
import { FC, useCallback } from 'react';
import { FaGithub } from 'react-icons/fa';
import { useIntl } from 'react-intl';
import {
    ConnectorsResources,
    ExtendedAgentsGraphResources,
    KnowledgeSettingsResources,
    SreAgentResources,
} from '../../../../Strings/SREAgentResources';
import { useAddRepositoryDialogStyles } from './AddRepositoryDialog.styles';
import { useAddRepository } from './useAddRepository';

interface AddRepositoryDialogProps {
    isOpen: boolean;
    onOpenChange: (open: boolean) => void;
    onSuccess?: () => void;
    agentName: string | undefined;
    agentLocation: string | undefined;
}

export const AddRepositoryDialog: FC<AddRepositoryDialogProps> = ({ isOpen, onOpenChange, onSuccess, agentName, agentLocation }) => {
    const intl = useIntl();
    const styles = useAddRepositoryDialogStyles();

    const {
        formState,
        setRepositoryUrl,
        setDisplayName,
        setDescription,
        repositoryType,
        validationErrors,
        isGitHubSignedIn,
        gitHubAccount,
        isSigningIn,
        isAdding,
        handleSignInToGitHub,
        handleSignInWithDifferentAccount,
        handleAddRepository,
        resetForm,
        isFormValid,
    } = useAddRepository(agentName, agentLocation);

    const handleCancel = useCallback(() => {
        resetForm();
        onOpenChange(false);
    }, [resetForm, onOpenChange]);

    const handleSubmit = useCallback(async () => {
        const success = await handleAddRepository();
        if (success) {
            resetForm();
            onOpenChange(false);
            onSuccess?.();
        }
    }, [handleAddRepository, resetForm, onOpenChange, onSuccess]);

    const showGitHubAuth = repositoryType === 'github';

    return (
        <Dialog open={isOpen} onOpenChange={(_, data) => onOpenChange(data.open)}>
            <DialogSurface className={styles.dialogSurface}>
                <DialogBody>
                    <DialogTitle
                        action={
                            <Button
                                appearance="transparent"
                                icon={<Dismiss24Regular />}
                                onClick={handleCancel}
                                aria-label={intl.formatMessage(SreAgentResources.close)}
                            />
                        }
                    >
                        {intl.formatMessage(KnowledgeSettingsResources.addRepository)}
                    </DialogTitle>
                    <DialogContent className={styles.dialogContent}>
                        <Text className={styles.description}>
                            {intl.formatMessage(KnowledgeSettingsResources.addRepositoryDialogDescription)}
                        </Text>

                        <Field
                            label={intl.formatMessage(KnowledgeSettingsResources.repositoryUrlLabel)}
                            required
                            validationState={validationErrors.repositoryUrl ? 'error' : undefined}
                            validationMessage={validationErrors.repositoryUrl}
                            hint={
                                <Text className={styles.hintText}>
                                    {intl.formatMessage(KnowledgeSettingsResources.supportedRepositoriesHint)}
                                </Text>
                            }
                        >
                            <Input
                                value={formState.repositoryUrl}
                                onChange={(_, data) => setRepositoryUrl(data.value)}
                                placeholder={intl.formatMessage(KnowledgeSettingsResources.repositoryUrlPlaceholder)}
                            />
                        </Field>

                        <Field
                            label={intl.formatMessage(KnowledgeSettingsResources.displayNameLabel)}
                            required
                            validationState={validationErrors.displayName ? 'error' : undefined}
                            validationMessage={validationErrors.displayName}
                        >
                            <Input
                                value={formState.displayName}
                                onChange={(_, data) => setDisplayName(data.value)}
                                placeholder={intl.formatMessage(KnowledgeSettingsResources.displayNamePlaceholder)}
                            />
                        </Field>

                        <Field label={intl.formatMessage(ExtendedAgentsGraphResources.description)}>
                            <Textarea
                                value={formState.description}
                                onChange={(_, data) => setDescription(data.value)}
                                placeholder={intl.formatMessage(KnowledgeSettingsResources.repositoryDescriptionPlaceholder)}
                            />
                        </Field>

                        {showGitHubAuth && (
                            <Field
                                label={intl.formatMessage(KnowledgeSettingsResources.gitHubAccountLabel)}
                                required
                                validationState={validationErrors.gitHubAccount ? 'error' : undefined}
                                validationMessage={validationErrors.gitHubAccount}
                            >
                                {!isGitHubSignedIn ? (
                                    <div className={styles.signInLoading}>
                                        <Button
                                            appearance="primary"
                                            onClick={handleSignInToGitHub}
                                            disabled={isSigningIn}
                                            className={styles.signInButton}
                                        >
                                            {intl.formatMessage(KnowledgeSettingsResources.signInToGitHub)}
                                        </Button>
                                        {isSigningIn && (
                                            <>
                                                <Spinner size="tiny" />
                                                <Text>{intl.formatMessage(ConnectorsResources.establishingConnection)}</Text>
                                            </>
                                        )}
                                    </div>
                                ) : (
                                    <>
                                        <Card className={styles.accountCard}>
                                            <div className={styles.accountInfo}>
                                                <FaGithub className={styles.gitHubIcon} />
                                                <div className={styles.accountText}>
                                                    <span className={styles.connectedLabel}>
                                                        {intl.formatMessage(ConnectorsResources.connectedAs)}
                                                    </span>
                                                    <span className={styles.accountEmail}>{gitHubAccount}</span>
                                                </div>
                                            </div>
                                            <CheckmarkCircle20Filled className={styles.checkmark} />
                                        </Card>
                                        <Link
                                            onClick={handleSignInWithDifferentAccount}
                                            className={styles.differentAccountLink}
                                            disabled={isSigningIn}
                                        >
                                            {intl.formatMessage(ConnectorsResources.signInWithDifferentAccount)}
                                        </Link>
                                    </>
                                )}
                            </Field>
                        )}
                    </DialogContent>
                    <DialogActions className={styles.dialogActions}>
                        <Button appearance="primary" onClick={handleSubmit} disabled={!isFormValid || isAdding}>
                            {intl.formatMessage(KnowledgeSettingsResources.addRepositoryButton)}
                        </Button>
                        <Button appearance="secondary" onClick={handleCancel}>
                            {intl.formatMessage(SreAgentResources.cancel)}
                        </Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};
