/**
 * Represents a question presented to the user for interactive response.
 * Similar to Claude Code's AskUserQuestion tool.
 */
export interface UserQuestion {
    /** Unique identifier for this question, used to match responses */
    questionId: string;
    /** The question text to display to the user */
    question: string;
    /** A short header/label for the question (max 12 characters) */
    header: string;
    /** Available options for the user to choose from */
    options: UserQuestionOption[];
    /** Whether to allow free text input in addition to options */
    allowFreeText: boolean;
    /** Current status of the question */
    status: UserQuestionStatus;
    /** The label of the option the user selected (if any) */
    selectedOptionLabel?: string;
    /** The free text response from the user (if any) */
    freeTextResponse?: string;
    /** When the question was created */
    createdAt: string;
    /** When the user answered the question (if answered) */
    answeredAt?: string;
}

/**
 * An option presented to the user in a question.
 */
export interface UserQuestionOption {
    /** Short display text for the option (1-5 words) */
    label: string;
    /** Longer description explaining what this option means */
    description: string;
}

/**
 * Status of a user question.
 */
export type UserQuestionStatus = 'Pending' | 'Answered' | 'Cancelled';

/**
 * Response from the user to a question.
 */
export interface UserQuestionResponse {
    /** The label of the selected option (if user clicked an option) */
    selectedLabel?: string;
    /** Free text response (if user typed a custom response) */
    freeText?: string;
}
