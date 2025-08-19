import { Body1, Card, CardHeader, makeStyles, Subtitle1, Text, tokens } from '@fluentui/react-components';
import { NodeProps } from '@xyflow/react';
import { memo, useContext } from 'react';
import ReactMarkdown from 'react-markdown';
import rehypeRaw from 'rehype-raw';
import remarkGfm from 'remark-gfm';
import { AgentTaskNodeSize, GraphFlowNode } from '../../Contracts/Activities';
import { AgentTaskGraphContext } from '../../Contracts/Context';
import { getInitialInvestigationStepsIcon } from './Utility';

const useStyles = makeStyles({
    nodeContainer: {
        pointer: 'cursor',
        width: `${AgentTaskNodeSize.InitialInvestigationNode.width}px`,
        height: `${AgentTaskNodeSize.InitialInvestigationNode.height}px`,
    },
    card: {
        border: `1px solid ${tokens.colorNeutralBackground3Selected} `,
        borderRadius: tokens.borderRadiusXLarge,
        width: '100%',
        height: '100%',
        padding: '16px',
        zIndex: 0,
    },
    header: {
        display: 'flex',
        gap: `${tokens.spacingHorizontalM}`,
        justifyContent: 'space-between',
        alignItems: 'center',
        width: '100%',
    },
    title: {
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        display: '-webkit-box',
        WebkitLineClamp: 2,
        WebkitBoxOrient: 'vertical',
    },
    description: {
        width: '100%',
        height: '100%',
        overflow: 'hidden',
        display: 'flex',
        flexDirection: 'column',
        '> *': {
            padding: '0px',
            margin: '0px',
            flex: '0 0 auto',
            minHeight: '0px',
        },
        '::after': {
            content: '""',
            position: 'absolute',
            bottom: '0',
            left: '0',
            width: '100%',
            height: '30%',
            background: 'linear-gradient(transparent, white)',
        },
    },
    stepsContainer: {
        height: '100%',
        width: '100%',
        overflow: 'hidden',
    },
    stepContainer: {
        display: 'flex',
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'flex-start',
        gap: tokens.spacingHorizontalXXS,
        padding: `${tokens.spacingVerticalXS} 0px`,
    },
    stepText: {
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
    },
});

const InitialInvestigationNode = (props: NodeProps<GraphFlowNode>) => {
    const { id, data } = props;

    const { nodeContainer, card, header, title, description, stepsContainer, stepContainer, stepText } = useStyles();

    const { selectedNodeId, selectNode } = useContext(AgentTaskGraphContext);

    const isSummaryNode = data.showInitialInvestigationSummary;

    const Summary = () => {
        return (
            <div className={description}>
                <ReactMarkdown
                    remarkPlugins={[remarkGfm]}
                    rehypePlugins={[rehypeRaw]}
                    components={{
                        p: ({ children }) => <Text as="span">{children}</Text>,
                        h1: ({ children }) => (
                            <Text as="span" weight="semibold" style={{ marginTop: '2px' }}>
                                {children}
                            </Text>
                        ),
                        h2: ({ children }) => (
                            <Text as="span" weight="semibold" style={{ marginTop: '2px' }}>
                                {children}
                            </Text>
                        ),
                        h3: ({ children }) => (
                            <Text as="span" weight="semibold" style={{ marginTop: '2px' }}>
                                {children}
                            </Text>
                        ),
                        h4: ({ children }) => (
                            <Text as="span" weight="semibold" style={{ marginTop: '2px' }}>
                                {children}
                            </Text>
                        ),
                        h5: ({ children }) => (
                            <Text as="span" weight="semibold" style={{ marginTop: '2px' }}>
                                {children}
                            </Text>
                        ),
                        h6: ({ children }) => (
                            <Text as="span" weight="semibold" style={{ marginTop: '2px' }}>
                                {children}
                            </Text>
                        ),
                        ul: ({ children }) => <div>{children}</div>,
                        li: ({ children }) => <Text as="span">{children}</Text>,
                        strong: ({ children }) => <Text as="span">{children}</Text>,
                        blockquote: ({ children }) => <blockquote>{children}</blockquote>,
                    }}
                >
                    {data.description}
                </ReactMarkdown>
            </div>
        );
    };

    const Steps = () => {
        const steps = data.gatheringContextSteps || [];

        const statusIcon = getInitialInvestigationStepsIcon(data.status);

        let stepsToDisplay = steps.slice(0, 6); // Display only the first 6 steps
        let showEllipsis = false;

        if (steps.length > 6) {
            // If the steps exceed 6, we slice the first 5 and set a flag to show ellipsis
            stepsToDisplay = steps.slice(0, 5);
            showEllipsis = true;
        }

        return (
            <div className={stepsContainer}>
                {stepsToDisplay.map((step, index) => (
                    <div key={index} className={stepContainer}>
                        {statusIcon}
                        <Body1 className={stepText}>{step.title}</Body1>
                    </div>
                ))}
                {showEllipsis && (
                    <Body1 style={{ margin: '16px 0px 0px 16px' }}>{`and ${steps.length - stepsToDisplay.length} more`}</Body1>
                )}
            </div>
        );
    };

    return (
        <div className={nodeContainer}>
            <Card
                className={card}
                // Do not check the data.id because it is the investigation group node's id
                selected={selectedNodeId === id}
                onSelectionChange={(_, selection) => {
                    selectNode(selection.selected ? id : null);
                }}
                focusMode={'tab-only'}
            >
                <CardHeader
                    header={
                        <div className={header}>
                            <Subtitle1 block={true} className={title}>
                                {isSummaryNode ? 'Summary' : 'Investigation steps'}
                            </Subtitle1>
                        </div>
                    }
                />
                {isSummaryNode ? <Summary /> : <Steps />}
            </Card>
        </div>
    );
};

export default memo(InitialInvestigationNode);
