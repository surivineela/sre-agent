import { Badge, Table, TableBody, TableCell, TableHeader, TableHeaderCell, TableRow, Text } from '@fluentui/react-components';
import { DocumentText16Regular } from '@fluentui/react-icons';
import { memo } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources } from '../../../Strings/SREAgentResources';
import { ExtendedTool, Skill, SystemTool } from '../../Contracts/ExtendedAgentGraph';
import { useExtendedAgentInfoStyles } from '../../Styles/ExtendedAgentGraph.styles';
import { EntityIcon } from '../EntityIcon';

const EMPTY_DISPLAY = '-' as const;
const SkillDocTitle = 'SKILL.md';

type SkillDetailsProps = {
    skill: Skill;
    toolMap: Map<string, ExtendedTool>;
    systemToolMap: Map<string, SystemTool>;
};

export const SkillDetails = memo(({ skill, toolMap, systemToolMap }: SkillDetailsProps) => {
    const styles = useExtendedAgentInfoStyles();
    const intl = useIntl();

    return (
        <>
            <div className={styles.paddingVertical10}>
                {skill.description && (
                    <div className={styles.metadataRow}>
                        <Text className={styles.metadataKey}>{intl.formatMessage(ExtendedAgentsGraphResources.description)}</Text>
                        <Text>{skill.description}</Text>
                    </div>
                )}
            </div>

            {skill.tools && skill.tools.length > 0 && (
                <div className={styles.section}>
                    <Text className={styles.sectionTitle}>{intl.formatMessage(ExtendedAgentsGraphResources.tools)}</Text>
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
                            {skill.tools.map(toolName => {
                                const tool = toolMap.get(toolName);
                                const systemTool = systemToolMap.get(toolName);
                                const description = tool?.description || systemTool?.description || EMPTY_DISPLAY;

                                return (
                                    <TableRow key={`skill-tool-${toolName}`}>
                                        <TableCell className={styles.tableCellTruncate}>
                                            <div className={styles.flexRowCenter8}>
                                                <EntityIcon
                                                    type="tool"
                                                    shorthandStyle={{ wrapperSize: 20, iconSize: 16, borderRadius: 4 }}
                                                />
                                                <Text title={toolName} className={styles.tableCellTextTruncate}>
                                                    {toolName}
                                                </Text>
                                            </div>
                                        </TableCell>
                                        <TableCell className={styles.tableCellTruncate}>
                                            <Text title={description} className={styles.tableCellTextTruncate}>
                                                {description}
                                            </Text>
                                        </TableCell>
                                    </TableRow>
                                );
                            })}
                        </TableBody>
                    </Table>
                </div>
            )}

            {(skill.skillMdContent || (skill.additionalFiles && skill.additionalFiles.length > 0)) && (
                <div className={styles.section}>
                    <Text className={styles.sectionTitle}>{intl.formatMessage(ExtendedAgentsGraphResources.skillFiles)}</Text>
                    <Table>
                        <TableHeader>
                            <TableRow>
                                <TableHeaderCell className={styles.tableCellTruncate}>
                                    <Text
                                        weight="semibold"
                                        className={styles.tableCellTextTruncate}
                                        title={intl.formatMessage(ExtendedAgentsGraphResources.fileName)}
                                    >
                                        {intl.formatMessage(ExtendedAgentsGraphResources.fileName)}
                                    </Text>
                                </TableHeaderCell>
                            </TableRow>
                        </TableHeader>
                        <TableBody>
                            {skill.skillMdContent && (
                                <TableRow key="skill-file-skill-md">
                                    <TableCell className={styles.tableCellTruncate}>
                                        <div className={styles.flexRowCenter8}>
                                            <DocumentText16Regular />
                                            <Text title={SkillDocTitle} className={styles.tableCellTextTruncate}>
                                                {SkillDocTitle}
                                            </Text>
                                            <Badge appearance="outline" size="small">
                                                {intl.formatMessage(ExtendedAgentsGraphResources.defaultFile)}
                                            </Badge>
                                        </div>
                                    </TableCell>
                                </TableRow>
                            )}
                            {skill.additionalFiles?.map((file, index) => (
                                <TableRow key={`skill-file-${index}`}>
                                    <TableCell className={styles.tableCellTruncate}>
                                        <div className={styles.flexRowCenter8}>
                                            <DocumentText16Regular />
                                            <Text title={file.filePath} className={styles.tableCellTextTruncate}>
                                                {file.filePath}
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

SkillDetails.displayName = 'SkillDetails';
