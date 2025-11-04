import repeat from 'lodash/repeat';

const replaceGuidTemplate = (template: string): string =>
    template.replace(/[xy]/g, character => {
        const randomNibble = (Math.random() * 16) | 0;
        const value = character === 'x' ? randomNibble : (randomNibble & 0x3) | 0x8;
        return value.toString(16);
    });

export const newGuid = (): string => replaceGuidTemplate('xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx');

export const newShortGuid = (): string => replaceGuidTemplate('xxxxxxxx-yxxx');

export const newTinyGuid = (): string => replaceGuidTemplate('yxxx');

export const newCustomGuid = (length: number): string => {
    if (length <= 0) {
        return '';
    }

    return replaceGuidTemplate(repeat('x', length));
};

export const isValid = (input: string): boolean => /^[0-9a-fA-F]{8}-([0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}$/.test(input);
