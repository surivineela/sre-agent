import { Button, Card, CardHeader, makeStyles, tokens } from '@fluentui/react-components';
import { Add20Regular, Agents20Regular, Warning20Regular, Wrench20Regular, WrenchSettings20Regular } from '@fluentui/react-icons';
import { FC } from 'react';
import { useIntl } from 'react-intl';
import ArrowsSvg from '../../../assets/Arrows.svg';
import { TextWithLink } from '../../Common/Components/TextWithLink';
import { SreAgentFwLinks } from '../../Common/Constants/FwLinks';
import { ExtendedAgentsGraphResources } from '../../Strings/SREAgentResources';

const useEmptyStateStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        height: '100%',
        overflowY: 'auto',
        textAlign: 'center',
        backgroundColor: tokens.colorNeutralBackground1,
        backgroundImage: `radial-gradient(${tokens.colorNeutralStroke2} 1px, transparent 0)`,
        backgroundSize: '20px 20px',
    },
    title: {
        fontSize: tokens.fontSizeHero800,
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
        margin: '0',
        marginBottom: '25px',
    },
    description: {
        fontSize: tokens.fontSizeBase300,
        color: tokens.colorNeutralForeground3,
        maxWidth: '600px',
        lineHeight: '1.6',
        margin: '0',
        marginBottom: '20px',
    },
    createButton: {
        marginBottom: '0',
        borderRadius: tokens.borderRadiusXLarge,
    },
    diagramContainer: {
        position: 'relative',
        display: 'flex',
        flexDirection: 'column',
        gap: '48px',
        minHeight: '300px',
        alignItems: 'center',
    },
    diagramSvg: {
        position: 'absolute',
        top: '0',
        left: '0',
        width: '100%',
        height: '100%',
        pointerEvents: 'none',
        zIndex: '0',
    },
    diagramRow: {
        display: 'flex',
        alignItems: 'center',
        gap: '55px',
        justifyContent: 'center',
        position: 'relative',
        zIndex: '1',
    },
    nodeCardWrapper: {
        position: 'relative',
        display: 'inline-block',
    },
    nodeCard: {
        boxShadow: tokens.shadow4,
        minWidth: '100px',
        height: '60px',
        textAlign: 'center',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        borderRadius: tokens.borderRadiusXLarge,
        backgroundColor: tokens.colorNeutralBackground1,
    },
    nodeIconContainer: {
        width: '32px',
        height: '32px',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        borderRadius: '4px',
    },
    nodeLabel: {
        fontSize: '16px',
        fontWeight: tokens.fontWeightSemibold,
        lineHeight: '22px',
    },
    cardStack: {
        position: 'relative',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
    },
    toolsContainer: {
        backgroundColor: 'transparent',
        borderRadius: tokens.borderRadiusXLarge,
        padding: '16px',
        display: 'flex',
        flexDirection: 'column',
        gap: '10px',
        border: `1.5px solid ${tokens.colorNeutralStroke2}`,
    },
    stackedCard: {
        position: 'absolute',
    },
});

interface ExtendedAgentEmptyStateProps {
    onCreateClick: () => void;
}

interface NodeCardProps {
    icon: React.ReactElement;
    label: string;
    backgroundColor: string;
}

const NodeCard: FC<NodeCardProps> = ({ icon, label, backgroundColor }) => {
    const styles = useEmptyStateStyles();

    return (
        <div className={styles.nodeCardWrapper}>
            <Card appearance="subtle" className={styles.nodeCard}>
                <CardHeader
                    image={
                        <div className={styles.nodeIconContainer} style={{ backgroundColor }}>
                            {icon}
                        </div>
                    }
                    header={<span className={styles.nodeLabel}>{label}</span>}
                />
            </Card>
        </div>
    );
};

export const ExtendedAgentEmptyState: FC<ExtendedAgentEmptyStateProps> = ({ onCreateClick }) => {
    const styles = useEmptyStateStyles();
    const intl = useIntl();

    return (
        <div className={styles.container}>
            <div className={styles.diagramContainer}>
                <img src={ArrowsSvg} className={styles.diagramSvg} alt="" aria-hidden="true" />

                <div className={styles.diagramRow}>
                    <NodeCard
                        icon={<Warning20Regular />}
                        label={intl.formatMessage(ExtendedAgentsGraphResources.trigger)}
                        backgroundColor={tokens.colorPaletteCranberryBackground2}
                    />
                    <NodeCard
                        icon={<Agents20Regular />}
                        label={intl.formatMessage(ExtendedAgentsGraphResources.subagent)}
                        backgroundColor={tokens.colorPaletteLavenderBackground2}
                    />
                    <div className={styles.cardStack}>
                        <div className={styles.toolsContainer}>
                            <NodeCard
                                icon={<Wrench20Regular />}
                                label={intl.formatMessage(ExtendedAgentsGraphResources.tool)}
                                backgroundColor={tokens.colorPaletteLilacBackground2}
                            />
                            <NodeCard
                                icon={<WrenchSettings20Regular />}
                                label={intl.formatMessage(ExtendedAgentsGraphResources.tool)}
                                backgroundColor={tokens.colorPaletteLilacBackground2}
                            />
                        </div>
                        <div className={styles.stackedCard} style={{ top: '191px', zIndex: 1 }}>
                            <NodeCard
                                icon={<Agents20Regular />}
                                label={intl.formatMessage(ExtendedAgentsGraphResources.subagent)}
                                backgroundColor={tokens.colorPaletteLavenderBackground2}
                            />
                        </div>
                    </div>
                </div>
            </div>

            <h2 className={styles.title}>{intl.formatMessage(ExtendedAgentsGraphResources.scaleYourAgentsCapabilitiesWithSubagents)}</h2>

            <p className={styles.description}>
                <TextWithLink
                    text={intl.formatMessage(ExtendedAgentsGraphResources.emptyStateDescription)}
                    linkText={intl.formatMessage(ExtendedAgentsGraphResources.emptyStateDescriptionLearnMore)}
                    linkUrl={SreAgentFwLinks.learnMoreAboutSubagents}
                />
            </p>

            <Button appearance="primary" size="large" icon={<Add20Regular />} onClick={onCreateClick} className={styles.createButton}>
                {intl.formatMessage(ExtendedAgentsGraphResources.createSubagent)}
            </Button>
        </div>
    );
};
