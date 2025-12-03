import { Table, TableBody, TableCell, TableHeader, TableHeaderCell, TableRow, Text } from '@fluentui/react-components';
import { memo } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources } from '../../../Strings/SREAgentResources';
import { ExtendedTool, SystemTool } from '../../Contracts/ExtendedAgentGraph';
import { useExtendedAgentInfoStyles } from '../../Styles/ExtendedAgentGraph.styles';
import { EntityIcon } from '../EntityIcon';

const EMPTY_DISPLAY = '-' as const;

type ToolsTableProps = {
    toolNames: string[];
    toolMap: Map<string, ExtendedTool>;
    systemToolMap: Map<string, SystemTool>;
};

export const ToolsTable = memo(({ toolNames, toolMap, systemToolMap }: ToolsTableProps) => {
    const styles = useExtendedAgentInfoStyles();
    const intl = useIntl();

    if (toolNames.length === 0) {
        return <Text className={styles.emptyState}>{intl.formatMessage(ExtendedAgentsGraphResources.noTools)}</Text>;
    }

    return (
        <Table>
            <TableHeader>
                <TableRow>
                    <TableHeaderCell className={styles.tableCellTruncate}>
                        <Text
                            weight="semibold"
                            className={styles.tableCellTextTruncate}
                            title={intl.formatMessage(ExtendedAgentsGraphResources.toolName)}
                        >
                            {intl.formatMessage(ExtendedAgentsGraphResources.toolName)}
                        </Text>
                    </TableHeaderCell>
                    <TableHeaderCell className={styles.tableCellTruncate}>
                        <Text
                            weight="semibold"
                            className={styles.tableCellTextTruncate}
                            title={intl.formatMessage(ExtendedAgentsGraphResources.description)}
                        >
                            {intl.formatMessage(ExtendedAgentsGraphResources.description)}
                        </Text>
                    </TableHeaderCell>
                </TableRow>
            </TableHeader>
            <TableBody>
                {toolNames.map(name => {
                    const tool = toolMap.get(name);
                    const systemTool = systemToolMap.get(name);
                    const isMcpTool = name.includes('-mcp_') || name.includes('-mcp-');

                    let iconType: 'tool' | 'toolWithGear' | 'pythonTool' | 'windowWrenchRegular' = 'tool';
                    let description = tool?.description || EMPTY_DISPLAY;

                    if (systemTool) {
                        iconType = 'tool';
                        description =
                            systemTool.description || intl.formatMessage(ExtendedAgentsGraphResources.listViewDescriptionFallback);
                    } else if (isMcpTool || tool?.type === 'mcp') {
                        iconType = 'windowWrenchRegular';
                    } else if (tool?.type === 'PythonFunctionTool') {
                        iconType = 'pythonTool';
                    } else if (tool?.type === 'KustoTool') {
                        iconType = 'toolWithGear';
                    }

                    return (
                        <TableRow key={`tool-${name}`}>
                            <TableCell className={styles.tableCellTruncate}>
                                <div className={styles.flexRowCenter8}>
                                    <EntityIcon type={iconType} shorthandStyle={{ wrapperSize: 20, iconSize: 16, borderRadius: 4 }} />
                                    <Text title={name} className={styles.tableCellTextTruncate}>
                                        {name}
                                    </Text>
                                </div>
                            </TableCell>
                            <TableCell className={styles.tableCellTruncate}>
                                <div className={styles.flexRowCenter8}>
                                    <Text title={description} className={styles.tableCellTextTruncate}>
                                        {description}
                                    </Text>
                                </div>
                            </TableCell>
                        </TableRow>
                    );
                })}
            </TableBody>
        </Table>
    );
});

ToolsTable.displayName = 'ToolsTable';
