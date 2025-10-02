import { Card, CardHeader, Link, makeStyles, Subtitle2, Text, tokens } from '@fluentui/react-components';
import { TaskListAdd24Regular } from '@fluentui/react-icons';
import { memo, useContext } from 'react';
import { FormattedMessage } from 'react-intl';
import { TodoInfo } from '../../Common/Contracts/DataPlane/TodoInfo';
import { ChatBoxContext } from '../Contracts/Context';

const useStyles = makeStyles({
    root: {
        display: 'flex',
        flexDirection: 'column',
        gap: '12px',
        padding: '5px 0px',
    },
    card: {
        height: 'fit-content',
        padding: '20px',
        borderRadius: tokens.borderRadiusLarge,
    },
    header: {
        width: '100%',
    },
});

const TodoPlanChatMessage = ({ todoPlan }: { todoPlan: TodoInfo }) => {
    const { openTodoPlan } = useContext(ChatBoxContext);

    const styles = useStyles();

    return (
        <div className={styles.root}>
            <Text>
                <FormattedMessage id="6HiPiX" defaultMessage="A new To-do Plan has been created:" />
            </Text>
            <Card
                orientation="horizontal"
                className={styles.card}
                onClick={e => {
                    e.stopPropagation();
                }}
            >
                <CardHeader
                    className={styles.header}
                    image={<TaskListAdd24Regular fontSize={32} />}
                    header={
                        <Link
                            appearance="subtle"
                            onClick={e => {
                                e.stopPropagation();
                                openTodoPlan(todoPlan);
                            }}
                        >
                            <Subtitle2 wrap={true} block={true}>
                                {todoPlan.title}
                            </Subtitle2>
                        </Link>
                    }
                />
            </Card>
        </div>
    );
};

export default memo(TodoPlanChatMessage);
