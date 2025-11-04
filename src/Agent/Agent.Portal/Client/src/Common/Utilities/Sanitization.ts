import cloneDeep from 'lodash/cloneDeep';

export type AdditionalLogData = { message?: string; resourceId?: string; [key: string]: unknown };

export class RedactedMessage {
    public static readonly passwordOrKeyDetected = 'REDACTED (possible password or key detected)';
    public static readonly freeform = 'REDACTED (freeform)';
}

class Detectors {
    public static readonly uriCredentialTokens = ['token=', 'accountkey=', 'server=', 'password=', 'sharedaccesskey=', 'pwd=', '&sig='];
    public static readonly uriValueTerminators = ['&', '/', '?', '#'];

    public static readonly credentialTokens = [
        ...Detectors.uriCredentialTokens,
        'defaultendpointsprotocol=http',
        'data source=',
        '&amp;sig=',
        'compose|',
        'kube|',
        '.eyj',
    ];
    public static readonly valueTerminators = ['<', '"'];

    public static readonly MinJwtUriLen = 32;

    public static readonly Regex = {
        jwtUriRegex: /([/=]*)eyJ[a-zA-Z0-9=_-]+\.eyJ[^.]*\.[a-zA-Z0-9=_-]+/g,
        appServiceCredentialRegexList: new RegExp(
            [
                /\W[a-z0-9/+]{86}==/i,
                /(azurecr[\s\S]{0,50}|password|-p)[:\s=]([a-z0-9/+]{32,52})/i,
                /(sig=|signature|key|token|secret|password)[\\s\\S]{0,100}[a-z0-9%]{0,73}%3d/i,
                /((microsoft\\.)?maps[\\s\\S]{0,40}|sig=)[a-z0-9\\-_]{43}/i,
                /(key|access|sas|shared|secret|password|credential)[\\s\\S]{0,200}[^a-z0-9/+]([a-z0-9/+]{43}=)/i,
            ]
                .map(regexLiteral => regexLiteral.source)
                .join('|')
        ),
        credentialKeyPattern: /(key|access|sas|shared|secret|password|credential|signature|sig|token)/i,
        credentialValuePattern: /(key|access|sas|shared|secret|password|credential|signature|sig|token)=/i,
        passwordScrub: /password\s*=\s*'.*?'/,
    };
}

export const hasCredentialValueInString = (str: string) => Detectors.Regex.credentialValuePattern.test(str);

export const sanitizeMessageString = (messageOrAction: string): string => {
    let sanitizedMessageString = messageOrAction;

    // Attempt the more targeted sanitization before we test for full-redacts
    sanitizedMessageString = sanitizeString(sanitizedMessageString);

    if (
        !sanitizedMessageString.includes(RedactedMessage.passwordOrKeyDetected) &&
        (Detectors.Regex.appServiceCredentialRegexList.test(sanitizedMessageString) || hasCredentialValueInString(sanitizedMessageString))
    ) {
        sanitizedMessageString = RedactedMessage.passwordOrKeyDetected;
    }

    return sanitizedMessageString;
};

/**
 * Returns a new sanitized data object
 */
export const getSanitizedLogData = <T extends Record<string, unknown>>(logData: T): T => {
    if (!logData) return {} as T;

    const cloned = cloneDeep(logData);

    const sanitizeObjectInPlace = (obj: Record<string, unknown>) => {
        Object.entries(obj).forEach(([key, value]) => {
            if (typeof value === 'string') {
                let newValue = value;
                if (key === 'resourceId') {
                    // Split the resourceId so query strings/passwords are not logged
                    newValue = newValue.split('?')[0].split('=')[0];
                    newValue = sanitizeUriString(newValue);
                }

                // Check to see if the key has a credential keyword, and if so, redact the value
                if (Detectors.Regex.credentialKeyPattern.test(key)) {
                    newValue = RedactedMessage.passwordOrKeyDetected;
                }

                newValue = sanitizeMessageString(newValue);
                obj[key] = newValue;
            } else if (typeof value === 'object' && value !== null) {
                sanitizeObjectInPlace(value as Record<string, unknown>);
            }
        });
    };

    sanitizeObjectInPlace(cloned);
    return cloned;
};

export const sanitizeString = (input: string) => {
    let sanitizedInput = input;

    for (const token of Detectors.credentialTokens) {
        let credStartIndex = sanitizedInput.toLowerCase().indexOf(token, 0);
        while (credStartIndex !== -1) {
            const credEndIndex = Array.from(sanitizedInput).findIndex(
                (char, idx) => idx >= credStartIndex && Detectors.valueTerminators.includes(char)
            );

            sanitizedInput =
                sanitizedInput.substring(0, credStartIndex) +
                RedactedMessage.passwordOrKeyDetected +
                (credEndIndex !== -1 ? sanitizedInput.substring(credEndIndex) : '');
            credStartIndex = sanitizedInput.toLowerCase().indexOf(token, credStartIndex);
        }
    }

    return sanitizedInput;
};

export const sanitizeUriString = (input: string) => {
    let sanitizedInput = input;

    // Handle Java Web Tokens (JWT) in URI
    if (sanitizedInput.length > Detectors.MinJwtUriLen && sanitizedInput.indexOf('.eyJ') !== -1) {
        sanitizedInput = sanitizedInput.replaceAll(Detectors.Regex.jwtUriRegex, RedactedMessage.passwordOrKeyDetected);
    }

    // Handle other sensitive URI tokens
    for (const token of Detectors.uriCredentialTokens) {
        let credStartIndex = sanitizedInput.toLowerCase().indexOf(token, 0);
        while (credStartIndex !== -1) {
            const credEndIndex = Array.from(sanitizedInput).findIndex(
                (char, idx) => idx >= credStartIndex && Detectors.uriValueTerminators.includes(char)
            );

            sanitizedInput =
                sanitizedInput.substring(0, credStartIndex) +
                RedactedMessage.passwordOrKeyDetected +
                (credEndIndex !== -1 ? sanitizedInput.substring(credEndIndex) : '');
            credStartIndex = sanitizedInput.toLowerCase().indexOf(token, credStartIndex);
        }
    }

    return sanitizedInput;
};
