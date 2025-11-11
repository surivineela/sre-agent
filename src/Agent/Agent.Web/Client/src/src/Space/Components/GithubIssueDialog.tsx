import {
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    DialogTrigger,
    Field,
    Input,
    Link,
    makeStyles,
    Textarea,
    TextareaProps,
    tokens,
} from '@fluentui/react-components';
import { memo, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { GithubIssueResources } from '../../Strings/SREAgentResources';

interface IGithubDialogProps {
    isOpen: boolean;
    setIsOpen: (isOpen: boolean) => void;
    threadId: string | undefined;
}

const useStyles = makeStyles({
    fieldCommon: {
        margin: `${tokens.spacingVerticalM} 0px`,
    },
});

const GithubIssueDialog = ({ isOpen, setIsOpen, threadId }: IGithubDialogProps) => {
    const intl = useIntl();
    const styles = useStyles();

    const [title, setTitle] = useState<string>('');
    const [issueDescription, setIssueDescription] = useState<string>('');
    const [threadIdValue, setThreadIdValue] = useState<string>('');
    const [stepsToReproduce, setStepsToReproduce] = useState<string>('');
    const [expectedBehavior, setExpectedBehavior] = useState<string>('');
    const [actualBehavior, setActualBehavior] = useState<string>('');
    const [titleError, setTitleError] = useState<string>('');
    const [issueDescriptionError, setIssueDescriptionError] = useState<string>('');

    const commonTextareaProps: Partial<TextareaProps> = {
        size: 'large',
        resize: 'vertical',
    };

    const githubIssueUrl = useMemo(() => {
        const githubIssueTitle = `[${intl.formatMessage(GithubIssueResources.titlePrefix)}] ${title}`;

        const issueDescriptionText = `${intl.formatMessage(GithubIssueResources.issueDescriptionField)}\n${issueDescription ? issueDescription : intl.formatMessage(GithubIssueResources.issueDescriptionPlaceholder)}`;
        const threadIdText = `${intl.formatMessage(GithubIssueResources.threadIdField)}\n${threadIdValue ? threadIdValue : intl.formatMessage(GithubIssueResources.threadIdPlaceholder)}`;
        const stepsToReproduceText = `${intl.formatMessage(GithubIssueResources.stepsToReproduceField)}\n${stepsToReproduce ? stepsToReproduce : intl.formatMessage(GithubIssueResources.stepsToReproducePlaceholder)}`;
        const expectedBehaviorText = `${intl.formatMessage(GithubIssueResources.expectedBehaviorField)}\n${expectedBehavior ? expectedBehavior : intl.formatMessage(GithubIssueResources.expectedBehaviorPlaceholder)}`;
        const actualBehaviorText = `${intl.formatMessage(GithubIssueResources.actualBehaviorField)}\n${actualBehavior ? actualBehavior : intl.formatMessage(GithubIssueResources.actualBehaviorPlaceholder)}`;

        const githubIssueBody = `${issueDescriptionText}\n\n${threadIdText}\n\n${stepsToReproduceText}\n\n${expectedBehaviorText}\n\n${actualBehaviorText}`;

        return `https://github.com/microsoft/sre-agent/issues/new?title=${encodeURIComponent(githubIssueTitle)}&body=${encodeURIComponent(githubIssueBody)}`;
    }, [title, issueDescription, threadIdValue, stepsToReproduce, expectedBehavior, actualBehavior, intl]);

    useEffect(() => {
        if (isOpen) {
            setTitle('');
            setIssueDescription('');
            setThreadIdValue(threadId ?? '');
            setStepsToReproduce('');
            setExpectedBehavior('');
            setActualBehavior('');
        }
    }, [isOpen, threadId]);

    return (
        <Dialog
            open={isOpen}
            onOpenChange={(_, data) => {
                setIsOpen(data.open);
            }}
        >
            <DialogSurface>
                <DialogBody>
                    <DialogTitle>{intl.formatMessage(GithubIssueResources.createGithubIssueTitle)}</DialogTitle>
                    <DialogContent>
                        <Field
                            label={intl.formatMessage(GithubIssueResources.titleField)}
                            className={styles.fieldCommon}
                            required
                            validationState={titleError ? 'error' : undefined}
                            validationMessage={titleError}
                        >
                            <Input
                                value={title}
                                onChange={(_, data) => {
                                    setTitle(data.value);
                                    setTitleError(data.value ? '' : intl.formatMessage(GithubIssueResources.titleError));
                                }}
                            />
                        </Field>
                        <Field
                            label={intl.formatMessage(GithubIssueResources.issueDescriptionField)}
                            className={styles.fieldCommon}
                            required
                            validationState={issueDescriptionError ? 'error' : undefined}
                            validationMessage={issueDescriptionError}
                        >
                            <Textarea
                                {...commonTextareaProps}
                                value={issueDescription}
                                onChange={(_, data) => {
                                    setIssueDescription(data.value);
                                    setIssueDescriptionError(
                                        data.value ? '' : intl.formatMessage(GithubIssueResources.issueDescriptionError)
                                    );
                                }}
                                placeholder={intl.formatMessage(GithubIssueResources.issueDescriptionPlaceholder)}
                            />
                        </Field>
                        <Field label={intl.formatMessage(GithubIssueResources.threadIdField)} className={styles.fieldCommon}>
                            <Input
                                value={threadIdValue}
                                onChange={(_, data) => setThreadIdValue(data.value)}
                                placeholder={intl.formatMessage(GithubIssueResources.threadIdPlaceholder)}
                            />
                        </Field>
                        <Field label={intl.formatMessage(GithubIssueResources.stepsToReproduceField)} className={styles.fieldCommon}>
                            <Textarea
                                {...commonTextareaProps}
                                value={stepsToReproduce}
                                onChange={(_, data) => setStepsToReproduce(data.value)}
                                placeholder={intl.formatMessage(GithubIssueResources.stepsToReproducePlaceholder)}
                            />
                        </Field>
                        <Field label={intl.formatMessage(GithubIssueResources.expectedBehaviorField)} className={styles.fieldCommon}>
                            <Textarea
                                {...commonTextareaProps}
                                value={expectedBehavior}
                                onChange={(_, data) => setExpectedBehavior(data.value)}
                                placeholder={intl.formatMessage(GithubIssueResources.expectedBehaviorPlaceholder)}
                            />
                        </Field>
                        <Field label={intl.formatMessage(GithubIssueResources.actualBehaviorField)} className={styles.fieldCommon}>
                            <Textarea
                                {...commonTextareaProps}
                                value={actualBehavior}
                                onChange={(_, data) => setActualBehavior(data.value)}
                                placeholder={intl.formatMessage(GithubIssueResources.actualBehaviorPlaceholder)}
                            />
                        </Field>
                    </DialogContent>
                    <DialogActions fluid>
                        <DialogTrigger>
                            <Link target="_blank" rel="noopener noreferrer" href={githubIssueUrl} disabled={!title || !issueDescription}>
                                {intl.formatMessage(GithubIssueResources.createGithubIssueLinkText)}
                            </Link>
                        </DialogTrigger>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};

export default memo(GithubIssueDialog);
