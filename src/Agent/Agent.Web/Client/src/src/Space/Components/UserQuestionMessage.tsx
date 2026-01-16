import { makeStyles, mergeClasses, tokens } from '@fluentui/react-components';
import { ChatBubblesQuestion24Regular, Checkmark12Regular } from '@fluentui/react-icons';
import { memo, useCallback, useState } from 'react';
import { useIntl } from 'react-intl';
import { UserQuestion, UserQuestionResponse } from '../../Common/Contracts/DataPlane/UserQuestion';
import { SreAgentResources } from '../../Strings/SREAgentResources';

interface UserQuestionMessageProps {
    userQuestion: UserQuestion;
    onSubmitResponse?: (questionId: string, response: UserQuestionResponse) => void;
}

const useStyles = makeStyles({
    root: {
        display: 'flex',
        flexDirection: 'column',
        gap: '8px',
        padding: '12px 16px',
        backgroundColor: tokens.colorNeutralBackground2,
        borderRadius: '8px',
        border: `1px solid ${tokens.colorNeutralStroke1}`,
        borderLeft: `3px solid ${tokens.colorBrandBackground}`,
    },
    rootAnswered: {
        borderLeftColor: tokens.colorNeutralStroke1,
        opacity: 0.85,
    },
    header: {
        display: 'flex',
        alignItems: 'center',
        gap: '6px',
    },
    headerIcon: {
        color: tokens.colorBrandForeground1,
        display: 'flex',
        alignItems: 'center',
    },
    questionText: {
        fontSize: '13px',
        lineHeight: '20px',
        color: tokens.colorNeutralForeground1,
        fontWeight: 500,
        flex: 1,
    },
    optionsList: {
        display: 'flex',
        flexDirection: 'column',
        gap: '2px',
    },
    optionRow: {
        display: 'flex',
        alignItems: 'center',
        gap: '8px',
        padding: '6px 8px',
        borderRadius: '4px',
        cursor: 'pointer',
        color: tokens.colorNeutralForeground2,
        fontSize: '13px',
        transition: 'all 0.1s ease',
        backgroundColor: 'transparent',
        border: 'none',
        width: '100%',
        textAlign: 'left',
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground1Hover,
            color: tokens.colorNeutralForeground1,
        },
    },
    optionRowSelected: {
        backgroundColor: tokens.colorBrandBackground2,
        color: tokens.colorBrandForeground1,
        ':hover': {
            backgroundColor: tokens.colorBrandBackground2,
            color: tokens.colorBrandForeground1,
        },
    },
    optionRowDisabled: {
        cursor: 'default',
    },
    optionRowDimmed: {
        opacity: 0.4,
        ':hover': {
            backgroundColor: 'transparent',
            color: tokens.colorNeutralForeground2,
        },
    },
    optionNumber: {
        fontSize: '11px',
        fontWeight: 600,
        color: tokens.colorNeutralForeground4,
        minWidth: '16px',
    },
    optionNumberSelected: {
        color: tokens.colorBrandForeground1,
    },
    optionText: {
        flex: 1,
    },
    optionDescription: {
        color: tokens.colorNeutralForeground4,
        fontSize: '12px',
    },
    checkIcon: {
        color: tokens.colorBrandForeground1,
        marginLeft: 'auto',
    },
    freeTextInput: {
        flex: 1,
        backgroundColor: 'transparent',
        border: 'none',
        outline: 'none',
        color: tokens.colorNeutralForeground1,
        fontSize: '13px',
        padding: 0,
        '::placeholder': {
            color: tokens.colorNeutralForeground4,
        },
    },
});

