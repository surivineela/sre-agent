import { Badge, Link, mergeClasses, Text } from '@fluentui/react-components';
import { memo } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources } from '../../../Strings/SREAgentResources';
import { ExtendedAgent, ExtendedTool, SystemTool } from '../../Contracts/ExtendedAgentGraph';
import { PrimaryNavItemValues, SecondaryNavItemValues } from '../../Contracts/SreAgentSpace';
import { useAgentSiteNavigate } from '../../Hooks/useAgentSiteNavigate';
import { useExtendedAgentInfoStyles } from '../../Styles/ExtendedAgentGraph.styles';
import { HandoffsTable } from './HandoffsTable';
import { ToolsTable } from './ToolsTable';

type AgentDetailsProps = {
    agent: ExtendedAgent;
    agents: ExtendedAgent[];
    toolNames: string[];
    toolMap: Map<string, ExtendedTool>;
    systemToolMap: Map<string, SystemTool>;
    memoryEnabled: boolean;
    documentCount: number | null;
    skillsEnabled?: boolean;
    allowedSkills?: string[];
};

export const AgentDetails = memo(
    ({
        agent,
        agents,
        toolNames,
        toolMap,
        systemToolMap,
        memoryEnabled,
        documentCount,
        skillsEnabled,
        allowedSkills,
    }: AgentDetailsProps) => {
        const styles = useExtendedAgentInfoStyles();
        const intl = useIntl();
        const navigate = useAgentSiteNavigate();

        return (
            <>
                <div className={styles.paddingVertical10}>
                    <div className={styles.badgeRow}>
                        <Badge appearance="outline" size="medium" className={styles.neutralBadge}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.toolsCountBadge, {
                                count: toolNames.length,
                            })}
                        </Badge>
                        <Badge appearance="outline" size="medium" className={styles.neutralBadge}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.handoffCountBadge, {
                                count: agent.handoffs?.length ?? 0,
                            })}
                        </Badge>
                        {memoryEnabled && (
                            <Badge appearance="outline" size="medium" className={styles.neutralBadge}>
                                {intl.formatMessage(ExtendedAgentsGraphResources.memoryEnabledBadge)}
                            </Badge>
                        )}
                        {skillsEnabled && (
                            <Badge appearance="outline" size="medium" className={styles.neutralBadge}>
                                {intl.formatMessage(ExtendedAgentsGraphResources.skillsEnabledBadge)}
                            </Badge>
                        )}
                    </div>
                    {memoryEnabled && documentCount !== null && (
                        <div className={styles.marginTopLeft}>
                            <Link
                                onClick={() =>
                                    navigate({
                                        primaryNavItemValue: PrimaryNavItemValues.Settings,
                                        secondaryNavItemValue: SecondaryNavItemValues.KnowledgeBase,
                                    })
                                }
                                className={styles.knowledgeBaseLink}
                            >
                                {documentCount > 0
                                    ? `View ${documentCount} documents in Knowledge Base`
                                    : 'No documents in Knowledge Base - Add documents'}
                            </Link>
                        </div>
                    )}
                </div>

                <div className={styles.section}>
                    <Text className={styles.sectionTitle}>{intl.formatMessage(ExtendedAgentsGraphResources.instructionsTitle)}</Text>
                    {agent.instructions && agent.instructions.trim().length > 0 ? (
                        <textarea readOnly value={agent.instructions} className={styles.textArea} />
                    ) : (
                        <Text className={styles.emptyState}>{intl.formatMessage(ExtendedAgentsGraphResources.noInstructions)}</Text>
                    )}
                    {agent.handoffDescription && (
                        <div className={styles.handoffSection}>
                            <Text className={mergeClasses(styles.sectionTitle, styles.marginBottom10)}>
                                {intl.formatMessage(ExtendedAgentsGraphResources.agentHandoffInstructions)}
                            </Text>
                            <textarea readOnly value={agent.handoffDescription} className={styles.textAreaSmall} />
                        </div>
                    )}
                </div>

                <div className={styles.section}>
                    <Text className={styles.sectionTitle}>{intl.formatMessage(ExtendedAgentsGraphResources.tools)}</Text>
                    <ToolsTable toolNames={toolNames} toolMap={toolMap} systemToolMap={systemToolMap} />
                </div>

                <div className={styles.section}>
                    <Text className={styles.sectionTitle}>{intl.formatMessage(ExtendedAgentsGraphResources.skillsLabel)}</Text>
                    {skillsEnabled ? (
                        allowedSkills && allowedSkills.length > 0 ? (
                            <Text>{allowedSkills.join(', ')}</Text>
                        ) : (
                            <Text className={styles.emptyState}>{intl.formatMessage(ExtendedAgentsGraphResources.allSkillsAllowed)}</Text>
                        )
                    ) : (
                        <Text className={styles.emptyState}>{intl.formatMessage(ExtendedAgentsGraphResources.skillsDisabledLabel)}</Text>
                    )}
                </div>

                <div className={styles.section}>
                    <Text className={styles.sectionTitle}>{intl.formatMessage(ExtendedAgentsGraphResources.handoffsSectionTitle)}</Text>
                    <HandoffsTable handoffs={agent.handoffs ?? []} agents={agents} toolMap={toolMap} systemToolMap={systemToolMap} />
                </div>
            </>
        );
    }
);

AgentDetails.displayName = 'AgentDetails';
