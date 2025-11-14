import { ToolParameter } from '../../Contracts/ExtendedAgentGraph';

export interface KustoToolFormProps {
    name: string;
    description: string;
    connector?: string;
    database?: string;
    query?: string;
    parameters?: ToolParameter[];
}

/**
 * Extracts the first complete JSON object from a string.
 * @param text - The string containing JSON
 * @returns The extracted JSON string, or null if not found
 */
const extractJsonFromString = (text: string): string | null => {
    const jsonStart = text.indexOf('{');
    if (jsonStart === -1) return null;

    let braceCount = 0;
    let jsonEnd = -1;

    for (let i = jsonStart; i < text.length; i++) {
        if (text[i] === '{') {
            braceCount++;
        } else if (text[i] === '}') {
            braceCount--;
            if (braceCount === 0) {
                jsonEnd = i;
                break;
            }
        }
    }

    return jsonEnd !== -1 ? text.substring(jsonStart, jsonEnd + 1) : null;
};

/**
 * Attempts to parse JSON from a string property, handling embedded JSON safely.
 * @param value - The string value that might contain JSON
 * @returns Parsed JSON object or null if parsing fails
 */
const tryParseNestedJson = (value: string): any | null => {
    try {
        const jsonStr = extractJsonFromString(value);
        return jsonStr ? JSON.parse(jsonStr) : null;
    } catch {
        return null;
    }
};

/**
 * Recursively finds the deepest nested error object in a complex error structure.
 * @param errorObj - The initial error object to start drilling down from
 * @param maxDepth - Maximum recursion depth to prevent infinite loops
 * @returns The deepest nested error object found
 */
const findDeepestErrorObject = (errorObj: any, maxDepth: number = 10): any => {
    let currentError = errorObj;
    let depth = 0;

    while (depth < maxDepth) {
        let foundDeeper = false;
        depth++;

        // Check for nested error property
        if (currentError?.error) {
            currentError = currentError.error;
            foundDeeper = true;
            continue;
        }

        // Check for JSON embedded in message properties
        const messageProperties = ['@message', 'message'];
        for (const prop of messageProperties) {
            if (currentError?.[prop] && typeof currentError[prop] === 'string') {
                const nestedError = tryParseNestedJson(currentError[prop]);
                if (nestedError) {
                    currentError = nestedError;
                    foundDeeper = true;
                    break;
                }
            }
        }

        if (!foundDeeper) break;
    }

    return currentError;
};

/**
 * Extracts authorization error message from a Kusto error object.
 * @param errorObj - The error object to check
 * @returns The principal authorization message if found, otherwise null
 */
const extractAuthorizationMessage = (errorObj: any): string | null => {
    const failureCode = errorObj?.['@failureCode'] || errorObj?.failureCode;

    if (failureCode === 403) {
        const message = errorObj?.['@message'] || errorObj?.message;
        if (message && typeof message === 'string' && message.trim().startsWith('Principal')) {
            return message.trim();
        }
    }

    return null;
};

/**
 * Parses a complex Kusto error message string to extract authorization error details.
 * If the @failureCode is 403 and there's a @message starting with "Principal", returns that message.
 * @param errorMessage - The complex error message string from Kusto
 * @returns The extracted principal message if conditions are met, otherwise null
 */
export const parseKustoAuthorizationError = (errorMessage: string): string | null => {
    return parseKustoError(errorMessage, extractAuthorizationMessage);
};

/**
 * Generic function to extract specific error information from nested Kusto errors.
 * @param errorMessage - The error message string
 * @param extractor - Function to extract the desired information from an error object
 * @returns The extracted information or null if not found
 */
export const parseKustoError = <T>(errorMessage: string, extractor: (errorObj: any) => T | null): T | null => {
    try {
        const jsonStr = extractJsonFromString(errorMessage);
        if (!jsonStr) return null;

        const initialError = JSON.parse(jsonStr);
        const deepestError = findDeepestErrorObject(initialError);

        return extractor(deepestError);
    } catch (error) {
        console.error('Error parsing Kusto error message:', error);
        return null;
    }
};

export const truncateErrorMessage = (errorMessage: string) => {
    return errorMessage.slice(0, 400) + '...';
};
