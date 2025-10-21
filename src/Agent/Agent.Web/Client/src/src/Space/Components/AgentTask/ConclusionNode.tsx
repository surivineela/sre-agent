import { Badge, Card, CardHeader, makeStyles, mergeClasses, Subtitle1, Subtitle2, tokens } from '@fluentui/react-components';
import { Handle, NodeProps, Position } from '@xyflow/react';
import { memo, useContext } from 'react';
import { FormattedMessage } from 'react-intl';
import { AgentTaskResources } from '../../../Strings/SREAgentResources';
import { AgentTaskNodeSize, GraphFlowNode } from '../../Contracts/Activities';
import { AgentTaskGraphContext } from '../../Contracts/Context';
import { useCommonStyles } from './Utility';

const useStyles = makeStyles({
    nodeContainer: {
        width: `${AgentTaskNodeSize.ConclusionNode.width}px`,
        maxHeight: `${AgentTaskNodeSize.ConclusionNode.height}px`,
        position: 'relative',
    },
    tag: {
        position: 'absolute',
        top: '-18px',
        left: '50%',
        transform: 'translateX(-50%)',
        zIndex: 2,
    },
    card: {
        backgroundColor: tokens.colorNeutralBackground2,
        width: '100%',
        height: '100%',
        padding: '35px 16px',
        zIndex: 1,
    },
    title: {
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        display: '-webkit-box',
        WebkitLineClamp: 5,
        WebkitBoxOrient: 'vertical',
    },
    handle: {
        opacity: 0,
        pointerEvents: 'none',
    },
});

const ConclusionNode = (props: NodeProps<GraphFlowNode>) => {
    const { data, id } = props;

    const { nodeContainer, tag, card, title, handle } = useStyles();
    const commonStyles = useCommonStyles();

    const { selectedNodeId, selectNode } = useContext(AgentTaskGraphContext);

    return (
        <div className={nodeContainer}>
            <Badge className={tag} size={'extra-large'}>
                <Subtitle2>
                    <FormattedMessage {...AgentTaskResources.conclusionNodeText} />
                </Subtitle2>
            </Badge>
            <Card
                focusMode={'tab-only'}
                className={mergeClasses(card, commonStyles.card, commonStyles.cardBorder)}
                selected={selectedNodeId === id}
                onSelectionChange={(_, selection) => selectNode(selection.selected ? id : null)}
            >
                <CardHeader
                    header={
                        <Subtitle1 block={true} className={title}>
                            {data.title}
                        </Subtitle1>
                    }
                />
            </Card>
            <Handle type={'target'} position={Position.Top} isConnectable={false} className={handle} />
        </div>
    );
};

export default memo(ConclusionNode);
