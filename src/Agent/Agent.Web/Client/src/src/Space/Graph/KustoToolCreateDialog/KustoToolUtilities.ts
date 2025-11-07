import { ToolParameter } from '../../Contracts/ExtendedAgentGraph';

export interface KustoToolFormProps {
    name: string;
    description: string;
    connector?: string;
    database?: string;
    query?: string;
    parameters?: ToolParameter[];
}
