import {
    Link,
    Subtitle1,
    Subtitle2,
    Table,
    TableBody,
    TableCell,
    TableHeader,
    TableHeaderCell,
    TableRow,
    Text,
    Title3,
    makeStyles,
    mergeClasses,
    tokens,
} from '@fluentui/react-components';
import { memo, useMemo } from 'react';
import ReactMarkdown from 'react-markdown';
import rehypeRaw from 'rehype-raw';
import remarkGfm from 'remark-gfm';

const useStyles = makeStyles({
    chatRoot: {
        backgroundColor: tokens.colorNeutralBackground3,
        padding: '1px 16px',
        borderRadius: tokens.borderRadiusXLarge,
    },
    codeInline: {
        backgroundColor: tokens.colorNeutralBackground6,
        fontFamily: tokens.fontFamilyMonospace,
        fontSize: tokens.fontSizeBase200,
        display: 'inline-block',
        padding: '2px 4px',
        borderRadius: tokens.borderRadiusSmall,
    },
    codeBlockInPre: {
        backgroundColor: tokens.colorTransparentBackground,
        fontFamily: tokens.fontFamilyMonospace,
        fontSize: tokens.fontSizeBase200,
        display: 'block',
    },
    pre: {
        overflowX: 'auto',
        overflowY: 'hidden',
        backgroundColor: tokens.colorNeutralBackground6,
        borderRadius: tokens.borderRadiusSmall,
        padding: '15px',
    },
    h3: { fontWeight: '600', fontSize: '14px', lineHeight: '20px' },
    h2: { fontWeight: '600', fontSize: '16px', lineHeight: '22px' },
    h1: { fontWeight: '600', fontSize: '20px', lineHeight: '26px' },
    blockquote: {
        borderLeft: `4px solid ${tokens.colorNeutralStroke1}`,
        paddingLeft: '10px',
        marginLeft: '0',
        marginRight: '0',
        marginBottom: '10px',
        fontStyle: 'italic',
    },
});

interface ReactMarkdownComponentProps {
    content?: string | null;
    className?: string;
    variant?: 'chat' | 'panel' | 'default';
    /** When variant === 'chat', controls bubble styling like user vs assistant */
    isUserMessage?: boolean;
}

const ReactMarkdownComponent = ({ content, className, variant = 'default', isUserMessage }: ReactMarkdownComponentProps) => {
    const styles = useStyles();

    // Variant-specific renderers
    const headingRenderers = useMemo(() => {
        if (variant === 'panel') {
            return {
                h1: ({ children }: any) => (
                    <Title3 as={'h1'} block>
                        {children}
                    </Title3>
                ),
                h2: ({ children }: any) => (
                    <Subtitle1 as={'h2'} block>
                        {children}
                    </Subtitle1>
                ),
                h3: ({ children }: any) => (
                    <Subtitle2 as={'h3'} block>
                        {children}
                    </Subtitle2>
                ),
                h4: ({ children }: any) => (
                    <Subtitle2 as={'h4'} block>
                        {children}
                    </Subtitle2>
                ),
                h5: ({ children }: any) => (
                    <Subtitle2 as={'h5'} block>
                        {children}
                    </Subtitle2>
                ),
                h6: ({ children }: any) => (
                    <Subtitle2 as={'h6'} block>
                        {children}
                    </Subtitle2>
                ),
                p: ({ children }: any) => <Text as="p">{children}</Text>,
            };
        }

        // Chat & default headings (may merge in the future)
        return {
            h1: ({ children }: any) => <h1 className={styles.h1}>{children}</h1>,
            h2: ({ children }: any) => <h2 className={styles.h2}>{children}</h2>,
            h3: ({ children }: any) => <h3 className={styles.h3}>{children}</h3>,
        };
    }, [styles, variant]);

    const rootClass = mergeClasses(
        variant === 'chat' ? 'markdown-content' : undefined, // Don't know if this actually does anything...
        className,
        variant === 'chat' && !isUserMessage ? styles.chatRoot : undefined
    );

    return (
        <div className={rootClass}>
            <ReactMarkdown
                remarkPlugins={[remarkGfm]}
                rehypePlugins={[rehypeRaw]}
                components={{
                    strong: ({ children }: any) => (
                        <Text as="strong" weight="semibold">
                            {children}
                        </Text>
                    ),
                    em: ({ children }: any) => (
                        <Text as="em" italic>
                            {children}
                        </Text>
                    ),
                    blockquote: ({ children }: any) => <blockquote className={styles.blockquote}>{children}</blockquote>,
                    a: ({ children, href }: any) => (
                        <Link href={href} target="_blank" rel="noopener noreferrer">
                            {children}
                        </Link>
                    ),
                    code: (props: any) => {
                        const isInPre = props.node?.parent?.tagName === 'pre';
                        const cls = isInPre ? styles.codeBlockInPre : styles.codeInline;
                        return <code className={cls}>{props.children}</code>;
                    },
                    pre: (props: any) => <pre className={styles.pre}>{props.children}</pre>,
                    table: (props: any) => (
                        <Table style={{ tableLayout: 'auto', marginTop: tokens.spacingVerticalM, marginBottom: tokens.spacingVerticalM }}>
                            {props.children}
                        </Table>
                    ),
                    thead: (props: any) => <TableHeader>{props.children}</TableHeader>,
                    tbody: (props: any) => <TableBody>{props.children}</TableBody>,
                    tr: (props: any) => <TableRow>{props.children}</TableRow>,
                    th: (props: any) => (
                        <TableHeaderCell>
                            <Text weight="semibold">{props.children}</Text>
                        </TableHeaderCell>
                    ),
                    td: (props: any) => <TableCell>{props.children ?? '-'}</TableCell>,
                    ...headingRenderers,
                }}
            >
                {content}
            </ReactMarkdown>
        </div>
    );
};

export default memo(ReactMarkdownComponent);
