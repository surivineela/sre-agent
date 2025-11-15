import { Link, makeStyles, tokens, Tooltip } from '@fluentui/react-components';
import { Agents16Regular, ClipboardTaskList16Regular, Warning16Regular } from '@fluentui/react-icons';
import { FC, MouseEventHandler, useMemo } from 'react';

const useStyles = makeStyles({
    wrapper: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
        overflowX: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
    },
    icon: {
        flex: 'none',
    },
    link: {
        fontSize: '13px',
        lineHeight: '20px',
        overflow: 'hidden',
        width: '100%',
        '> a': {
            textOverflow: 'ellipsis',
        },
    },
});

export interface ResponsePlanLinkWithIconProps {
    type: 'responsePlan' | 'handlingAgent' | 'agentTrigger';
    value: string;
    onClick: MouseEventHandler;
}

export const ResponsePlanLinkWithIcon: FC<ResponsePlanLinkWithIconProps> = ({ type, value, onClick }) => {
    const styles = useStyles();
    const Icon = useMemo(
        () => (type === 'responsePlan' ? ClipboardTaskList16Regular : type === 'handlingAgent' ? Agents16Regular : Warning16Regular),
        [type]
    );

    return (
        <div className={styles.wrapper}>
            <Icon className={styles.icon} aria-hidden={true} />
            <Tooltip content={value} relationship="label">
                <Link className={styles.link} onClick={onClick}>
                    {value}
                </Link>
            </Tooltip>
        </div>
    );
};
