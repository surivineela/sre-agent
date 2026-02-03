import { Link, Table, TableBody, TableCell, TableHeader, TableHeaderCell, TableRow, Text } from '@fluentui/react-components';
import { memo } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources, SreAgentTabResources } from '../../../Strings/SREAgentResources';
import { SystemTool } from '../../Contracts/ExtendedAgentGraph';
import { PrimaryNavItemValues, SecondaryNavItemValues } from '../../Contracts/SreAgentSpace';
import { useAgentSiteNavigate } from '../../Hooks/useAgentSiteNavigate';
import { useExtendedAgentInfoStyles } from '../../Styles/ExtendedAgentGraph.styles';

type SystemToolDetailsProps = {
    systemTool: SystemTool;
};

export const SystemToolDetails = memo(({ systemTool }: SystemToolDetailsProps) => {
    const styles = useExtendedAgentInfoStyles();
    const intl = useIntl();
    const navigate = useAgentSiteNavigate();

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
                            onClick={() =>
                                navigate({
                                    primaryNavItemValue: PrimaryNavItemValues.Settings,
                                    secondaryNavItemValue: SecondaryNavItemValues.KnowledgeBase,
                                })
                            }
                            className={styles.flexRowCenter}
                        >
                            {intl.formatMessage(SreAgentTabResources.knowledgeBase)}
                        </Link>
                        <Link
                            appearance="subtle"
                            onClick={() =>
                                navigate({
                                    primaryNavItemValue: PrimaryNavItemValues.Settings,
                                    secondaryNavItemValue: SecondaryNavItemValues.Connectors,
                                })
                            }
                            className={styles.flexRowCenter}
                        >
                            {intl.formatMessage(SreAgentTabResources.connectors)}
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
