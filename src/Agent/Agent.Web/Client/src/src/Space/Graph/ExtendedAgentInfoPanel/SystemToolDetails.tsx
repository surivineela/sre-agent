import { Link, Table, TableBody, TableCell, TableHeader, TableHeaderCell, TableRow, Text } from '@fluentui/react-components';
import { memo } from 'react';
import { useIntl } from 'react-intl';
import { useNavigate } from 'react-router-dom';
import { ExtendedAgentsGraphResources, SettingsTabResources } from '../../../Strings/SREAgentResources';
import { SystemTool } from '../../Contracts/ExtendedAgentGraph';
import { useExtendedAgentInfoStyles } from '../../Styles/ExtendedAgentGraph.styles';

type SystemToolDetailsProps = {
    systemTool: SystemTool;
};

export const SystemToolDetails = memo(({ systemTool }: SystemToolDetailsProps) => {
    const styles = useExtendedAgentInfoStyles();
    const intl = useIntl();
    const navigate = useNavigate();

    return (
        <>
            <div className={styles.paddingVertical10}>
                <div className={styles.metadataRow}>
                    <Text className={styles.metadataKey}>{intl.formatMessage(ExtendedAgentsGraphResources.category)}</Text>
                    <Text>{systemTool.category}</Text>
                </div>
            </div>

            {systemTool.description && (
                <div className={styles.section}>
                    <Text className={styles.sectionTitle}>{intl.formatMessage(ExtendedAgentsGraphResources.toolDescriptionLabel)}</Text>
                    <Text className={styles.subtitle}>{systemTool.description}</Text>
                </div>
            )}

            {systemTool.name?.toLowerCase() === 'searchmemory' && (
                <div className={styles.section}>
                    <Text className={styles.sectionTitle}>{intl.formatMessage(ExtendedAgentsGraphResources.connectsTo)}</Text>
                    <div className={styles.flexColumnGap8}>
                        <Link
                            appearance="subtle"
                            onClick={() => navigate('/views/settings/dataKnowledgeSpace')}
                            className={styles.flexRowCenter}
                        >
                            {intl.formatMessage(SettingsTabResources.knowledgeBase)}
                        </Link>
                        <Link
                            appearance="subtle"
                            onClick={() => navigate('/views/settings/data-connectors')}
                            className={styles.flexRowCenter}
                        >
                            {intl.formatMessage(SettingsTabResources.connectors)}
                        </Link>
                    </div>
                </div>
            )}

            {systemTool.parameters && systemTool.parameters.length > 0 && (
                <div className={styles.section}>
                    <Text className={styles.sectionTitle}>{intl.formatMessage(ExtendedAgentsGraphResources.parametersSectionTitle)}</Text>
                    <Table>
                        <TableHeader>
                            <TableRow>
                                <TableHeaderCell className={styles.tableCellTruncate}>
                                    <Text
                                        weight="semibold"
                                        className={styles.tableCellTextTruncate}
                                        title={intl.formatMessage(ExtendedAgentsGraphResources.parameter)}
                                    >
                                        {intl.formatMessage(ExtendedAgentsGraphResources.parameter)}
                                    </Text>
                                </TableHeaderCell>
                            </TableRow>
                        </TableHeader>
                        <TableBody>
                            {systemTool.parameters.map((param: string, index: number) => (
                                <TableRow key={index}>
                                    <TableCell className={styles.tableCellTruncate}>
                                        <div className={styles.flexRowCenter8}>
                                            <Text title={param} className={styles.tableCellTextTruncate}>
                                                {param}
                                            </Text>
                                        </div>
                                    </TableCell>
                                </TableRow>
                            ))}
                        </TableBody>
                    </Table>
                </div>
            )}
        </>
    );
});

SystemToolDetails.displayName = 'SystemToolDetails';
