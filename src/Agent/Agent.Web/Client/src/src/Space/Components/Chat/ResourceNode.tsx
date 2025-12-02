import {
    $applyNodeReplacement,
    DecoratorNode,
    EditorConfig,
    LexicalNode,
    NodeKey,
    SerializedLexicalNode,
    tokens,
} from '@fluentui-copilot/react-copilot';

export type ResourceNodeType = 'resourceNode';

export type SerializedResourceNode = SerializedLexicalNode & {
    type: ResourceNodeType;
    version: 1;
    text: string;
    displayText: string;
};

export class ResourceNode extends DecoratorNode<JSX.Element> {
    __text: string;
    __displayText: string;

    static clone(node: ResourceNode): ResourceNode {
        return new ResourceNode(node.__text, node.__displayText, node.__key);
    }

    constructor(text: string, displayText: string, key?: NodeKey) {
        super(key);
        this.__text = text;
        this.__displayText = displayText;
    }

    static getType(): string {
        return 'resourceNode';
    }

    getTextContent(): string {
        // Add an extra spaces to separate from adjacent text
        return ' ' + this.__text + ' ';
    }

    createDOM(_config: EditorConfig): HTMLElement {
        const dom = document.createElement('span');
        // Lexical manages lifecycle; the actual content will come from React
        return dom;
    }

    updateDOM(): boolean {
        return true; // React handles updates
    }

    // This is the key method for decorated nodes
    decorate(): JSX.Element {
        return (
            <span
                style={{
                    display: 'inline-flex',
                    alignItems: 'center',
                }}
            >
                <span
                    style={{
                        backgroundColor: tokens.colorBrandBackground2,
                        color: tokens.colorBrandForeground2,
                        padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}`,
                        borderRadius: tokens.borderRadius2XL,
                        fontSize: tokens.fontSizeBase300,
                        fontWeight: tokens.fontWeightMedium,
                    }}
                >
                    {this.__displayText}
                </span>
            </span>
        );
    }

    // Required for DecoratorNode
    isInline(): boolean {
        return true;
    }

    isIsolated(): boolean {
        return true;
    }

    // When caret moves left into this node, collapse before it
    collapseAtStart(): boolean {
        return true;
    }

    // When caret moves right into this node, collapse after it
    collapseAtEnd(): boolean {
        return true;
    }

    // Serialization
    exportJSON(): SerializedResourceNode {
        return {
            ...super.exportJSON(),
            type: 'resourceNode',
            version: 1,
            text: this.__text,
            displayText: this.__displayText,
        };
    }

    static importJSON(serializedNode: SerializedResourceNode): ResourceNode {
        const node = $createResourceNode(serializedNode.text, serializedNode.displayText);
        return node;
    }

    // Update methods
    setText(text: string): void {
        const writable = this.getWritable();
        writable.__text = text;
    }

    setDisplayText(displayText: string): void {
        const writable = this.getWritable();
        writable.__displayText = displayText;
    }
}

// Factory helper
export function $createResourceNode(text: string, displayText: string): ResourceNode {
    const node = new ResourceNode(text, displayText);
    return $applyNodeReplacement(node);
}

// Type guard
export function $isResourceNode(node: LexicalNode | null | undefined): node is ResourceNode {
    return node instanceof ResourceNode;
}
