import { ToolParameter } from '../../Contracts/ExtendedAgentGraph';

export interface KustoToolFormProps {
    name: string;
    instructions: string;
    connector?: string;
    database?: string;
    query?: string;
    parameters?: ToolParameter[];
}
