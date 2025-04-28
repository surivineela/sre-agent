export enum AntUxStringComparison {
    IgnoreCase,
}

export const equals = (str1: string, str2: string, stringComparison?: AntUxStringComparison): boolean => {
    if (typeof str1 !== 'string' || typeof str2 !== 'string') {
        return false;
    }

    if (stringComparison === AntUxStringComparison.IgnoreCase) {
        str1 = str1.toUpperCase();
        str2 = str2.toUpperCase();
    }

    return str1 === str2;
};
