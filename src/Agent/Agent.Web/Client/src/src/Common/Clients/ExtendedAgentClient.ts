import axios from 'axios';
import {
    ExtendedAgent,
    ExtendedConnector,
    ExtendedTool,
    GeneratePythonToolRequest,
    GeneratePythonToolResponse,
    PaginatedResponse,
    PromptImprovementResponse,
    Skill,
    TestPythonToolRequest,
    TestPythonToolResponse,
} from '../../Space/Contracts/ExtendedAgentGraph.ts';
import { buildMetaAgentYaml, convertExtendedEntityToYaml } from '../../Space/Graph/ExtendedAgentYamlUtils.ts';
import { ConnectorStatus } from '../Contracts/Azure/SreAgent.ts';
import { getAgentHeaders } from '../Helpers/headers';
import { DataPlaneClient, Response } from './DataPlaneClient.ts';

export class ExtendedAgentClient extends DataPlaneClient {
    private static _instance: ExtendedAgentClient;

    public static getInstance(sreAgentEndpoint: string): ExtendedAgentClient {
        if (!ExtendedAgentClient._instance) {
            ExtendedAgentClient._instance = new ExtendedAgentClient(sreAgentEndpoint);
        }
        return ExtendedAgentClient._instance;
    }

    constructor(sreAgentEndpoint: string) {
        super(sreAgentEndpoint);
    }

    public getExtendedAgents = async (): Promise<Response<PaginatedResponse<ExtendedAgent>>> => {
        try {
            const { data } = await axios.get(this.getRequestUrl('/api/v1/extendedAgent/agents?page=1&limit=200'), {
                headers: getAgentHeaders(),
            });
            return {
                isSuccessful: true,
                content: data,
            };
        } catch (e) {
            return {
                isSuccessful: false,
                error: e,
            };
        }
    };

    public deleteExtendedAgent = async (agentName: string): Promise<Response<void>> => {
        try {
            await axios.delete(this.getRequestUrl(`/api/v1/extendedAgent/agents/${encodeURIComponent(agentName)}`), {
                headers: getAgentHeaders(),
            });
            return {
                isSuccessful: true,
                content: undefined,
            };
        } catch (e) {
            return {
                isSuccessful: false,
                error: e,
            };
        }
    };

    public testKustoTool = async (tool: Partial<ExtendedTool>) => {
        try {
            const { data } = await axios.post(
                this.getRequestUrl('/api/v1/extendedAgent/tools/kusto/test'),
                {
                    query: tool.query,
                    connector: tool.connector || '',
                    database: tool.database,
                    mode: tool.mode || 'query',
                    parameters: tool.parameters?.reduce(
                        (acc, param) => {
                            acc[param.name] = param.value;
                            return acc;
                        },
                        {} as { [key: string]: any }
                    ),
                },
                {
                    headers: getAgentHeaders(),
                }
            );
            return {
                isSuccessful: true,
                content: data,
            };
        } catch (error) {
            const errorMessage = this.getErrorMessage(error);
            return {
                isSuccessful: false,
                error: errorMessage,
            };
        }
    };

    public deleteKustoTool = async (toolName: string) => {
        const encodedToolName = encodeURIComponent(toolName);
        try {
            const { data } = await axios.delete(this.getRequestUrl(`/api/v1/extendedAgent/tools/${encodedToolName}`), {
                headers: getAgentHeaders(),
            });
            return {
                isSuccessful: true,
                content: data,
            };
        } catch (error) {
            const errorMessage = this.getErrorMessage(error);
            return {
                isSuccessful: false,
                error: errorMessage,
            };
        }
    };

    public applyEntity = async (
        data: Partial<ExtendedAgent> | Partial<ExtendedTool> | Partial<ExtendedConnector>,
        type: 'agent' | 'tool' | 'connector'
    ): Promise<Response<string | undefined>> => {
        try {
            const yamlDocuments = this.convertExtendedEntityToYamlDocuments(data, type);
            for (let i = 0; i < yamlDocuments.length; i++) {
                if (!yamlDocuments[i]) continue;

                await axios.put(this.getRequestUrl('/api/v1/extendedAgent/apply'), yamlDocuments[i], {
                    headers: {
                        ...getAgentHeaders(),
                        'Content-Type': 'application/x-yaml',
                    },
                });
            }
            return {
                isSuccessful: true,
            };
        } catch (error) {
            const errorMessage = this.getErrorMessage(error);
            return {
                isSuccessful: false,
                error: errorMessage,
            };
        }
    };

    public getPromptImprovement = async (prompt: string): Promise<Response<PromptImprovementResponse>> => {
        try {
            const { data } = await axios.post(
                this.getRequestUrl('/api/v1/extendedAgent/prompt-improvement'),
                { prompt },
                {
                    headers: {
                        ...getAgentHeaders(),
                        'Content-Type': 'application/json',
                    },
                }
            );
            return {
                isSuccessful: true,
                content: data,
            };
        } catch (error) {
            const errorMessage = this.getErrorMessage(error);
            return {
                isSuccessful: false,
                error: errorMessage,
            };
        }
    };

    public generatePythonTool = async (request: GeneratePythonToolRequest): Promise<Response<GeneratePythonToolResponse>> => {
        try {
            const { data } = await axios.post(this.getRequestUrl('/api/v1/extendedAgent/generate-python-tool'), request, {
                headers: {
                    ...getAgentHeaders(),
                    'Content-Type': 'application/json',
                },
            });
            return {
                isSuccessful: true,
                content: data,
            };
        } catch (error) {
            const errorMessage = this.getErrorMessage(error);
            return {
                isSuccessful: false,
                error: errorMessage,
            };
        }
    };

