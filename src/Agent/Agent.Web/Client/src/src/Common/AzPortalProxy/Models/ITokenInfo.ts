export type TokenTypes = 'arm' | 'sreagent' | 'applicationinsightsapi';

export interface ITokenInfo {
    token: string;
    type: TokenTypes;
}
