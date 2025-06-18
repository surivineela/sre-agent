import { mergeClasses, tokens } from '@fluentui/react-components';
import { mergeStyleSets } from '@fluentui/react/lib/Styling';
import { memo, useCallback } from 'react';
import ReactMarkdown from 'react-markdown';
import rehypeRaw from 'rehype-raw';
import remarkGfm from 'remark-gfm';

const chatMessageStyles = mergeStyleSets({
    root: {
        backgroundColor: tokens.colorNeutralBackground3,
        padding: '0px 16px',
        borderRadius: tokens.borderRadiusXLarge,
    },
    codeBlock: {
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
    preBlock: {
        overflowX: 'auto',
        overflowY: 'hidden',
        backgroundColor: tokens.colorNeutralBackground6,
        borderRadius: tokens.borderRadiusSmall,
        padding: '15px',
    },
});

const ReactMarkdownComponent = ({ content, isUserMessage }: { content?: string | null; isUserMessage?: boolean }) => {
    const aLinkRenderer = useCallback((props: any) => {
        return (
            <a href={props.href} target="_blank" rel="noopener noreferrer">
                {props.children}
            </a>
        );
    }, []);

    const codeRenderer = useCallback((props: any) => {
        // Check if this code element is inside a pre element (code block)
        const isInPre = props.node?.parent?.tagName === 'pre';
        const className = isInPre ? chatMessageStyles.codeBlockInPre : chatMessageStyles.codeBlock;
        return <code className={className}>{props.children}</code>;
    }, []);

    const preRenderer = useCallback((props: any) => {
        return <pre className={chatMessageStyles.preBlock}>{props.children}</pre>;
    }, []);

    return (
        <div className={mergeClasses('markdown-content', isUserMessage ? undefined : chatMessageStyles.root)}>
            <ReactMarkdown
                components={{
                    a: aLinkRenderer,
                    code: codeRenderer,
                    pre: preRenderer,
                }}
                remarkPlugins={[remarkGfm]}
                rehypePlugins={[rehypeRaw]}
            >
                {content}
            </ReactMarkdown>
        </div>
    );
};

export default memo(ReactMarkdownComponent);
