import { makeStyles, mergeClasses, Spinner, tokens } from '@fluentui/react-components';
import { ChatBubblesQuestion24Regular, Checkmark12Regular, ChevronDown16Regular, ChevronRight16Regular } from '@fluentui/react-icons';
import { memo, useCallback, useState } from 'react';
import { useIntl } from 'react-intl';
import { UserQuestion, UserQuestionResponse } from '../../Common/Contracts/DataPlane/UserQuestion';
import { SreAgentResources } from '../../Strings/SREAgentResources';

interface UserQuestionMessageProps {
    userQuestion: UserQuestion;
    onSubmitResponse?: (questionId: string, response: UserQuestionResponse) => void;
}

const useStyles = makeStyles({
    // Summary line - minimal, VS Code style
    summaryLine: {
        display: 'flex',
        alignItems: 'center',
        gap: '6px',
        padding: '2px 0',
        cursor: 'pointer',
        color: tokens.colorNeutralForeground3,
        fontSize: '13px',
        userSelect: 'none',
        ':hover': {
            color: tokens.colorNeutralForeground2,
        },
    },
    chevron: {
        color: tokens.colorNeutralForeground4,
        flexShrink: 0,
        fontSize: '14px',
    },
    summaryIcon: {
        color: tokens.colorBrandForeground1,
        flexShrink: 0,
        display: 'flex',
        alignItems: 'center',
        fontSize: '16px',
    },
    summaryKeyParam: {
        color: tokens.colorNeutralForeground2,
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
        flexShrink: 1,
        minWidth: 0,
    },
    summaryResultInfo: {
        color: tokens.colorNeutralForeground4,
        flexShrink: 0,
        whiteSpace: 'nowrap',
        fontSize: '12px',
    },
    summaryResultInfoAnswered: {
        color: tokens.colorPaletteGreenForeground1,
    },
    separator: {
        flexShrink: 0,
        color: tokens.colorNeutralForeground4,
    },
    spinner: {
        marginLeft: '-2px',
    },

    // Expanded container - minimal left border only
    expandedContainer: {
        borderLeft: `1px solid ${tokens.colorNeutralStroke3}`,
        marginLeft: '7px',
        marginTop: '2px',
        overflow: 'hidden',
    },

    // Content header - minimal
    contentHeader: {
        display: 'flex',
        alignItems: 'center',
        padding: '4px 8px 4px 12px',
        gap: '8px',
    },
    contentHeaderLeft: {
        display: 'flex',
        alignItems: 'center',
        gap: '6px',
        flex: 1,
        minWidth: 0,
    },
    headerIcon: {
        color: tokens.colorBrandForeground1,
        display: 'flex',
        alignItems: 'center',
        flexShrink: 0,
        fontSize: '16px',
    },
    questionText: {
        fontSize: '13px',
        lineHeight: '18px',
        color: tokens.colorNeutralForeground1,
        flex: 1,
    },

    // Options container - minimal
    optionsContainer: {
        padding: '4px 0',
    },
    optionsList: {
        display: 'flex',
        flexDirection: 'column',
        gap: '0',
    },
    optionRow: {
        display: 'flex',
        alignItems: 'center',
        gap: '8px',
        padding: '4px 8px 4px 12px',
        cursor: 'pointer',
        color: tokens.colorNeutralForeground3,
        fontSize: '13px',
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
        opacity: 0.5,
        ':hover': {
            backgroundColor: 'transparent',
            color: tokens.colorNeutralForeground3,
        },
    },
    optionNumber: {
        fontSize: '12px',
        color: tokens.colorNeutralForeground4,
        minWidth: '18px',
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
        color: tokens.colorPaletteGreenForeground1,
        marginLeft: 'auto',
        fontSize: '14px',
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
    const [isExpanded, setIsExpanded] = useState(userQuestion.status === 'Pending');

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

    const handleToggleExpand = useCallback(() => {
        setIsExpanded(prev => !prev);
    }, []);

    const handleKeyDown = useCallback(
        (e: React.KeyboardEvent) => {
            if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                handleToggleExpand();
            }
        },
        [handleToggleExpand]
    );

    const selectedOptionIndex = userQuestion.selectedOptionLabel
        ? (userQuestion.options?.findIndex(o => o.label === userQuestion.selectedOptionLabel) ?? -1)
        : -1;

    // Truncate question for summary line
    const truncatedQuestion =
        userQuestion.question.length > 80 ? userQuestion.question.slice(0, 77) + '...' : userQuestion.question;

    // Get result info text
    const getResultInfo = () => {
        if (isAnswered) {
            if (userQuestion.selectedOptionLabel) {
                return userQuestion.selectedOptionLabel;
            }
            if (userQuestion.freeTextResponse) {
                return userQuestion.freeTextResponse.length > 30
                    ? userQuestion.freeTextResponse.slice(0, 27) + '...'
                    : userQuestion.freeTextResponse;
            }
            return intl.formatMessage(SreAgentResources.userQuestionAnswered);
        }
        return `${userQuestion.options?.length || 0} options`;
    };

    return (
        <div>
            {/* Summary Line */}
            <div
                className={classes.summaryLine}
                onClick={handleToggleExpand}
                onKeyDown={handleKeyDown}
                role="button"
                tabIndex={0}
                aria-expanded={isExpanded}
            >
                {isSubmitting ? (
                    <Spinner size="extra-tiny" className={classes.spinner} />
                ) : isExpanded ? (
                    <ChevronDown16Regular className={classes.chevron} />
                ) : (
                    <ChevronRight16Regular className={classes.chevron} />
                )}

                <span className={classes.summaryIcon}>
                    <ChatBubblesQuestion24Regular />
                </span>

                <span className={classes.summaryKeyParam}>{truncatedQuestion}</span>

                <span className={classes.separator}>·</span>
                <span className={mergeClasses(classes.summaryResultInfo, isAnswered && classes.summaryResultInfoAnswered)}>
                    {getResultInfo()}
                </span>
            </div>

            {/* Expanded Content */}
            {isExpanded && (
                <div className={classes.expandedContainer}>
                    {/* Header with question */}
                    <div className={classes.contentHeader}>
                        <div className={classes.contentHeaderLeft}>
                            <span className={classes.headerIcon}>
                                <ChatBubblesQuestion24Regular />
                            </span>
                            <div className={classes.questionText}>{userQuestion.question}</div>
                        </div>
                    </div>

                    {/* Options list */}
                    <div className={classes.optionsContainer}>
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
                                            <span
                                                className={mergeClasses(
                                                    classes.optionNumber,
                                                    isSelected && classes.optionNumberSelected
                                                )}
                                            >
                                                {index + 1}.
                                            </span>
                                            <span className={classes.optionText}>
                                                {option.label}
                                                {option.description && (
                                                    <span className={classes.optionDescription}> — {option.description}</span>
                                                )}
                                            </span>
                                            {isSelected && <Checkmark12Regular className={classes.checkIcon} />}
                                        </button>
                                    );
                                })}

                            {userQuestion.allowFreeText && isPending && (
                                <div
                                    className={mergeClasses(
                                        classes.optionRow,
                                        focusedIndex === freeTextIndex && classes.optionRowSelected
                                    )}
                                    onMouseEnter={() => setFocusedIndex(freeTextIndex)}
                                    onMouseLeave={() => setFocusedIndex(null)}
                                >
                                    <span
                                        className={mergeClasses(
                                            classes.optionNumber,
                                            focusedIndex === freeTextIndex && classes.optionNumberSelected
                                        )}
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
                                    <span className={mergeClasses(classes.optionNumber, classes.optionNumberSelected)}>
                                        {freeTextIndex}.
                                    </span>
                                    <span className={classes.optionText}>{userQuestion.freeTextResponse}</span>
                                    <Checkmark12Regular className={classes.checkIcon} />
                                </div>
                            )}
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
};

export default memo(UserQuestionMessage);
