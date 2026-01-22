export const ENTITY_NAME_MAX_LENGTH = 36;

const INVALID_CHARACTERS_REGEX = /[^A-Za-z0-9-]/g;
const VALID_NAME_REGEX = /^[A-Za-z0-9-]+$/;

export const sanitizeEntityName = (value: string): string => {
    if (!value) {
        return '';
    }

    const sanitized = value.replace(INVALID_CHARACTERS_REGEX, '');
    return sanitized.slice(0, ENTITY_NAME_MAX_LENGTH);
};

export const isEntityNameValid = (value: string | undefined | null): boolean => {
    if (!value) {
        return false;
    }

    const trimmed = value.trim();
    if (trimmed.length === 0 || trimmed.length > ENTITY_NAME_MAX_LENGTH) {
        return false;
    }

    return VALID_NAME_REGEX.test(trimmed);
};

export const INSTRUCTION_MIN_LENGTH = 50;
export const INSTRUCTION_MAX_LENGTH = 6000;

export const HANDOFF_INSTRUCTION_MAX_LENGTH = 500;