    public testPythonTool = async (request: TestPythonToolRequest): Promise<Response<TestPythonToolResponse>> => {
        try {
            const { data } = await axios.post(this.getRequestUrl('/api/v1/extendedAgent/tools/python/test'), request, {
                headers: {
                    ...getAgentHeaders(),
                    'Content-Type': 'application/json',
                },
            });
            return {
                isSuccessful: true,
                content: data,
            };
        } catch (error) {
            const errorMessage = this.getErrorMessage(error);
            return {
                isSuccessful: false,
                error: errorMessage,
            };
        }
    };

    public getSkills = async (): Promise<Response<Skill[]>> => {
        try {
            const { data } = await axios.get(this.getRequestUrl('/api/v2/extendedAgent/skills?limit=200'), {
                headers: getAgentHeaders(),
            });
            // v2 API returns { value: [...] } format
            const skills = (data.value ?? []).map((item: any) => ({
                name: item.name,
                description: item.properties?.description,
                tools: item.properties?.tools,
                skillMdContent: item.properties?.skillMdContent,
                additionalFiles: item.properties?.additionalFiles,
            }));
            return {
                isSuccessful: true,
                content: skills,
            };
        } catch (e) {
            return {
                isSuccessful: false,
                error: e,
            };
        }
    };

    public getSkill = async (skillName: string): Promise<Response<Skill>> => {
        try {
            const { data } = await axios.get(this.getRequestUrl(`/api/v2/extendedAgent/skills/${encodeURIComponent(skillName)}`), {
                headers: getAgentHeaders(),
            });
            // v2 API returns envelope format
            const skill: Skill = {
                name: data.name,
                description: data.properties?.description,
                tools: data.properties?.tools,
                skillMdContent: data.properties?.skillMdContent,
                additionalFiles: data.properties?.additionalFiles,
            };
            return {
                isSuccessful: true,
                content: skill,
            };
        } catch (e) {
            return {
                isSuccessful: false,
                error: e,
            };
        }
    };

    public createOrUpdateSkill = async (skill: Skill): Promise<Response<Skill>> => {
        try {
            const requestBody = {
                name: skill.name,
                type: 'Skill',
                properties: {
                    description: skill.description,
                    tools: skill.tools,
                    skillMdContent: skill.skillMdContent,
                    additionalFiles: skill.additionalFiles?.map(file => ({
                        fileName: file.fileName,
                        filePath: file.filePath || file.fileName,
                        content: file.content,
                    })),
                },
            };

            await axios.put(this.getRequestUrl(`/api/v2/extendedAgent/skills/${encodeURIComponent(skill.name)}`), requestBody, {
                headers: {
                    ...getAgentHeaders(),
                    'Content-Type': 'application/json',
                },
            });
            return {
                isSuccessful: true,
                content: skill,
            };
        } catch (e) {
            return {
                isSuccessful: false,
                error: e,
            };
        }
    };

    public deleteSkill = async (skillName: string): Promise<Response<void>> => {
        try {
            await axios.delete(this.getRequestUrl(`/api/v2/extendedAgent/skills/${encodeURIComponent(skillName)}`), {
                headers: getAgentHeaders(),
            });
            return {
                isSuccessful: true,
                content: undefined,
            };
        } catch (e) {
            return {
                isSuccessful: false,
                error: e,
            };
        }
    };

    public getConnectorStatus = async (connectorName: string): Promise<Response<ConnectorStatus>> => {
        const { data } = await axios.get(
            this.getRequestUrl(`/api/v2/extendedAgent/connectors/${encodeURIComponent(connectorName)}/status`),
            {
                headers: getAgentHeaders(),
            }
        );
        return {
            isSuccessful: !!data,
            content: data,
        };
    };

    private convertExtendedEntityToYamlDocuments(
        data: Partial<ExtendedAgent> | Partial<ExtendedTool> | Partial<ExtendedConnector>,
        type: 'agent' | 'tool' | 'connector'
    ) {
        // Generate the YAML content (this may include multiple documents for meta agent override)
        let yamlContent = convertExtendedEntityToYaml(data, type);

        // If this is an agent with meta agent override enabled, append the meta agent YAML
        if (type === 'agent' && (data as any)?.metaAgentOverride) {
            const agentData = data as Partial<ExtendedAgent>;

            // Create the user's agent first (without meta agent override flag for YAML generation)
            const userAgentData = { ...agentData };
            delete (userAgentData as any).metaAgentOverride;
            const userAgentYaml = convertExtendedEntityToYaml(userAgentData, type);

            // Get the meta agent YAML (which already starts with ---)
            const metaAgentYaml = buildMetaAgentYaml();

            // Combine documents - userAgentYaml doesn't have --- at start, metaAgentYaml does
            yamlContent = userAgentYaml.trim() + '\n' + metaAgentYaml;
        }

        // Split YAML content on document separators and apply each document
        // Handle both cases: documents that start with --- and those that don't
        let yamlDocuments: string[];
        if (yamlContent.includes('\n---\n')) {
            // Multi-document YAML with separators
            yamlDocuments = yamlContent.split(/\n---\n/).filter(document => document.trim().length > 0);
        } else {
            // Single document
            yamlDocuments = [yamlContent];
        }

        return yamlDocuments.map(document => {
            return document.replace(/^---\s*\n?/, '').trim();
        });
    }
}
