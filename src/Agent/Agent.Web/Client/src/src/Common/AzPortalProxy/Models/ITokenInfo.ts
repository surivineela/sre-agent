export type TokenTypes = 'arm' | 'sreagent' | 'applicationinsightapi';

export interface ITokenInfo {
    token: string;
    type: TokenTypes;
}