const UserQuestionMessage = ({ userQuestion, onSubmitResponse }: UserQuestionMessageProps) => {
    const classes = useStyles();
    const intl = useIntl();
    const [freeTextValue, setFreeTextValue] = useState('');
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [focusedIndex, setFocusedIndex] = useState<number | null>(null);

    const isAnswered = userQuestion.status === 'Answered';
    const isPending = userQuestion.status === 'Pending';
    const hasOptions = userQuestion.options && userQuestion.options.length > 0;
    const freeTextIndex = (userQuestion.options?.length || 0) + 1;

    const handleOptionClick = useCallback(
        (label: string) => {
            if (!onSubmitResponse) {
                console.warn('[UserQuestionMessage] onSubmitResponse is not defined');
                return;
            }
            console.log('[UserQuestionMessage] Clicked option:', {
                label,
                questionId: userQuestion.questionId,
                questionText: userQuestion.question,
            });
            setIsSubmitting(true);
            onSubmitResponse(userQuestion.questionId, { selectedLabel: label });
        },
        [onSubmitResponse, userQuestion.questionId, userQuestion.question]
    );

    const handleFreeTextSubmit = useCallback(() => {
        if (!freeTextValue.trim() || isAnswered || isSubmitting || !onSubmitResponse) {
            console.log('[UserQuestionMessage] Free text submit blocked:', {
                freeTextValue: freeTextValue.trim(),
                isAnswered,
                isSubmitting,
                hasOnSubmitResponse: !!onSubmitResponse,
            });
            return;
        }
        console.log('[UserQuestionMessage] Submitting free text:', {
            freeText: freeTextValue.trim(),
            questionId: userQuestion.questionId,
        });
        setIsSubmitting(true);
        onSubmitResponse(userQuestion.questionId, { freeText: freeTextValue.trim() });
    }, [freeTextValue, isAnswered, isSubmitting, onSubmitResponse, userQuestion.questionId]);

    const rootClasses = mergeClasses(classes.root, isAnswered && classes.rootAnswered);

    const selectedOptionIndex = userQuestion.selectedOptionLabel
        ? (userQuestion.options?.findIndex(o => o.label === userQuestion.selectedOptionLabel) ?? -1)
        : -1;

    return (
        <div className={rootClasses}>
            <div className={classes.header}>
                <span className={classes.headerIcon}>
                    <ChatBubblesQuestion24Regular />
                </span>
                <div className={classes.questionText}>{userQuestion.question}</div>
            </div>

            <div className={classes.optionsList}>
                {hasOptions &&
                    userQuestion.options.map((option, index) => {
                        const isSelected = selectedOptionIndex === index;
                        const isDimmed = isAnswered && !isSelected;

                        const rowClasses = mergeClasses(
                            classes.optionRow,
                            isSelected && classes.optionRowSelected,
                            isAnswered && classes.optionRowDisabled,
                            isDimmed && classes.optionRowDimmed
                        );

                        return (
                            <button
                                key={`${option.label}-${index}`}
                                type="button"
                                className={rowClasses}
                                onClick={() => handleOptionClick(option.label)}
                                onMouseEnter={() => !isAnswered && setFocusedIndex(index + 1)}
                                onMouseLeave={() => !isAnswered && setFocusedIndex(null)}
                                disabled={isAnswered || isSubmitting}
                            >
                                <span className={mergeClasses(classes.optionNumber, isSelected && classes.optionNumberSelected)}>
                                    {index + 1}.
                                </span>
                                <span className={classes.optionText}>
                                    {option.label}
                                    {option.description && <span className={classes.optionDescription}> — {option.description}</span>}
                                </span>
                                {isSelected && <Checkmark12Regular className={classes.checkIcon} />}
                            </button>
                        );
                    })}

                {userQuestion.allowFreeText && isPending && (
                    <div
                        className={mergeClasses(classes.optionRow, focusedIndex === freeTextIndex && classes.optionRowSelected)}
                        onMouseEnter={() => setFocusedIndex(freeTextIndex)}
                        onMouseLeave={() => setFocusedIndex(null)}
                    >
                        <span
                            className={mergeClasses(classes.optionNumber, focusedIndex === freeTextIndex && classes.optionNumberSelected)}
                        >
                            {freeTextIndex}.
                        </span>
                        <input
                            className={classes.freeTextInput}
                            placeholder={intl.formatMessage(SreAgentResources.userQuestionPlaceholder)}
                            value={freeTextValue}
                            onChange={e => setFreeTextValue(e.target.value)}
                            onFocus={() => setFocusedIndex(freeTextIndex)}
                            onBlur={() => setFocusedIndex(null)}
                            onKeyDown={e => e.key === 'Enter' && handleFreeTextSubmit()}
                            disabled={isSubmitting}
                        />
                    </div>
                )}

                {userQuestion.allowFreeText && isAnswered && userQuestion.freeTextResponse && (
                    <div className={mergeClasses(classes.optionRow, classes.optionRowSelected, classes.optionRowDisabled)}>
                        <span className={mergeClasses(classes.optionNumber, classes.optionNumberSelected)}>{freeTextIndex}.</span>
                        <span className={classes.optionText}>{userQuestion.freeTextResponse}</span>
                        <Checkmark12Regular className={classes.checkIcon} />
                    </div>
                )}
            </div>
        </div>
    );
};

export default memo(UserQuestionMessage);
