import { Table, TableBody, TableCell, TableHeader, TableHeaderCell, TableRow, Text } from '@fluentui/react-components';
import { memo } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources } from '../../../Strings/SREAgentResources';
import { ExtendedAgent, ExtendedTool, SystemTool } from '../../Contracts/ExtendedAgentGraph';
import { useExtendedAgentInfoStyles } from '../../Styles/ExtendedAgentGraph.styles';
import { EntityIcon } from '../EntityIcon';

const EMPTY_DISPLAY = '-' as const;

type HandoffsTableProps = {
    handoffs: string[];
    agents: ExtendedAgent[];
    toolMap: Map<string, ExtendedTool>;
    systemToolMap: Map<string, SystemTool>;
};

export const HandoffsTable = memo(({ handoffs, agents, toolMap, systemToolMap }: HandoffsTableProps) => {
    const styles = useExtendedAgentInfoStyles();
    const intl = useIntl();

    if (!handoffs || handoffs.length === 0) {
        return <Text className={styles.emptyState}>{intl.formatMessage(ExtendedAgentsGraphResources.noHandoffs)}</Text>;
    }

    return (
        <Table>
            <TableHeader>
                <TableRow>
                    <TableHeaderCell className={styles.tableCellTruncate}>
                        <Text
                            weight="semibold"
                            className={styles.tableCellTextTruncate}
                            title={intl.formatMessage(ExtendedAgentsGraphResources.agentName)}
                        >
                            {intl.formatMessage(ExtendedAgentsGraphResources.agentName)}
                        </Text>
                    </TableHeaderCell>
                    <TableHeaderCell className={styles.tableCellTruncate}>
                        <Text
                            weight="semibold"
                            className={styles.tableCellTextTruncate}
                            title={intl.formatMessage(ExtendedAgentsGraphResources.tools)}
                        >
                            {intl.formatMessage(ExtendedAgentsGraphResources.tools)}
                        </Text>
                    </TableHeaderCell>
                </TableRow>
            </TableHeader>
            <TableBody>
                {handoffs.map(handoffAgentName => {
                    const handoffAgent = agents.find(agent => agent.name === handoffAgentName);

                    const explicitSystemTools = handoffAgent?.systemTools ?? [];
                    const implicitSystemTools = handoffAgent?.tools?.filter(toolName => systemToolMap.has(toolName)) ?? [];
                    const systemToolCount = Array.from(new Set([...explicitSystemTools, ...implicitSystemTools])).length;

                    const kustoToolCount =
                        handoffAgent?.tools?.filter(toolName => {
                            const tool = toolMap.get(toolName);
                            return tool?.type === 'KustoTool';
                        })?.length ?? 0;

                    const pythonToolCount =
                        handoffAgent?.tools?.filter(toolName => {
                            const tool = toolMap.get(toolName);
                            return tool?.type === 'PythonFunctionTool';
                        })?.length ?? 0;

                    return (
                        <TableRow key={handoffAgentName}>
                            <TableCell className={styles.tableCellTruncate}>
                                <div className={styles.flexRowCenter8}>
                                    <Text title={handoffAgentName} className={styles.tableCellTextTruncate}>
                                        {handoffAgentName}
                                    </Text>
                                </div>
                            </TableCell>
                            <TableCell className={styles.tableCellTruncate}>
                                <div className={styles.flexRowCenter8}>
                                    {systemToolCount === 0 && kustoToolCount === 0 && pythonToolCount === 0 ? (
                                        <Text title={EMPTY_DISPLAY} className={styles.tableCellTextTruncate}>
                                            {EMPTY_DISPLAY}
                                        </Text>
                                    ) : (
                                        <>
                                            {systemToolCount > 0 && (
                                                <div className={styles.flexRowCenter4}>
                                                    <EntityIcon
                                                        type="tool"
                                                        shorthandStyle={{
                                                            wrapperSize: 20,
                                                            iconSize: 16,
                                                            borderRadius: 3,
                                                        }}
                                                    />
                                                    <Text title={systemToolCount.toString()} className={styles.tableCellTextTruncate}>
                                                        {systemToolCount}
                                                    </Text>
                                                </div>
                                            )}

                                            {kustoToolCount > 0 && (
                                                <div className={styles.flexRowCenter4}>
                                                    <EntityIcon
                                                        type="toolWithGear"
                                                        shorthandStyle={{
                                                            wrapperSize: 20,
                                                            iconSize: 16,
                                                            borderRadius: 3,
                                                        }}
                                                    />
                                                    <Text title={kustoToolCount.toString()} className={styles.tableCellTextTruncate}>
                                                        {kustoToolCount}
                                                    </Text>
                                                </div>
                                            )}

                                            {pythonToolCount > 0 && (
                                                <div className={styles.flexRowCenter4}>
                                                    <EntityIcon
                                                        type="pythonTool"
                                                        shorthandStyle={{
                                                            wrapperSize: 20,
                                                            iconSize: 16,
                                                            borderRadius: 3,
                                                        }}
                                                    />
                                                    <Text title={pythonToolCount.toString()} className={styles.tableCellTextTruncate}>
                                                        {pythonToolCount}
                                                    </Text>
                                                </div>
                                            )}
                                        </>
                                    )}
                                </div>
                            </TableCell>
                        </TableRow>
                    );
                })}
            </TableBody>
        </Table>
    );
});

HandoffsTable.displayName = 'HandoffsTable';
